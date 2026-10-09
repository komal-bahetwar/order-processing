---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0014
artifact_type: runbook
title: "Operational runbooks for the order processing service"
status: approved
stage: S7
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
# Approver is a placeholder for the SRE Lead at review; this draft records no approval and no date.
approver: { kind: human, name: "Komal Bahetwar", role: "SRE Lead", date: 2026-10-09 }
created: 2026-10-09
updated: 2026-10-09
sources: [DOC-0012, DOC-0013, DOC-0002, DOC-0003, DOC-0005, DOC-0008, ADR-0002, ADR-0003]
jurisdiction: []
superseded_by: null
---

# Operational runbooks for the order processing service

This document holds the symptom, diagnosis, and action steps for the failure
modes we expect in the order processing service. Each runbook is written for the
on-call responder, who may not have built the service. Read the symptom the
alert or the report gives you, follow the diagnosis until a cause is confirmed,
then take the matching action.

Last verified: 2026-10-09. These runbooks were written from the design in
DOC-0002 and DOC-0003 and from the test evidence in DOC-0005, not from
production incidents. The service has never run under production traffic, so no
step here has been exercised against a real incident. The safest reading is that
the diagnosis paths are grounded in the design while the recovery times are
unproven.

## Failure mode 1: the automatic move is not running or is failing

Symptom

- Alert 1 from DOC-0013 fires: a PENDING order is older than ten minutes and
  pending orders still exist.
- Or an operator sees PENDING orders that should have moved and nothing is
  processing them.
- The Hangfire dashboard shows the `process-pending-orders` recurring job as
  missing, disabled, or failing on repeated attempts.

Diagnosis

1. Check whether the scheduler is running at all. Open the Hangfire dashboard
   and confirm the `process-pending-orders` recurring job exists and its last
   execution is recent. If the job is missing, the scheduler has not registered
   it, which usually means the API start sequence did not reach the scheduler
   step.
2. If the job exists but fails, read its failure reason and the API log for the
   same time. A database error points at failure mode 2; a domain or mapping
   error points at the service logic.
3. Confirm the schedule. The expected value is `OrderProcessing:CronExpression`,
   default `*/5 * * * *`. A changed or malformed cron explains a job that runs
   far less often than it should.
4. Check the batch. `OrderProcessing:BatchSize` default 200 is the greatest
   number of orders one run processes. If the pending backlog is larger than the
   batch, orders move but slowly, and the oldest-order figure grows. That is
   falling behind, not a stall, and the fix is different.
5. Confirm the run itself. The run records its duration and how many orders it
   moved. A run that completes in milliseconds with zero moved while pending
   orders exist means the query or the filter is wrong, not that the scheduler
   is dead.

Action

- If the job is missing, restart the API so the startup path registers the
  recurring job, then confirm the job appears and its first run moves the
  backlog. Restarting is safe: the job is idempotent, and the domain guard
  rejects an order that has already left PENDING.
- If the cron is wrong, correct `OrderProcessing:CronExpression` to the intended
  schedule, restart, and confirm the next run.
- If the backlog exceeds the batch, raise `OrderProcessing:BatchSize` for the
  catch-up and lower it again once the backlog clears. Do not leave a huge batch
  in place as the steady state; it lengthens each run and delays the next.
- If the run fails on the database, switch to failure mode 2 and come back.
- If the run fails on logic, capture the order id and the exception, treat it as
  a defect, and route it as a new S0 item. Do not hand-edit order rows to hide
  the failure; a correction is itself an audited change.

Verification

- The oldest PENDING order age falls back under the budget, and the next run
  reports a processed count that matches the eligible backlog.
- The alert clears and stays clear for one full schedule cycle.

## Failure mode 2: the database is unavailable

Symptom

- Alert 4 or 5 from DOC-0013 fires: readiness returns a non-200, or the store
  connection check fails.
- Requests return 500 with `INTERNAL_ERROR`; the create, retrieve, list, status,
  and cancel paths all fail together because they share the store.
- The startup log shows a connection failure or a migration that cannot reach
  the database.

Diagnosis

1. Check the database process first. In the local environment, run
   `docker compose ps` and confirm the PostgreSQL 18 container is healthy. The
   compose healthcheck runs `pg_isready` against the `orderprocessing` database.
