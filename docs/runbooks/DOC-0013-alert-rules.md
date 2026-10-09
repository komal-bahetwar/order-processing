---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0013
artifact_type: ops
title: "Alert rules for the order processing service"
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
sources: [DOC-0012, NFR-0001, DOC-0005]
jurisdiction: []
superseded_by: null
---

# Alert rules for the order processing service

This document defines the alerts that watch the service against the SLOs in
DOC-0012. The rules are written as configuration and versioned with the service,
but they are not deployed in this environment: there is no metrics backend, no
collector, and no alert router in the take-home, so nothing here has fired or
been paged. Each rule below states its condition, its severity, and the runbook
in DOC-0014 it points at.

The rule expressions are written in the shape we would use with a metrics store
(Prometheus-style selectors over the OpenTelemetry metrics the service already
emits). They are illustrative of the condition, not a working config file. The
deployment step is to move them into whatever rule store the operating
environment provides and to point the router at the on-call rotation.

## Severity model

We use three severities, matching the incident model in the process:

- Critical: a client-visible failure or a stalled lifecycle. Page the on-call.
- Warning: the service is degrading or a budget is at risk. Open a ticket and
  watch it; page only if it persists or worsens.
- Info: a record for the ops review. No page.

## Rules

| # | Rule | Condition | Severity | Runbook |
|---|---|---|---|---|
| 1 | Automatic move stalled | The oldest PENDING order is older than 600 seconds while at least one PENDING order exists, sustained for two consecutive checks | Critical | DOC-0014, automatic move not running or failing |
| 2 | Error rate above threshold | The share of requests returning 5xx over a five-minute window is above 2 percent, sustained for ten minutes | Critical | DOC-0014, database unavailable; repeated concurrency conflicts |
| 3 | p95 latency above target | Read and list p95 is above 300 ms, or write p95 is above 500 ms, over a ten-minute window at the assumed peak | Warning | DOC-0014, high latency |
| 4 | Readiness failing | `GET /health/ready` returns a non-200 for two consecutive probes, or is unreachable for one minute | Critical | DOC-0014, database unavailable; startup migration fails |
| 5 | Database unreachable | The readiness data store check fails for one minute, or the store connection pool reports repeated connection failures | Critical | DOC-0014, database unavailable |

Two thresholds are deliberately mild because the service has no production
baseline. When real data exists, the ops review revisits them and tightens them
against the observed error and latency distributions, not against a guess.

## Rule detail

### Rule 1: automatic move stalled

Condition:

```
max_over_time(order_processing_pending_oldest_age_seconds[10m]) > 600
and on() count(order_processing_pending_orders) > 0
```

For: 2 consecutive evaluations.

What it means: a PENDING order has been eligible to move for more than ten
minutes, which is twice the about-five-minute budget. The ten-minute threshold
is chosen so a single late or slow run does not page, while a true stall does.
The second clause keeps a quiet service from alerting when there is simply
nothing to move.

The SLI this watches is the age of the oldest PENDING order in DOC-0012. The
first diagnosis is whether the scheduler ran at all, then whether the run
failed, then whether the store is rejecting the writes. The runbook walks those
in order.

### Rule 2: error rate above threshold

Condition:

```
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
  / sum(rate(http_server_request_duration_seconds_count[5m])) > 0.02
```

For: 10 minutes.

What it means: more than two requests in a hundred are failing as server
errors. Documented 4xx responses (validation, not found, conflict) do not count,
because the service handled those correctly. A sustained 5xx rate is a
server-side fault, and the most likely causes are the store being unreachable or
writes failing on a conflict path we did not expect. The alert links both
runbooks because the error code in the logs, `INTERNAL_ERROR` versus
`CONCURRENCY_CONFLICT`, tells the responder which one applies.

### Rule 3: p95 latency above target

Conditions:

```
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{operation=~"read|list"}[10m]))) > 0.300
or
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{operation=~"create|status|cancel"}[10m]))) > 0.500
```

For: 10 minutes.

What it means: the p95 request duration has passed the read or write target in
NFR-0001 at the assumed peak. This is a warning, not a page, because a brief
p95 excursion is usually load, a cold cache, or a compaction, and it may
recover on its own. It is worth watching because the two latency budgets were
never measured in this environment and are the most likely SLO to be set wrong.
A persistent breach is the trigger to run the high latency runbook and to
revise the budget.

### Rule 4: readiness failing

Condition:

```
probe_success{job="order-processing-ready"} == 0
```

For: 2 consecutive probes, or no probe result for 1 minute.

What it means: the service is up but not ready, which in this service means its
data store or its Hangfire store is not reachable, or startup migrations have
not completed. A readiness failure is what takes an instance out of rotation, so
the alert is critical even before clients report errors. The first action is to
read the readiness response and the startup log, then follow the database or
migration runbook as the cause points.

### Rule 5: database unreachable

Condition:

```
increase(order_processing_db_connection_failures_total[1m]) > 0
or probe_success{job="order-processing-ready"} == 0
```

For: 1 minute.

What it means: the service cannot reach PostgreSQL. This overlaps rule 4 by
design: readiness failing is the user-visible symptom, and this rule names the
likely cause so the page carries the right runbook. The runbook covers checking
the database process, the connection string, and the container health, and it
covers the recovery order so the API does not start before the store is healthy.

## Routing and ownership

- Critical alerts page the on-call SRE. If the on-call cannot restore service
  within the incident response window, they escalate to the EM.
- Warning alerts open a ticket for the next business day and are reviewed at the
  weekly ops review.
- The SLOs these rules watch are in DOC-0012. The steps to take are in
  DOC-0014. The four unmeasured budgets behind rules 1 and 3 are waived at G5
  and tracked in DOC-0017.

## Deferred

- No alert backend, collector, or router is deployed in this environment, so no
  rule has been exercised. The rules are reviewed from the design and the test
  evidence, not from a real alert.
- Error-budget burn-rate alerts (a fast and a slow burn window) are the natural
  next step once availability data exists. They are deferred with the
  availability measurement, not designed here.
