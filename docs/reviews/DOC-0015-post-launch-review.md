---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0015
artifact_type: review
title: "Post-launch review of the order processing backend"
status: approved
stage: S8
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
# Approver is a placeholder for the PM at review; this draft records no approval and no date.
approver: { kind: human, name: "Komal Bahetwar", role: "PM + EM", date: 2026-10-09 }
gate: G7
created: 2026-10-09
updated: 2026-10-09
sources: [BRIEF-0001, PRD-0001, DOC-0005, DOC-0007, DOC-0008]
jurisdiction: []
superseded_by: null
---

# Post-launch review of the order processing backend

This is the S8 post-launch review, the A8.1 artifact for the G7 packet. It
scores the outcome against the success criteria in BRIEF-0001, records the
decisions taken since, and recommends whether the change is closed, maintained,
or retired.

Read this with one fact in front: this is a take-home assignment, not a
production service. There is no production traffic, no customer adoption, and
no live telemetry. The evidence we have is the 45-test run reported in DOC-0005
and the local Docker Compose deployment verified in DOC-0008. Where a criterion
needs real world adoption to score, we say so rather than borrow a number. The
success criteria in BRIEF-0001 are written about behavior, so most of them can
be scored against the test evidence; none of them should be read as a claim
about production usage.

## Outcome against the brief's success criteria

BRIEF-0001 states six success criteria. Each is marked met or missed below,
with the reason and the evidence.

| # | Success criterion (BRIEF-0001) | Result | Reason and evidence |
|---|---|---|---|
| 1 | An order created with two or more items is stored once and read back with all of its items and its total intact | Met | Create tests cover one item and several distinct items; the total is derived from the items and read-back total equals the sum. Evidence in DOC-0005, domain and endpoint areas. |
| 2 | An order can be retrieved by id, and an unknown id returns a not-found result | Met | Retrieve tests cover known, unknown, and malformed ids; unknown returns not found with a stable code. Evidence in DOC-0005. |
| 3 | Listing returns every order, and returns only the matching orders when a status filter is applied | Met | List tests cover no filter, one filter, an empty match, and the stable oldest-first order. Evidence in DOC-0005. |
| 4 | An order begins in PENDING, reaches PROCESSING, then SHIPPED, then DELIVERED, and no other status change is accepted | Met | Domain unit tests and endpoint tests cover the accepted transitions, the rejected ones, and the terminal guards. Evidence in DOC-0005. |
| 5 | A PENDING order can be cancelled, and a cancel attempt on an order in any later status is rejected | Met | Cancellation tests cover the pending cancel, the later-status rejections, and whole-order cancellation. Evidence in DOC-0005. |
| 6 | Every PENDING order reaches PROCESSING within about five minutes of becoming eligible, with no manual step, and orders that have already left PENDING are not affected | Met for correctness, timing not measured | The automatic move and idempotency tests confirm a PENDING order becomes PROCESSING, a cancelled order is untouched, and a repeated run changes nothing. Evidence in DOC-0005. The real-time five-minute sample is a separate manual check and did not run here, so the timing half is a target to monitor, not a measured result. |

The timing half of criterion 6 is the one honest gap in the scoring. The
behavior is proved; the wall-clock bound is not. DOC-0012 records it as a target
to monitor, and the measurement is tracked in DOC-0017.

## Outcome against the PRD success criteria

PRD-0001 restates the brief's criteria and adds the concurrency and same-status
cases. Those map onto the same evidence and are met:

- Same-status request is accepted with no change: covered by the no-op test.
- Two racing changes: exactly one applies and the other reports a conflict:
  covered by the stale-writer conflict test and the batch-continues test.
- Listing order is stable across repeated requests: covered by the list tests.

The full functional set FR-1 to FR-10 passed UAT, signed on 2026-10-09 in
DOC-0007 against the evidence in DOC-0005.

## What the evidence is, and what it is not

The evidence is:

- The 45-test run on 2026-10-09: 19 domain unit tests, 5 architecture tests, and
  21 integration tests against a real PostgreSQL 18 container.
- The local Docker Compose deployment run on 2026-10-09: the database reports
  healthy, the API reports ready, migrations apply on startup, and a created
  order reads back with its status and total.

The evidence is not production adoption. There are no real users, no traffic of
any kind, no SLO data, and no incident history. Any claim about how the service
behaves under load, or how often a conflict happens in practice, is unmeasured.
The four performance and availability budgets are waived at G5, and the waiver
is recorded in DOC-0005 and DOC-0008.

## Open questions

BRIEF-0001 carried four open questions, all closed at G1 in OQ-0001. Two more
were added at G1 and both are now closed by the S3 design:

- When an order repeats the same product on more than one line, are the lines
  kept separate or combined? Closed: kept separate, recorded in ADR-0007.
- What precision and rounding apply to item unit prices and order totals?
  Closed: fixed-scale decimal, `numeric(18,2)` in the store, half-up rounding at
  the line, recorded in ADR-0004.

No question from the requirements baseline remains open. The enforcement of who
may perform each lifecycle action is named in the PRD and deliberately out of
scope; the design records where a policy would attach, and that stays a
documented deferral rather than an open question.

## Work deferred

The extensions kept out of this baseline are listed in FUTURE_PHASES.md with
the trigger that would start each one. Nothing there is committed scope. The
first functional example is item-level order status, so an order with several
products could carry status per item or per fulfilment unit rather than only per
order. Other deferred items include: a product catalogue so the price is not
caller-supplied; tax and discounts in the total; richer listing and search;
partial cancellation; notifications on status change; idempotency keys for
repeated submissions; contract versioning and rate limiting; distributed trace
export plus the SLO dashboards and alerts; blue/green or rolling deployment
with backups and disaster recovery; read replicas and caching; and multi-region
operation.

The operational half of that list is the part most tied to running the service
for real. Until the service runs, the SLO documents in DOC-0012 are targets and
the alert rules in DOC-0013 are undeployed config.

## Learnings

- The domain-first design paid off. The lifecycle rules live in the entity and
  the tests prove them without a database, which is why the concurrency and
  cancel rules are cheap to verify.
- The one blocker and four majors found in the S4 review came from contract and
  batch-handling mistakes, not from the domain. Contract validation in CI and a
  service-level batch assertion are the controls that would have caught them
  earlier; the assertion is still owed and is in DOC-0017.
- The performance budgets were never grounded in real traffic. Writing them as
  launch assumptions was honest, but it means the first real telemetry should
  revise them rather than treat them as met.
- The debt around observability assertions and the missing dependency scan is
  process debt, not product debt, and it should clear before the service is
  operated for real.

## Recommendation

Close the change at G7 and keep it in a maintain state. There is no case for
retirement: the service is the dependable order record the brief asked for, and
nothing in the evidence says it should be withdrawn. There is also no case for
declaring it production-ready: the performance and availability budgets are
unmeasured, the observability assertions are owed, and the dependency scan and
SBOM are missing. The right disposition is closure with the debt register in
DOC-0017 owned and triggered, and with the future phases in FUTURE_PHASES.md as
the backlog the next real traffic would pull from.

The debt delta is DOC-0017 and the telemetry position is DOC-0016. Together with
this review they form the G7 packet for joint PM and EM approval. This document
records no approval; the PM and EM record the decision.
