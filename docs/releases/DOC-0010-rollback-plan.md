---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0010
artifact_type: runbook
title: "Rollback plan for the order processing backend"
status: approved
stage: S6
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "SRE", date: 2026-10-09 }
gate: G6
sources: [DOC-0008, DOC-0002]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Rollback plan for the order processing backend

This is the S6 rollback plan, the A6.3 artifact for the G6 packet. It names the
triggers that send us back, the steps to run, and the database and data
considerations that make those steps safe. It is a draft for the SRE. No agent
has approved it and no agent runs it.

## Decision triggers

We roll back when any of these holds after a deploy:

- The API is unhealthy. The readiness endpoint does not return 200, or the
  liveness endpoint fails, so the process will not serve traffic.
- Migrations fail at startup. The API logs a migration failure and never
  becomes ready, so the schema and the code are out of step.
- A defect escaped to the local environment. A wrong status transition, a wrong
  order total, a broken error response, or a route that does not behave as the
  contract and DOC-0011 describe.
- A dependency or database problem that a redeploy is known to clear and that
  cannot be fixed faster in place.

If the trigger is a single bad request rather than a broken deploy, we do not
roll back; we fix forward and record the defect.

## Rollback steps

1. Stop the API. Run `docker compose stop api` to stop the API and leave the
   database up, or `docker compose down` to stop both.
2. Redeploy the previous version. Revert the compose change or check out the
   previous known-good commit and run `docker compose up --build` again. In a
   real environment, this is the point where we redeploy the previous image tag
   rather than build from source.
3. Re-run the previous commit. Start the service at the previous commit so the
   running code matches the rollback version.
4. Verify. Confirm `GET /health/ready` returns 200, then create, read, and list
   an order to smoke-test the path that failed.

## Database consideration

Migrations are forward-only with compensation, so there is no down migration to
run and a rollback of the service does not need to reverse the schema. The
previous service version is compatible with the current schema, because schema
changes are made backward-compatible first and any destructive step is retired
in a later release. If a schema defect itself has to be undone, the fix is a new
forward migration that compensates, written per the migration design in
DOC-0002; a shipped migration is never edited.

## Data consideration

The local database lives in the Docker volume `order-postgres-data`. The volume
can be reset with `docker compose down -v`, which stops the stack and deletes
the volume, so the next `up` starts from an empty store and applies the
migrations again. That is acceptable in the local environment because the data
is local test data with no personal data in it. In a real environment, a
destructive reset needs a verified backup or restore point first, per the
regulated-data rule in the process, and this plan would not delete a volume
without one.

## Who executes it

The release owner executes the rollback, the SRE role in a real environment and
the candidate wearing that hat for this take-home, with the Engineering Manager
informed when rollback is chosen. In a real environment the on-call engineer on
point runs it. A rollback is a human action. Agents may prepare the commands and
read the logs, but a human decides to roll back and a human runs it.

## Exercise status

The plan was exercised at the level of a `docker compose down` and `up` cycle
during the S6 deployment check on 2026-10-09. The stack came down, came back up,
the database reported healthy, migrations applied again, `GET /health/ready`
returned 200, and the API served requests after the cycle. The release owner
exercised the cycle and reviewed the result before G6, so the plan is tested by
someone other than the author. We did not exercise a full previous-version
redeploy, because there is no previous production version to roll back to in
this take-home. That redeploy is written as a documented step for a real
environment, and it is the part of this plan that is not yet proven by a live
run. Last verified: 2026-10-09.
