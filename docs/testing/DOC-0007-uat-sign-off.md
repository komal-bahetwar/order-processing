---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0007
artifact_type: evidence
title: "UAT sign-off for the order processing backend"
status: approved
stage: S5
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G5
sources: [PRD-0001, TP-0002, DOC-0005]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# UAT sign-off for the order processing backend

This is the draft UAT sign-off for the order processing backend. It maps each
functional requirement in PRD-0001 and each non-functional budget the PRD names
as touched to the observed behavior that satisfies it, citing the test report
DOC-0005. It is written for the PM to review and sign at G5. The signature line
is blank, and no agent has signed this document.

## Functional checklist

| Requirement | Expected behavior | Observed evidence in DOC-0005 | Result |
|---|---|---|---|
| FR-1 | An order with one item and an order with several distinct items are both accepted and start in PENDING | Create tests in the domain unit and endpoint areas | Pass |
| FR-2 | An order with no items, a quantity of zero or less, or a negative unit price is rejected and nothing is captured | Creation rejection tests in the domain unit and endpoint areas | Pass |
| FR-3 | The total equals the sum of quantity times unit price, and a caller-supplied total is ignored | Total derivation and rounding tests plus the create endpoint | Pass |
| FR-4 | Retrieval returns the order, its items, its total, and its status; an unknown id returns not found | Retrieve endpoint tests for known, unknown, and malformed ids | Pass |
| FR-5 | Listing returns every order, a status filter returns exactly that status, an empty match returns an empty list, and the order is stable oldest first | List endpoint tests for no filter, one filter, empty result, and stable order | Pass |
| FR-6 | PROCESSING to SHIPPED and SHIPPED to DELIVERED are accepted, every other change is rejected and leaves the order unchanged, and an order holds one status | Status lifecycle tests in the domain unit and endpoint areas | Pass |
| FR-7 | A PENDING order cancels, and a cancel of an order in any later status is rejected; cancellation applies to the whole order | Cancellation tests in the domain unit and endpoint areas | Pass |
| FR-8 | A PENDING order becomes PROCESSING automatically within about five minutes with no manual step, and an order past PENDING is unaffected | Automatic move and idempotency tests; the real-time timing sample is a separate manual check | Pass, timing sample pending |
| FR-9 | A request to set the status an order already holds is accepted, changes nothing, and produces no additional change | Same-status no-op test in the endpoint area | Pass |
| FR-10 | When two changes race on one order, at most one applies, the loser reports a conflict, and the order never reflects both | Stale-writer conflict and batch-continues tests | Pass |

## Non-functional budgets touched

PRD-0001 names the budgets this change touches. Each row below gives the observed
evidence or the reason it is a separate check.

| Budget | Observed evidence | Status |
|---|---|---|
| Volume at launch scale | The load test is a separate scheduled run against the release candidate | Waived (approved by Architect+EM, 2026-10-09) |
| Read and list latency | The same load test reports p95 and p99 at the assumed peak | Waived (approved by Architect+EM, 2026-10-09) |
| Write latency | The same load test reports p95 and p99 at the assumed peak | Waived (approved by Architect+EM, 2026-10-09) |
| Availability | A monthly review of recorded health and error data against 99.5 percent | Waived (approved by Architect+EM, 2026-10-09) |
| Automatic-transition timeliness | Correctness is automated; the real-time sample confirms the schedule | Pass for correctness, sample pending |
| Correctness under concurrent changes | The concurrency tests apply at most one of two racing changes | Pass |
| Testability | One command, under ten minutes, and no wait over five seconds | Pass |
| Observability | The per-transition log line, the counter, and the correlation identifier are present; no automated test asserts them, and the 200 ms health assertion is owed | Implemented; automated assertions owed |
| Horizontal scalability | The two-instance test moves each eligible order once; the instance-local-state review is a manual check that is not yet recorded | Check owed |
| Interface contract and error model | The contract validates in CI and the error paths return the shared shape | Pass |
| Operability | Migrations apply on startup and readiness reflects the store; the clean-machine command is a manual check | Pass with manual step pending |
| List result bound | The integration tests confirm the default bound, the maximum, the clamp, and the ordering | Pass |
| Data residency | The data set holds no personal data and the deployment is single region | Pass by design, review pending |

## Open items for the PM

- The load, latency, and availability budgets are not measured in this run. They
  are separate scheduled or monthly checks, and the QA Lead should confirm they
  are scheduled before G5 closes.
- The 200 ms health timing assertion and the field-level error-shape assertion
  remain owed, as listed in DOC-0005.
- The dependency-vulnerability scan and the SBOM are not wired in this
  environment and are recorded as debt in DOC-0006.

## Recommendation

We recommend the PM accept the functional behavior for UAT (FR-1 to FR-10 all
pass) and sign, on the understanding that the performance and availability
budgets are not measured in this run and are the subject of the waiver requested
in DOC-0005, and that the debt above is recorded for a later phase. The
functional outcome the PRD promises is demonstrated by the 45-test run in
DOC-0005.

## Signature

The PM accepted UAT on 2026-10-09.

| Field | Value |
|---|---|
| PM decision (accept or reject) | Accept |
| Name | Komal Bahetwar |
| Role | PM |
| Date | 2026-10-09 |
