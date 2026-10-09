---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0016
artifact_type: review
title: "Telemetry and adoption report for the order processing service"
status: approved
stage: S8
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
# Approver is a placeholder for the PM at review; this draft records no approval and no date.
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G7
created: 2026-10-09
updated: 2026-10-09
sources: [DOC-0012, DOC-0005, DOC-0008, NFR-0001]
jurisdiction: []
superseded_by: null
---

# Telemetry and adoption report for the order processing service

This is the S8 adoption and telemetry report, the A8.2 artifact for the G7
packet. It would normally answer whether the service is used and whether it is
healthy in use. Here it answers a narrower thing, because there is no production
telemetry to report: it records what would be collected when the service runs,
why it is not available now, and what the first real report should contain.

## The telemetry position

There is no production telemetry. The service has run only in a local Docker
Compose environment during the assignment, with no external users and no
traffic beyond the deployment check in DOC-0008 and the test run in DOC-0005.
There is no metrics backend, no trace collector, and no log aggregation
endpoint wired in this environment. The instrumentation exists in code; the
export and the storage do not exist here.

This is not a gap we can close inside the take-home. Adoption data requires
users; SLO data requires an operating window; both require a deployment the
assignment does not include. The right move is to be explicit about it and to
define the signal set so the first real deployment produces a useful report
instead of a blank one.

## What would be collected when the service runs

The signals below are the ones the design already emits. The table gives the
name, the shape, what it answers, and its status in this environment.

| Signal | Shape | What it answers | Status here |
|---|---|---|---|
| `orders.transitions` counter | Counter, one increment per status change, tagged by order status old and new | How many orders move, and through which transitions | Implemented in code, not exported or stored |
| Request rate | Counter of HTTP requests, tagged by operation and response status | How much traffic arrives and what share succeeds | Implemented in code, not exported or stored |
| Request latency | Histogram of request duration, tagged by operation and outcome | Whether reads and writes are inside the latency budgets | Implemented in code, not exported or stored |
| Automatic move run duration | Histogram or gauge of the processing run's elapsed time | Whether the scheduled run is keeping up and how long a pass takes | Implemented in code, not exported or stored |
| Automatic move processed count | Counter or gauge of orders moved per run | Whether the run is moving the eligible backlog and how close it is to the batch size | Implemented in code, not exported or stored |
| Error codes | Counter of failures, tagged by the stable error code | Which failures happen and how often, split by the shared error shape | Implemented in code, not exported or stored |
| Health signals | Liveness and readiness probe results | Whether an instance is up and whether its data store is reachable | Implemented and observed once during the deployment check |
| Per-transition log line | Structured log line with order id, old status, new status, and correlation id | The audit trail of a single order's life and the correlation of a request to its change | Implemented in code, not aggregated |

The counter naming is a convention, not yet a schema. When a metrics store is
chosen, the names and labels should be frozen in a small metrics contract so the
dashboards and the alert rules in DOC-0013 do not drift from the emission code.

## Why the signals are not available here

- No metrics store or collector. OpenTelemetry is wired in the service, but
  there is nothing to export to and nothing to query. The counter and histogram
  values exist only for the process that emits them.
- No operating window. Availability and adoption are windowed figures. A single
  deployment check is an observation, not a window.
- No users. There is no client, no account, and no traffic generator, so there
  is no adoption to measure. The 45 tests exercise behavior; they do not
  simulate a population of callers.
- The observability assertions are owed. The per-transition log line, the
  counter, the correlation id, and the 200 ms health check are implemented, but
  no automated test asserts them, which is itself debt in DOC-0017. Until those
  assertions land, we cannot claim the signals are reliably emitted, only that
  they are coded.

## What the first real report should contain

When the service runs, this document becomes a real report and should carry:

- Volume: requests per second over the window, against the launch assumption of
  10 sustained and a 25 burst in NFR-0001. The first job is to replace the
  assumption with the observed peak.
- Latency: p95 and p99 for reads, lists, and writes, cut by operation, against
  the budgets in NFR-0001 and DOC-0012.
- Availability: the monthly share of successful time, against 99.5%, with the
  error budget spent and the incidents that spent it.
- Lifecycle throughput: orders created, moved, shipped, delivered, and
  cancelled, from `orders.transitions`, so we can see where orders stall.
- Automatic move health: run duration and processed count per run, and the age
  of the oldest PENDING order, against the about-five-minute budget.
- Failure mix: counts by error code, so the next latency or error tuning is
  aimed at the code that actually fires.
- Adoption, once there is any: the number of distinct callers or channels, the
  order volume per period, and the share of orders that reach each terminal
  state.

## Decision framing

The telemetry feeds the S8 decision the same way it would for any service. The
triggers to watch:

- If the observed peak is far below 10 requests per second, the launch
  assumption was high and the latency budgets should be re-derived, not left
  overstated.
- If read traffic grows faster than write traffic, the read replica and cache
  phase in FUTURE_PHASES.md becomes worth estimating.
- If the oldest PENDING age keeps touching the budget, the automatic move's
  batch size and schedule need a real tuning pass, not the shipped default.
- If conflict counts spike, the optimistic concurrency choice in ADR-0002 needs
  a design review, which is the item the risk register already names.

None of those triggers has fired, because there is no data. The report's
honest state is empty, and its value now is that the signal set and the
decisions it would inform are written down before the first deployment rather
than after.

## Recommendation

Keep this report as the placeholder it is and reissue it after the first real
operating window. Do not read the absence of telemetry as evidence of health:
the correct position is that the service is unobserved in production, not that
it is observed and fine. The signal set above and the assertions in DOC-0017 are
the two things that must exist before the next version of this report can claim
anything.
