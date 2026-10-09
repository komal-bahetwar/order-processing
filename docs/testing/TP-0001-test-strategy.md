---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: TP-0001
artifact_type: test-plan
title: "Test strategy for the order processing service"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001, PRD-0001-S01, PRD-0001-S02, PRD-0001-S03, PRD-0001-S04, PRD-0001-S05, PRD-0001-S06, PRD-0001-S07, PRD-0001-S08, PRD-0001-S09, PRD-0001-S10, PRD-0001-S11]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Test strategy for the order processing service

This plan says what we test, at which level, and how each acceptance criterion
and non-functional budget is verified. It is the S3 test strategy that feeds
the S5 test plan and report. The stack under test is .NET 10, ASP.NET Core,
EF Core 10 with Npgsql, PostgreSQL 18, and Hangfire. Integration tests use a
real PostgreSQL 18 container through Testcontainers.

## Scope

In scope: every acceptance criterion in PRD-0001-S01 through PRD-0001-S11, the
domain invariants, the persistence behavior against a real database, the
interface contract, the concurrency correctness, the observability budget, and
the operational startup path. Out of scope: payments, catalogues, notifications,
and any behavior PRD-0001 lists as a non-goal. There is no regulatory matrix,
so no compliance-evidence mapping is required.

The suite must run in one command and complete in under ten minutes, and no test
may wait on real time for more than five seconds, per the testability budget in
NFR-0001. That rule shapes the levels below: the automatic move is tested by
calling the application service directly, never by waiting five minutes.

## Test levels

### Domain unit tests

Fast tests over the Domain module with no database, no HTTP, and no scheduler.
They cover order construction and its invariants, the derived total and the
rounding rule, every allowed and rejected transition, the terminal states, and
the cancel rule. They are the primary proof for the lifecycle and the money
rule because the domain is framework free and directly callable.

### Integration tests

Tests over the Api and Infrastructure modules against a real PostgreSQL 18
container, the one the service actually runs on. They boot the application with
the test host, apply migrations to a fresh database, and exercise each endpoint
and each error path. They also assert the store-level checks and that a
read-back total equals the sum of the item amounts. The automatic move is
exercised by resolving `IOrderProcessingService` and calling
`ProcessPendingOrdersAsync` directly.

### Contract tests

The OpenAPI document is validated in CI, so an invalid or drifted contract
fails the build. Contract tests then drive the running service for every
operation and assert the response shape, the status codes, the shared error
shape, and the correlation identifier. The contract is the unit under test as
much as the service is.

### Concurrency test

The test that proves FR-10 and the correctness under concurrent changes budget.
Two competing writes load one order at the same starting `version`, apply
different changes, and save. The test asserts that exactly one write succeeds,
that the other reports a conflict, and that the order's `version` advances by
exactly one. The central case is a customer cancel racing the automatic move on
a PENDING order: the order must end in exactly one of CANCELLED or PROCESSING,
never both. A two-instance run against one database extends the same test to
the horizontal scalability budget.

### Observability tests

Integration tests assert that every lifecycle transition emits exactly one
structured log line carrying the order id and the old and new status, and one
transition counter, using an in-memory log sink and meter. They assert the
correlation identifier on every log line and every error response, and that
each health signal responds within its budget.

## Acceptance criteria to test mapping

Each row names the story, the requirement the criterion verifies, and the
levels that prove it. "Unit" is the domain unit suite; "Integration" is the
real-database endpoint suite; "Contract" is the contract suite; "Concurrency"
is the race suite.

