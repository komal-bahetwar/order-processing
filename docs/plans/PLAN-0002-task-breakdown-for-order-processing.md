---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PLAN-0002
artifact_type: plan                  # planning-family type (no dedicated breakdown type)
title: "Task breakdown for order processing"
status: approved                     # draft | in_review | approved | baselined | superseded | retired
stage: S2                            # producing stage
work_item: TKT-0001
# kind: human | agent. Agents record model; a human author drops the model field.
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
# Added as executor->reviewer rounds run:
#   - { kind: agent, name: reviewer-agent, rounds: 2, verdict: approved }
#   - { kind: human, name: "Komal Bahetwar", role: "<...>" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 3, verdict: approved }
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 3, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "EM", date: 2026-10-09 }
gate: G2                             # gate this artifact is approved at (if any)
sources: [PRD-0001, PRD-0001-S01, PRD-0001-S02, PRD-0001-S03, PRD-0001-S04, PRD-0001-S05, PRD-0001-S06, PRD-0001-S07, PRD-0001-S08, PRD-0001-S09, PRD-0001-S10, PRD-0001-S11]
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Task breakdown: order processing

## Breakdown

Each task is one concrete implementation unit of at most two days. Task ids are local to this breakdown. Tests live with the story they verify, and the review tasks are the executor to reviewer passes the process requires.

### PRD-0001-S01: Create an order with one or more items

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S01-T1: Domain model for an order and its items, with the creation invariants (at least one item, quantity greater than zero, non-negative unit price) and the derived total | 1.0 | none | backend-engineer agent |
| S01-T2: Creation path that captures a valid order and rejects each invalid case with the rule that failed | 0.5 | S01-T1 | backend-engineer agent |
| S01-T3: Unit and integration tests for creation across the valid and invalid cases, and for the total derivation | 0.5 | S01-T1, S01-T2 | QA agent |

### PRD-0001-S02: Retrieve an order by id

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S02-T1: Retrieval path that returns an order, its items, its total, and its status, with a distinguishable not-found outcome | 0.5 | S01-T1 | backend-engineer agent |
| S02-T2: Integration tests for a known id, an unknown id, and the read-only guarantee | 0.5 | S02-T1 | QA agent |

### PRD-0001-S03: List and filter orders by status

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S03-T1: Listing path with the status filter, the oldest-first deterministic order, and the empty case | 0.5 | S01-T1 | backend-engineer agent |
| S03-T2: Integration tests for no filter, one filter, a filter that matches nothing, and repeated ordering | 0.5 | S03-T1 | QA agent |
| S03-T3: List bound, at most 20 by default and at most 100 on request, enforced over the creation-time order, with tests for the default bound, the maximum, and the order | 0.5 | S03-T1 | backend-engineer agent |

### PRD-0001-S04: Advance order status through the lifecycle

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S04-T1: Status rules for the accepted manual changes (PROCESSING to SHIPPED, SHIPPED to DELIVERED) and the rejection of every disallowed change | 1.0 | S01-T1 | backend-engineer agent |
| S04-T2: Status-change path that applies the rules and returns the updated order or the rejection | 0.5 | S04-T1 | backend-engineer agent |
| S04-T3: Unit and integration tests for accepted and rejected transitions | 0.5 | S04-T1, S04-T2 | QA agent |
| S04-T4: Review of the transition rules against the accepted-change table | 0.5 | S04-T3 | reviewer agent |

### PRD-0001-S05: Cancel a pending order

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S05-T1: Cancel rule and path, PENDING only and whole order, with the rejection cases | 0.5 | S01-T1 | backend-engineer agent |
| S05-T2: Unit and integration tests for cancel and its rejections | 0.5 | S05-T1 | QA agent |

### PRD-0001-S06: Move pending orders to processing automatically

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S06-T1: Processing routine that moves PENDING orders to PROCESSING and leaves every other order untouched | 0.5 | S01-T1 | backend-engineer agent |
| S06-T2: Automatic run at the about-five-minute interval, with the interval configurable | 0.5 | S06-T1 | backend-engineer agent |
| S06-T3: Integration tests for eligibility, the no-op case, and orders already past PENDING | 0.5 | S06-T1, S06-T2 | QA agent |

### PRD-0001-S07: Handle repeated and concurrent changes safely

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S07-T1: Same-status no-op for every status | 0.5 | S04-T2, S05-T1 | backend-engineer agent |
| S07-T2: Conflict handling so that at most one of two simultaneous changes is applied and the change that does not win reports a conflict | 1.0 | S04-T2, S05-T1, S06-T1 | backend-engineer agent |
| S07-T3: Concurrency tests for the cancel-versus-automatic-move race and the same-status repeat | 0.5 | S07-T1, S07-T2 | QA agent |
| S07-T4: Review of the conflict handling and the race coverage | 0.5 | S07-T3 | reviewer agent |

