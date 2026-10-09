---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: TP-0002
artifact_type: test-plan
title: "Test plan for the order processing backend"
status: approved
stage: S5
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "QA Lead", date: 2026-10-09 }
gate: G5
sources: [PRD-0001, NFR-0001, PLAN-0002, TP-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Test plan for the order processing backend

This is the S5 test plan for the order processing backend. It takes the S3 test
strategy in TP-0001 and states the levels, environments, data, and coverage we
use to verify the stories and the non-functional budgets before release. It is a
draft for the QA Lead at G5 and no approval is implied by this document.

## Scope

In scope: the eleven stories PRD-0001-S01 to PRD-0001-S11, the thirteen budget
rows in NFR-0001, and the task breakdown in PLAN-0002 that assigns the build and
review work. That covers order creation and its invariants, the derived total,
retrieval, listing and the result bound, the status lifecycle, cancellation, the
automatic move, same-status repeats, concurrent changes, observability,
multi-instance safety, the interface contract and error shape, and startup and
readiness.

Out of scope: payments, refunds, returns, inventory and stock, the product
catalogue, customer accounts and authentication, notifications, delivery
tracking beyond SHIPPED and DELIVERED, reporting, and multi-tenant or
multi-region operation. These are non-goals in PRD-0001 and no story delivers
them. There is no regulatory matrix, so no compliance-evidence mapping runs at
this level.

## Test levels

### Domain unit tests

Tests over the Domain module with no database, no HTTP, and no scheduler. They
cover order construction and its invariants, the derived total and the rounding
rule, every allowed and rejected transition, the terminal states, and the cancel
rule. They are the primary proof for the lifecycle and the money rule because
the domain is framework free and directly callable.

### Architecture tests

Tests over the built assemblies with NetArchTest. They assert the dependency
direction recorded in DOC-0001: the domain depends on no other layer and no ORM,
the application does not depend on infrastructure, the API, or the ORM,
infrastructure does not depend on the API, controllers do not reach into
persistence, and the application interfaces are implemented in infrastructure.
These tests are the mechanical guard that the layering in ADR-0001 and ADR-0008
holds rather than being assumed.

### Integration tests

Tests over the API and infrastructure against a real PostgreSQL 18 container
started by Testcontainers. Each class boots the application with the test host,
applies the EF Core migrations to a fresh database, and exercises the endpoints,
the store checks, and the failure paths. The same container holds the Hangfire
schema, so scheduler storage is exercised beside the order store. The automatic
move is exercised by resolving IOrderProcessingService and calling
ProcessPendingOrdersAsync directly; no test waits on the five-minute schedule.

### Contract checks

The OpenAPI document at openapi/order-processing.yaml is validated in CI, so an
invalid or drifted contract fails the build. Contract checks drive the running
service for every operation and assert the response shape, the status codes, the
shared error shape from ADR-0005, and the correlation identifier. These run as a
CI validation step beside the test projects and are not counted as test methods.

## Environments and data

- Unit and architecture tests need no external dependency.
- Integration and contract tests run against a pinned postgres:18-alpine
  container through Testcontainers. The Hangfire schema is created in the same
  container.
- Tests seed deterministic orders with fixed ids and fixed timestamps, reset the
  database between test classes, and never depend on wall-clock time or on test
  execution order.
- The data set holds no personal data, matching the residency budget. The same
  seed shape is reused across levels.
- The build target is .NET 10 with EF Core 10 and Npgsql. The pinned database
  image tag is postgres:18-alpine.
- The default run is one command and completes in under ten minutes. No test
  waits on real time for more than five seconds, per the testability budget.

## Entry and exit criteria

Entry:

- G4 is approved and the change is merged.
- The test projects build, the database image tag is pinned, and the container
  runtime is available.
- The stories and their acceptance criteria are baselined at G2 and the design
  is frozen at G3.

Exit:

- The automated suite passes in one command and completes in under ten minutes,
  with no test waiting more than five seconds on real time.
- Every story and every budget row below has a passing test, or a recorded
  manual result with the reason stated.
- The OpenAPI document validates and the contract checks pass for every
  operation.
- Zero critical or high defects are open.
- Each budget is met, or a documented waiver is approved by the Architect and
  the Engineering Manager.
- A flaky test is quarantined with a tracking item, never deleted.

## Coverage by story

Each row names the story, the requirement it delivers, the level that proves it,
and what that level checks.

| Story | Requirement | Level | What the level checks |
|---|---|---|---|
| PRD-0001-S01 | FR-1, FR-2, FR-3 | Domain unit, integration | Create with one item and with several items, each rejection case, and the derived total |
| PRD-0001-S02 | FR-4 | Integration, contract | Retrieve by known id, unknown id, and malformed id; retrieval does not change the order |
| PRD-0001-S03 | FR-5, list result bound | Integration | No filter, one filter, empty result, stable order, default bound, maximum bound, and clamp |
| PRD-0001-S04 | FR-6 | Domain unit, integration | PROCESSING to SHIPPED and SHIPPED to DELIVERED accepted; every other change rejected |
| PRD-0001-S05 | FR-7 | Domain unit, integration | Cancel PENDING; reject cancel after processing; cancellation applies to the whole order |
| PRD-0001-S06 | FR-8 | Integration, service direct | PENDING becomes PROCESSING; a CANCELLED order is untouched; a second run changes nothing |
| PRD-0001-S07 | FR-9, FR-10 | Domain unit, integration | Same-status no-op and the stale-writer conflict |
| PRD-0001-S08 | Observability | Implementation only, assertions owed | The per-transition log line, the counter, and the correlation identifier are implemented; no automated test asserts them, so the assertions are owed debt in DOC-0005. Liveness and readiness are wired to the database check; the 200 ms timing assertion is owed |
| PRD-0001-S09 | Horizontal scalability | Integration | Two instances against one store move a PENDING order exactly once |
| PRD-0001-S10 | Interface contract and error model | Integration, contract | Error-path status codes and the shared error shape; contract validation in CI |
| PRD-0001-S11 | Operability | Integration, manual | Migrations apply on startup and readiness reflects the store; the clean-machine start is manual |

## Coverage by NFR budget

Each row names a budget in NFR-0001, the level that verifies it, whether it runs
in the default suite, and the note.

| Budget | Level | In the default run | Note |
|---|---|---|---|
| Peak request volume | Performance | No | The 30-minute sustained run is a separate scheduled run against the release candidate |
| Read and list latency | Performance | No | The same separate run reports p95 and p99 at the assumed peak |
| Write latency | Performance | No | The same separate run reports p95 and p99 at the assumed peak |
| Availability | Manual review | No | A monthly aggregate of recorded health and error data; no single run measures it |
| Automatic-transition timeliness | Integration, manual sample | Partly | Correctness is automated by calling the service directly; the real-time sample is manual |
| Correctness under concurrent changes | Integration (concurrency) | Yes | Two competing writes on one order; exactly one succeeds and the other conflicts |
| Testability | CI timing check | Yes | One command, under ten minutes, no wait over five seconds |
| Observability | Implementation only, assertions owed | No | The log line, the counter, and the correlation identifier are implemented; no automated test asserts them, so the assertions are owed debt in DOC-0005. The health signals are wired; the 200 ms timing assertion is owed |
| Horizontal scalability | Integration | Yes | Two instances against one store move each eligible order once; the instance-local-state review is manual |
| Interface contract and error model | Contract, integration | Yes | The contract validates in CI; error-path tests assert validation, not-found, and conflict |
| Operability | Integration, manual | Partly | Migrations and readiness run in integration; the clean-machine one-command start is manual |
| List result bound | Integration | Yes | Default bound, maximum, clamp, and ordering |
| Data residency | Manual review | No | A data inventory and deployment review; no personal data and one region |

## Manual checks

Anything not automated is listed here with its reason, as the process requires.

| Check | Reason it is manual |
|---|---|
| The load and latency test (peak volume, read and list latency, write latency) | The 30-minute sustained run cannot fit inside the ten-minute default suite. It is a separate scheduled run against the release candidate. |
| The monthly availability review | Availability is an aggregate of recorded runtime data over a month, which no single test run can measure. |
| The sampled end-to-end automatic-transition timing check (PRD-0001-S06) | It requires real elapsed time of up to about five minutes. Correctness is automated; this sample confirms the schedule itself. |
| The clean-machine one-command start (PRD-0001-S11) | It depends on a clean host and a real container runtime rather than the test host. The steps are scripted, but a human runs it and records the result. |
| The data residency review | It is a review of the data inventory and the deployment region, not a code path. |
| The instance-local-state review (PRD-0001-S09) | It is a code and deployment review that no request depends on state held in one instance. The two-instance test covers the observable behavior. |

## The automatic move

The move is proved without the clock. The integration tests resolve
IOrderProcessingService and call ProcessPendingOrdersAsync directly, then assert
that a PENDING order becomes PROCESSING, that an order past PENDING is untouched,
and that a second run changes nothing. No test sleeps for five minutes. The
scheduled timing is confirmed separately by the manual sample above, which runs
against the release candidate and is not part of the default suite.

## Traceability

Each test is named for the story or budget it verifies, so the chain from
PRD-0001 through the stories to DOC-0005 stays mechanical. The design this plan
tests is in DOC-0001, DOC-0002, and DOC-0003, and the contract is
openapi/order-processing.yaml. The results against this plan are reported in
DOC-0005.