| Story | Requirement | Criterion | Level |
|---|---|---|---|
| PRD-0001-S01 | FR-1 | One item is captured and the order starts in PENDING | Unit, integration |
| PRD-0001-S01 | FR-1 | Several distinct items are all captured | Unit, integration |
| PRD-0001-S01 | FR-2 | No items is rejected and nothing is captured | Unit, integration |
| PRD-0001-S01 | FR-2 | Quantity of zero or less is rejected and nothing is captured | Unit, integration |
| PRD-0001-S01 | FR-2 | Negative unit price is rejected and nothing is captured | Unit, integration |
| PRD-0001-S01 | FR-3 | The total equals the sum of quantity times unit price | Unit, integration |
| PRD-0001-S01 | FR-3 | A supplied total has no effect and is ignored | Contract, integration |
| PRD-0001-S02 | FR-4 | A known id returns the order, items, total, and status | Integration |
| PRD-0001-S02 | FR-4 | An unknown id returns a not-found outcome | Integration |
| PRD-0001-S02 | FR-4 | Not-found and success are distinguishable | Integration, contract |
| PRD-0001-S02 | FR-4 | Retrieval does not change the order | Integration |
| PRD-0001-S03 | FR-5 | No filter returns every order | Integration |
| PRD-0001-S03 | FR-5 | One filter returns exactly that status | Integration |
| PRD-0001-S03 | FR-5 | A filter that matches nothing returns an empty list | Integration |
| PRD-0001-S03 | FR-5 | Repeated reads over unchanged data return the same sequence | Integration |
| PRD-0001-S03 | NFR-0001 | The default bound is at most 20 | Integration |
| PRD-0001-S03 | NFR-0001 | A requested bound is at most 100 | Integration |
| PRD-0001-S03 | NFR-0001 | A request for more than 100 returns 200 with at most 100 results | Integration |
| PRD-0001-S03 | NFR-0001 | The results are ordered by creation time | Integration |
| PRD-0001-S04 | FR-6 | PROCESSING to SHIPPED is accepted | Unit, integration |
| PRD-0001-S04 | FR-6 | SHIPPED to DELIVERED is accepted | Unit, integration |
| PRD-0001-S04 | FR-6 | PENDING to SHIPPED or DELIVERED is rejected and unchanged | Unit, integration |
| PRD-0001-S04 | FR-6 | A manual change to PROCESSING from PENDING, or to PENDING or CANCELLED, is rejected with a conflict | Unit, integration |
| PRD-0001-S04 | FR-6 | PROCESSING to any other status is rejected | Unit |
| PRD-0001-S04 | FR-6 | SHIPPED to any other status is rejected | Unit |
| PRD-0001-S04 | FR-6 | DELIVERED or CANCELLED to any other status is rejected | Unit |
| PRD-0001-S04 | FR-6 | An order holds exactly one status at a time | Unit |
| PRD-0001-S05 | FR-7 | PENDING cancels to CANCELLED | Unit, integration |
| PRD-0001-S05 | FR-7 | PROCESSING, SHIPPED, or DELIVERED cannot be cancelled | Unit, integration |
| PRD-0001-S05 | FR-7 | An already CANCELLED order cannot be cancelled again | Unit |
| PRD-0001-S05 | FR-7 | Cancellation applies to the whole order | Unit, integration |
| PRD-0001-S06 | FR-8 | A PENDING order becomes PROCESSING automatically | Integration |
| PRD-0001-S06 | FR-8 | An order that has left PENDING is not affected | Integration |
| PRD-0001-S06 | FR-8 | A CANCELLED order is not affected | Integration |
| PRD-0001-S06 | FR-8 | No PENDING orders means no change | Integration |
| PRD-0001-S06 | FR-8 | A second run over unchanged orders changes nothing | Integration |
| PRD-0001-S07 | FR-9 | A same-status request is a no-op for any status | Unit, integration |
| PRD-0001-S07 | FR-9 | A repeated identical request has the same result | Integration |
| PRD-0001-S07 | FR-10 | Two simultaneous changes apply at most one change | Concurrency |
| PRD-0001-S07 | FR-10 | The change that does not win reports a conflict | Concurrency |
| PRD-0001-S07 | FR-10 | Cancel racing the automatic move leaves exactly one outcome | Concurrency |
| PRD-0001-S08 | NFR-0001 | One log line per transition with order id and both statuses | Integration |
| PRD-0001-S08 | NFR-0001 | One counter per transition | Integration |
| PRD-0001-S08 | NFR-0001 | A correlation identifier on every log line and error response | Integration, contract |
| PRD-0001-S08 | NFR-0001 | Liveness responds within 200 ms | Integration |
| PRD-0001-S08 | NFR-0001 | Readiness responds within 200 ms | Integration |
| PRD-0001-S09 | NFR-0001 | Two instances against one store move each eligible order once | Integration, concurrency |
| PRD-0001-S09 | NFR-0001 | A status change racing the move applies at most one change | Concurrency |
| PRD-0001-S09 | NFR-0001 | No request depends on instance-local state | Integration, review |
| PRD-0001-S10 | NFR-0001 | One contract describes every operation and validates in CI | Contract |
| PRD-0001-S10 | NFR-0001 | Every failure uses the one error shape with a stable code | Integration, contract |
| PRD-0001-S10 | NFR-0001 | Contract tests confirm conformance for every operation | Contract |
| PRD-0001-S11 | NFR-0001 | One command starts the service and its data store | Manual |
| PRD-0001-S11 | NFR-0001 | Required schema changes apply on startup with no manual step | Integration |
| PRD-0001-S11 | NFR-0001 | Readiness reports ready only after the store is available | Integration |