2. Check the connection. Confirm the API is pointed at the right host and port
   and that the connection string is set. In compose the host is the `postgres`
   service on 5432; from a host process it is `localhost`.
3. Check readiness. `GET /health/ready` reports whether the application store
   and the Hangfire store are both reachable. A ready of non-200 with the
   database process up points at a credentials or network problem, not a dead
   server.
4. Check for a recent change that broke the connection, for example a port
   change through `POSTGRES_PORT` or a compose edit.
5. Check disk and logs on the database if the process is unhealthy.

Action

- If the database container is stopped, start it and wait for healthy. The API
  depends on a healthy database and may need a restart to reconnect cleanly.
- If the credentials or host are wrong, correct the connection string, restart
  the API, and confirm readiness.
- If the database is corrupt or its volume is unusable, restore from the most
  recent backup. In this take-home there is no backup and no production data, so
  the local volume can be recreated with `docker compose down -v` and a fresh
  start, accepting that local orders are lost. That is a take-home convenience,
  not a production procedure; a real deployment must restore from a verified
  backup, because orders are not regenerable.
- Never point the service at a different database to get it green. The data is
  the system of record for the orders.

Verification

- `GET /health/ready` returns 200, and a create, retrieve, list, status change,
  and cancel each complete.
- The automatic move resumes on its next schedule and clears the PENDING
  backlog.

## Failure mode 3: repeated concurrency conflicts

Symptom

- Writes return 409 with `CONCURRENCY_CONFLICT` more often than expected.
- Alert 2 from DOC-0013 may fire if the conflict is translated to a 5xx on a
  path we did not anticipate, but the normal conflict is a documented 409 and
  does not by itself count against the error rate.
- Logs show repeated `DbUpdateConcurrencyException` for the same order, or the
  automatic move logs a skip for the same order across runs.

Diagnosis

1. Confirm the mechanism. Conflicts are expected and healthy under optimistic
   concurrency: two writers read the same `version`, one write wins, the other
   is told to retry. A single conflict is not a fault. A spike is the signal.
2. Separate the two sources. A conflict between a customer cancel and the
   automatic move on the same PENDING order is the designed race and the common
   case. A conflict between two manual status changes, or between two retries of
   the same caller, points at client retry behavior.
3. Check retry behavior. A client that retries immediately and with no backoff
   can generate its own conflict storm. The service translates the loser to a
   409; the caller must decide to re-read and retry, and the caller's policy is
   where the storm usually lives.
4. Check the automatic move. A batch should tolerate one conflict, skip that
   order, and continue. A batch that aborts on the first conflict is the defect
   fixed during S4; if it has come back, read DOC-0005 for the expected
   behavior.
5. Check for hot orders. An order touched by many actors at once, or a test that
   hammers one id, produces conflicts by construction.

Action

- If the load is a client retry storm, add or fix backoff on that caller. The
  409 is the contract working, not failing.
- If a single order is stuck in a conflict loop, read its current status and
  version, and let the automatic move and the cancel race settle; exactly one
  wins and the other stops. Do not force the status in the store.
- If the automatic move aborts a batch on a conflict, treat it as a defect:
  capture the run, confirm the change-tracker handling in the processing
  service, and route a fix.
- Do not switch to pessimistic locking as a first response. The critical section
  is small and the current design avoids holding a lock across I/O. A persistent
  conflict rate at volume is the trigger to reconsider, and that is a design
  decision, not an on-call action.

Verification

- The conflict rate returns to the expected level for the traffic, and no
  single order is repeated in the conflict log.
- A racing cancel and automatic move still leave the order in exactly one state,
  with a conflict reported for the loser.

## Failure mode 4: a startup migration fails

Symptom

- The API container starts but never reports ready, or exits during startup.
- The startup log shows an EF Core migration error, a schema conflict, or a
  failed `Database.Migrate()`.
- `GET /health/ready` never returns 200, and alert 4 may fire if the instance is
  expected to be up.

Diagnosis

1. Read the migration error in full. Common shapes are a duplicate object
   because a previous run partly applied, a constraint violation from existing
   data, and a type or name mismatch between the model and the database.