### PRD-0001-S08: Emit order lifecycle observability

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S08-T1: Structured log line for every order state change, carrying the order id and the old and new status | 0.5 | S04-T2, S05-T1, S06-T1 | backend-engineer agent |
| S08-T2: Counter metric for every transition | 0.5 | S08-T1 | backend-engineer agent |
| S08-T3: Liveness and readiness health signals that respond within 200 ms | 0.5 | S01-T1 | backend-engineer agent |
| S08-T4: Tests asserting one log line and one counter for every transition, that each health signal responds within 200 ms, and that every log line and every error response carries the correlation identifier | 1.0 | S08-T1, S08-T2, S08-T3, S08-T5 | QA agent |
| S08-T5: Correlation identifier produced for every request and propagated onto every log line and every error response, so a request ties to the change it caused | 1.0 | S08-T1 | backend-engineer agent |

### PRD-0001-S09: Run safely with more than one instance

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S09-T1: Review that no request path depends on state held in a single instance, and remove any such dependency | 1.0 | S01-T1 | backend-engineer agent |
| S09-T2: Single-apply automatic move when more than one instance runs, so each eligible order moves once | 1.0 | S06-T1 | backend-engineer agent |
| S09-T3: Two-instance test against one data store asserting each eligible order moves once and a status change racing the automatic move applies at most one change | 1.0 | S04-T2, S05-T1, S09-T2 | QA agent |
| S09-T4: Review confirming no request depends on instance-local state and the automatic move is single-apply across instances | 0.5 | S09-T1, S09-T3 | reviewer agent |

### PRD-0001-S10: Return a consistent contract and error model

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S10-T1: Machine-readable interface contract covering every operation, lint clean and validating in CI | 1.0 | S01-T2, S02-T1, S03-T1, S04-T2, S05-T1 | backend-engineer agent |
| S10-T2: One documented error shape with a stable code, a meaningful status, and the correlation identifier for every failure, applied at the edge | 1.0 | S01-T2, S04-T1, S05-T1 | backend-engineer agent |
| S10-T3: Contract tests confirming the service conforms to the contract for every operation | 1.0 | S10-T1 | QA agent |
| S10-T4: Error-path tests for validation, not-found, and conflict | 1.0 | S10-T2 | QA agent |

### PRD-0001-S11: Start the service with one command

| Task | Estimate (days) | Depends on | Assignee |
|---|---|---|---|
| S11-T1: One documented start command that brings up the service and its data store on a clean machine with no manual step | 1.0 | S01-T2 | backend-engineer agent |
| S11-T2: Required data-store changes applied on startup without a manual step | 1.0 | S01-T1, S11-T1 | backend-engineer agent |
| S11-T3: Readiness that reports ready only after the data store is available | 0.5 | S11-T2 | backend-engineer agent |
| S11-T4: Clean-environment test that runs the documented command and a readiness check | 0.5 | S11-T1, S11-T2, S11-T3 | QA agent |

## Dependency order

The build lands in this order. The domain model in S01-T1 blocks every other task, because everything else reads or changes an order.

1. S01-T1, the domain model, lands first. It blocks S01-T2, and through it S02, S03, S04, S05, S06, and the health signals in S08.
2. S01-T2 and S01-T3 complete create order. With S01-T1 in place, S02, S03, S04, S05, and the S06 routine can proceed in parallel within the single increment. S03-T3 lands after S03-T1, because the bound applies to the list path.
3. S04-T2, S05-T1, and S06-T1 land before S08-T1, because those are the state changes the log line observes. S05-T1 and S06-T1 also land before S07-T2, because those are the two changes that can race on a PENDING order.
4. S07-T1, S07-T2, and S07-T3 close the concurrency story last. The concurrency tests need every change path in place, so S07 is the integration point for the correctness requirement.
5. S08-T1 and S08-T2 emit the log line and the counter, S08-T3 adds the health signals, and S08-T5 produces the correlation identifier and propagates it onto every log line and every error response. S08-T4 tests presence, the 200 ms health response, and the correlation identifier on every log line and every error response once all of the above are in place.
6. The review tasks S04-T4, S07-T4, and S09-T4 run after their tests pass. They are the only blockers the story does not clear on its own.
7. S09 hardens the shared state once the change paths exist: S09-T1 removes instance-local state, S09-T2 makes the automatic move single-apply across instances, and S09-T3 proves both with two instances against one data store.
8. S10 publishes the contract and the error shape after the operations through S05 are stable, then S10-T3 and S10-T4 confirm conformance and the error paths.
9. S11 depends only on S01, so it can land as soon as creation exists. S11-T2 applies data-store changes on startup, S11-T3 gates readiness on the data store, and S11-T4 proves the whole command on a clean environment.
