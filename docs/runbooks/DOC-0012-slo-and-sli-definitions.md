---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0012
artifact_type: ops
title: "SLO and SLI definitions for the order processing service"
status: approved
stage: S7
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
# Approver is a placeholder for the EM at review; this draft records no approval and no date.
approver: { kind: human, name: "Komal Bahetwar", role: "EM", date: 2026-10-09 }
created: 2026-10-09
updated: 2026-10-09
sources: [NFR-0001, PRD-0001, DOC-0005, DOC-0008]
jurisdiction: []
superseded_by: null
---

# SLO and SLI definitions for the order processing service

This document says what "healthy" means for the order processing service and how
we would know when it is not. It defines one service level indicator (SLI) and
one service level objective (SLO) for each signal an operator cares about, plus
the error budget policy that governs them.

Honesty first: the service has no production traffic. It runs in a local Docker
Compose environment, and the only runtime evidence we hold is the deployment
check in DOC-0008 and the 45-test run in DOC-0005. Everything below is the
target we would operate to once the service runs in production. The performance
and availability numbers are targets to monitor, not measured results, and the
availability budget was formally waived at G5 for this take-home. The
correctness and timeliness behaviors are demonstrated by tests; the latency and
availability numbers are not.

## Scope

The service exposes five operations: create an order, retrieve an order by id,
list and filter orders, advance the status, and cancel a pending order. A
scheduled job moves pending orders to processing. The SLIs below cover the
request path and that automatic move. We do not define a separate SLI for each
operation where the budget is the same; read and list share the read budget,
and create, status change, and cancel share the write budget, because that is
how NFR-0001 groups them.

## SLI and SLO summary

| SLI | What it measures | SLO target | Window | Source |
|---|---|---|---|---|
| Availability | The share of observed time in which the service answered requests successfully and its data store was reachable | 99.5% | Calendar month | NFR-0001 |
| Read and list latency | The p95 and p99 of request duration for GET order by id and GET order list | p95 under 300 ms, p99 under 800 ms at the assumed peak | Calendar month, sampled | NFR-0001 |
| Write latency | The p95 and p99 of request duration for create, status change, and cancel | p95 under 500 ms, p99 under 1000 ms at the assumed peak | Calendar month, sampled | NFR-0001 |
| Automatic move timeliness | The age of the oldest pending order, and the share of pending orders moved within about five minutes | No pending order waits more than about five minutes, and orders already past pending are untouched | Continuous, checked each run | PRD-0001, NFR-0001 |

The assumed peak is the launch assumption in NFR-0001: 10 requests per second
sustained, bursting to 25 requests per second for 60 seconds. It is a stated
starting point, not a measurement. When real traffic exists, the first review
of this document replaces the assumption with the observed peak.

## How each SLI is measured

### Availability

The availability SLI is the share of time in which the service passes a
synthetic request and its readiness check. We measure it two ways and reconcile
them:

- A synthetic probe calls `GET /health/ready` on a fixed interval. The probe
  records success or failure per check.
- The request path records every response's status. A request that returns a
  5xx counts against availability; a documented 4xx (validation, not found,
  conflict) does not, because the caller sent a request the service correctly
  refused.

The monthly target is 99.5% of the window. The window is the calendar month.
We keep the raw probe results and the request outcomes for the month so the
number can be recomputed, and we do not average away a bad hour by pointing at
a good week.

### Read and list latency

Latency is measured at the server, as the elapsed time from the start of
request handling to the response being written. We record a histogram of
durations, tagged by operation and by outcome, and read p95 and p99 from it over
the window at the assumed peak. A separate figure for reads and writes matters
because the two have different budgets and different causes: a slow read usually
points at the store or the query, a slow write at the transaction or the
concurrency token. A latency figure that mixes them hides which one moved.

### Write latency

Same mechanism as read latency, computed from the same histogram, filtered to
the create, status change, and cancel operations. We read p95 and p99 and
compare each to its own target. The contract responds on the write path only
after the transaction commits, so this figure covers the store write as well as
request handling.

### Automatic move timeliness

The automatic move timeliness SLI has two parts. The primary figure is the age
of the oldest order still in PENDING, read from the store on each measurement.
If that age passes about five minutes, the move is late, whatever the cause. The
secondary figure is how many pending orders the last run processed against how
many were eligible, which tells us whether the run is keeping up or falling
behind.

We measure the primary figure by sampling the store on a short interval and
noting the oldest creation time among PENDING orders. We measure the secondary
figure from the run itself: the run records its duration and the number of
orders it moved. The correctness half of this SLO (an order that has already
left PENDING is not touched, and a repeated run changes nothing) is proved by
the integration tests in DOC-0005, not inferred from the timing sample.

The real-time five-minute sample is not part of the 45-test run, so the timing
half of this SLO is a target to monitor, not a measured result.

## Error budget policy

The availability target of 99.5% leaves an error budget of 0.5% of the window:
about three hours and thirty-six minutes in a 30-day month. We spend the budget
on change, not on drift, and we treat its exhaustion as a decision point rather
than a number to explain away.

The policy:

- When the budget is healthy, we ship normal changes on the usual change path.
- When half the budget is spent before the window is half over, the SRE Lead
  and the EM review the cause at the next ops review and slow the release pace.
- When the budget is exhausted, new feature work for the service freezes. The
  team's first job is reliability work: diagnose, fix, and only then resume.
  Unfreezing is a recorded decision by the EM, not a default.
- A single incident that spends a large share of the budget, as a working
  threshold say 20 percent, gets a blameless postmortem even if the budget is
  not yet exhausted, because the size of the loss matters more than the total.

For this take-home the availability budget is nominal. It was waived at G5
because there is no production traffic to spend it against, and the waiver is
recorded in DOC-0005 and reflected in the change record DOC-0008. The budget
starts to bind when the service runs and the probe data exists.

## What is not covered here

- Four budgets in NFR-0001 are targets to monitor but are not measured in this
  environment: peak request volume, read and list latency, write latency, and
  availability. All four were waived at G5. The three latency and volume
  figures need a separate load test, and availability needs a month of recorded
  runtime data. Until then they are the numbers we will hold ourselves to, not
  numbers we have.
- The observability signals that feed these SLIs (the per-transition log line,
  the counter, the correlation id, and the 200 ms health check) are implemented
  but are not automatically asserted. That gap is tracked in DOC-0017.
- Cost, capacity, and access review are S7 activities but not SLIs, so they are
  out of scope for this document.

## Open items

- The alert thresholds that watch these SLOs are in DOC-0013.
- The response to each breached SLO is in DOC-0014.
- The performance and availability measurement is owed and tracked in DOC-0017.