2. Check migration history. Confirm which migrations are recorded as applied and
   which the pending set contains. The service applies migrations at startup
   under a PostgreSQL advisory lock, so a second instance waits rather than
   applying them twice; a lock wait that never clears points at a crashed
   applier.
3. Check the database state against the model. A column name mismatch is the
   known shape from the build: EF originally generated PascalCase column names
   while the design check constraints reference snake_case, and the fix mapped
   every column to snake_case. A recurrence means the mapping regressed.
4. Check for a forward-only violation. Migrations are never edited once
   shipped; a defect is corrected by a new forward migration. A history that was
   rewritten produces a mismatch the applier cannot reconcile.
5. If existing data violates a new constraint, identify the offending rows
   before changing anything.

Action

- If a migration partly applied because a previous start was interrupted, the
  safe path is to restore the database to the last known-good state and let the
  migration run cleanly, rather than to patch the half-applied schema by hand.
- If the failure is a schema or model mismatch, fix the mapping in code, add a
  new forward migration if the store is behind, and restart. Do not edit an
  applied migration.
- If existing data violates a constraint, correct the data with a compensating,
  audited change, then let the migration proceed.
- In this take-home the local volume can be recreated if there is nothing to
  preserve, but a real deployment must not drop a database with orders in it.
- After any fix, confirm the schema matches the data model in DOC-0002 before
  declaring recovery.

Verification

- The API starts, applies its pending migrations, starts the scheduler, and
  reports ready.
- Readiness returns 200, and the contract operations complete against the new
  schema.

## Failure mode 5: high latency

Symptom

- Alert 3 from DOC-0013 fires: read and list p95 is above 300 ms, or write p95 is
  above 500 ms, over a ten-minute window at the assumed peak.
- Callers report slow responses without errors, and the request histogram shows
  the slow tail in one budget only, reads and lists or writes.
- The service is otherwise healthy: readiness is 200 and the error rate is
  normal.

Diagnosis

1. Separate reads from writes. The two budgets have different causes, so confirm
   which one moved. A read-shaped breach usually points at the query or the
   store; a write-shaped breach points at the transaction or the concurrency
   token.
2. Check the store. A slow or contended PostgreSQL is the common cause of both.
   Look at connection-pool saturation, long-running queries, and lock waits
   before blaming the service logic.
3. Check the load. Compare the observed request rate to the assumed peak in
   NFR-0001. A breach that only appears above the assumption is a capacity
   finding, not a regression.
4. Check for a recent change. A new query, index, or mapping can add a slow path
   without changing behavior, and it is the first thing to rule in or out.
5. Check the environment. A cold cache, a compaction, or a noisy neighbor can
   raise p95 for one window and recover on its own; a breach that persists is
   real.

Action

- If the store is the cause, follow failure mode 2 to confirm the database is
  healthy, then add or fix the index or query behind the slow budget.
- If the cause is a recent change, route a fix as a new S0 item. Do not paper
  over it by relaxing the budget.
- If the cause is capacity above the assumed peak, treat the assumption as wrong
  and take the budget to the ops review for revision with data.
- The two latency budgets were never measured in this environment and are the
  most likely SLO to be set wrong. A persistent breach is the signal to revise
  the budget with data, not to lower it to clear the alert.

Verification

- Read and write p95 fall back under their targets at the assumed peak, and the
  alert stays clear for one full window.

## Escalation

- Failure mode 1 or 2 that is not resolved quickly is a page to the on-call SRE,
  then the EM.
- Failure mode 3 at sustained volume is a design conversation, not an
  on-call-only fix; bring the SRE Lead and the Architect.
- Failure mode 4 is a change-control event: record what ran, why, and how the
  schema was restored. A failed migration that touched the store always leaves a
  written record.

## Known gaps

- These runbooks are not independently verified. The runbook quality bar asks
  for a test by someone other than the author; with no production environment
  and one operator, that verification is owed. It is tracked in DOC-0017.
- There is no backup or restore procedure in this environment because there is
  no production data. The database branch of failure mode 2 names this plainly
  rather than pretending a restore exists.
- The alert rules these runbooks pair with are in DOC-0013 and are not deployed.
