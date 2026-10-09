---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0003
artifact_type: adr
title: "Keep the automatic processing logic independent of the scheduler and safe under more than one instance"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Keep the automatic processing logic independent of the scheduler and safe under more than one instance

## Context and problem statement

FR-8 requires every PENDING order to become PROCESSING within about five
minutes with no manual step. The horizontal scalability budget in NFR-0001
requires that adding capacity adds throughput and that the automatic move
applies at most once per order when more than one instance runs. We have to
choose what triggers the move and where the processing logic lives, and we have
to make the trigger safe when the service is scaled out.

## Decision drivers

1. FR-8: the move happens automatically within about five minutes, configurable
   rather than hard-coded.
2. NFR-0001 horizontal scalability: each eligible order moves once even when
   more than one instance runs.
3. NFR-0001 testability: the move is exercised directly, without waiting on
   real time.
4. A missed run after a crash or restart must recover; the service must not
   depend on a process staying up.
5. The third-party scheduler dependency should not lock the processing rules
   into the scheduler, so the rules can be tested and swapped.
6. The operation must be visible: failures and runs should be inspectable
   rather than lost in logs.

## Options considered

### Option 1: in-process timer

A background hosted service loops on a delay and moves pending orders.

- Good: no extra dependency, and it is simple to write.
- Bad: the schedule is in process memory, so a restart loses it and two
  instances both run it. A missed run after a crash is not recovered. It fails
  drivers 2 and 4, and it is the classic place for business logic to leak into
  the scheduler loop, which fails driver 5.

### Option 2: persistent scheduled-job library as an adapter over an application service

A persistent scheduler, Hangfire in this stack, holds a recurring job on a cron
expression in PostgreSQL. The job handler is a thin adapter that resolves a
scoped `IOrderProcessingService` and calls `ProcessPendingOrdersAsync`. The
eligibility rule and the transition live in the application service and the
domain, not in the job.

- Good: the recurring job is persisted, so a restart reschedules it; Hangfire
  acquires a distributed lock per recurring job, so only one instance runs a
  given tick, which meets driver 2 at the scheduler level; retries and a
  dashboard come built in, meeting driver 6; and because the adapter only
  delegates, the processing logic is directly testable, meeting drivers 3 and 5.
- Bad: one storage package and a schema in PostgreSQL that the service now
  depends on, and the scheduler's behavior is another thing to learn and
  configure.

### Option 3: external queue

A broker holds a message per pending order, and workers consume it.

- Good: scales independently and gives strong at-least-once delivery with
  visibility.
- Bad: a broker is infrastructure this change does not need, and the change
  is a periodic sweep, not a stream of events. It fails driver 5 with more
  moving parts than the requirement justifies.

## Decision

Option 2. Hangfire is the scheduler, configured with a cron expression read
from configuration (default every five minutes) and a bounded batch size. The
processing logic is `IOrderProcessingService.ProcessPendingOrdersAsync`, an
application service with no scheduler dependency. The recurring job handler is
an adapter that creates a scope and delegates.

Multi-instance safety is defense in depth. The scheduler's distributed lock
prevents two instances running the same recurring job at the same time, and
the optimistic concurrency token from ADR-0002 prevents two overlapping runs
from both applying a change to the same order. Either alone would be
insufficient: the lock does not survive a run that outlives its lease, and the
token alone would let each instance do wasted work. Together they meet the
at-most-once requirement and keep the API stateless.

## Consequences

The service can run as more than one instance without a shared in-process
state, and the automatic move stays testable by calling the application service
directly. The recurring job recovered after a crash is a scheduler concern
rather than application code.

The cost is a scheduler dependency and its schema in the data store. The
adapter boundary means the scheduler can be replaced by a hosted service or a
queue consumer without touching the processing logic, which is the escape hatch
if the dependency proves thorny. Batch size and the cron expression are
configuration, not constants.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