## NFR budget verification

The table maps each budget in NFR-0001 to how it is verified and whether that
verification is automated in the default suite.

| Budget | Verification | In the default suite |
|---|---|---|
| Peak request volume and burst | A load test drives 10 requests per second sustained and 25 per second for 60 seconds, for 30 minutes, and reports error rate and latency | No, a separate scheduled run |
| Read and list latency | The load test reports p95 and p99 at the assumed peak | No, a separate scheduled run |
| Write latency | The load test reports p95 and p99 at the assumed peak | No, a separate scheduled run |
| Availability | A monthly review of health and error data against the 99.5 percent target | No, manual |
| Automatic-transition timeliness | A controlled integration test exercises eligibility without waiting, plus a sampled timing check on the release candidate | Partly; the sampled end-to-end check is manual |
| Correctness under concurrent changes | The concurrency test loads one order at a starting `version`, submits two competing writes, and asserts exactly one succeeds and the other conflicts | Yes |
| Testability | The CI run records the suite duration and confirms the wait bound by reviewing test timings | Yes |
| Observability | Integration tests assert one log line and one counter per transition, a correlation identifier on every log line and error response, and each health signal within 200 ms | Yes |
| Horizontal scalability | The two-instance test against one store, plus a review that no request depends on instance-local state | Yes; the review is manual |
| Interface contract and error model | Contract lint and validation in CI, plus contract and error-path tests | Yes |
| Operability | A clean-environment run of the documented start command followed by a readiness check | No, manual |
| List result bound | Integration tests assert the default bound, the maximum, the clamping of an over-maximum request, and the ordering | Yes |
| Data residency | A data inventory and a deployment review confirm the single region and the absence of personal data | No, manual |

## Manual tests

Anything not automated is listed here with the reason, as the process requires.

| Test | Reason it is manual |
|---|---|
| The clean-machine one-command start (PRD-0001-S11) | It depends on a clean host and a real container runtime rather than the test host. The steps are scripted so the run is repeatable, but a human runs it and records the result. |
| The sampled end-to-end automatic-transition timing check (PRD-0001-S06) | It requires real elapsed time of up to about five minutes. The correctness is covered automatically by the direct service test; this sample confirms the schedule itself. |
| The monthly availability review (NFR-0001) | Availability is an aggregate of recorded runtime data over a month, which no single test run can measure. |
| The data residency review (NFR-0001) | It is a review of the data inventory and the deployment region, not a code path. |
| The instance-local-state review (PRD-0001-S09) | It is a code and deployment review that no request already depends on state held in one instance. The two-instance test covers the observable behavior. |
| The load and latency test (NFR-0001) | The 30-minute sustained run cannot fit inside the ten-minute default suite. It is a separate scheduled run against the release candidate. |

## Environments and data

- Unit tests need no external dependency.
- Integration and contract tests run against a pinned PostgreSQL 18 container
  started by Testcontainers. The Hangfire schema is created in the same
  container, so the scheduler storage is exercised alongside the order store.
- Tests seed deterministic orders with fixed ids and fixed timestamps, reset
  the database between test classes, and never depend on wall-clock time or on
  the order of test execution.
- The data set contains no personal data, matching the residency budget. The
  same seed data is reusable across levels.
- The load test uses the same container and a separate configuration; it is not
  part of the default command.

## Entry and exit criteria

Entry:

- G3 is approved and the design set, the contract, and this strategy are frozen.
- The test projects build, the database image is pinned, and the container
  runtime is available.
- The NFR budgets are known and the acceptance criteria are traced.

Exit:

- The full automated suite passes in one command and completes in under ten
  minutes, with no test waiting more than five seconds on real time.
- Every acceptance criterion in the mapping table has a passing automated test,
  or a recorded manual result with the reason above.
- The OpenAPI document validates and the contract tests pass for every
  operation.
- Zero critical or high defects are open.
- Each NFR budget is met, or a documented waiver is approved by the Architect
  and the Engineering Manager.
- A flaky test is quarantined with a tracking item, never deleted.

## Traceability

Each test is named for the story and requirement it verifies, so the chain from
PRD-0001 through the stories to the test report stays mechanical. The design
this strategy tests is in DOC-0001, DOC-0002, and DOC-0003, and the contract is
`openapi/order-processing.yaml`.
