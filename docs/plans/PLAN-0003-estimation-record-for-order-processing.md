---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PLAN-0003
artifact_type: plan                  # planning-family type (no dedicated estimate type)
title: "Estimation record for order processing"
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
sources: [PRD-0001, PLAN-0002]
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Estimation record: order processing

## Method

The unit is the day. Estimates are day-denominated expert judgment: each task in PLAN-0002 was sized on its own against the work it names, and a story estimate is the sum of its tasks. The sizes are for scope control and sequencing, not a prediction of agent-executed effort. The executor to reviewer agent loops execute the build, and runbook guidance says day-denominated estimates are wrong as effort predictions for agent-run work, often by an order of magnitude. We keep the day unit because it makes the scope reviewable and lets the plan note where a story is soft.

## Estimates

| Story | Estimate | Notes |
|---|---|---|
| PRD-0001-S01 | 2.0 days | Domain model, creation invariants, the derived total, and the creation tests. Soft until the duplicate-line and money-precision questions close. |
| PRD-0001-S02 | 1.0 day | Retrieval with a distinguishable not-found outcome and its tests. Small once S01 lands. |
| PRD-0001-S03 | 1.5 days | List, status filter, deterministic ordering, the empty case, and the result bound with its tests. The bound is the default 20 and the maximum 100, over the creation-time order. |
| PRD-0001-S04 | 2.5 days | The accepted-change rules, the rejection of every disallowed change, the status-change path, tests, and a reviewer pass. The largest story because it owns the lifecycle table. |
| PRD-0001-S05 | 1.0 day | Cancel rule and path plus tests. Small and self-contained. |
| PRD-0001-S06 | 1.5 days | The processing routine, the automatic run at the about-five-minute interval, and tests that avoid a real-time wait. |
| PRD-0001-S07 | 2.5 days | Same-status no-op, conflict handling, the concurrency tests, and a reviewer pass. The second rough estimate, because the conflict behavior firms up at S3. |
| PRD-0001-S08 | 3.5 days | The per-transition log line and counter, the correlation identifier on every log line and error response, the liveness and readiness signals, and the presence, 200 ms, and correlation-identifier tests. Behavior only; how the signals are produced and exposed, and how the correlation identifier is produced and propagated, are S3 decisions. |
| PRD-0001-S09 | 3.5 days | Independent request handling that lets more instances add throughput, the single-apply automatic move across instances, the two-instance test, and a reviewer pass. Soft until the S3 coordination design lands, because how a move applies once across instances is not yet decided. |
| PRD-0001-S10 | 4.0 days | The machine-readable contract, the one error shape with stable codes, contract tests, and error-path tests. The largest of the new stories because the contract covers every operation. |
| PRD-0001-S11 | 3.0 days | The one-command startup, the data-store changes on startup, readiness gated on the data store, and the clean-environment test. Small and independent of the change paths. |

Total: 26.0 days.

## Confidence

The estimates are least firm in several places.

- S01 and S03 depend on the duplicate-product-line question in OQ-0001, due 2026-10-16. If duplicate lines are combined rather than kept separate, the item handling and the derived total in S01 change, and the S03 tests gain a case. The move is small but it is real.
- S01 also depends on the money-precision question in OQ-0001, due 2026-10-23. The choice of precision and rounding affects how the total is computed and compared, and it can add a test case or two.
- S07 is soft because the conflict behavior is a design decision that lands at S3. The story fixes the outcome, that one of two simultaneous changes wins and the other reports a conflict, but how the losing change is detected and reported is not yet decided, so the estimate could move by half a day either way.
- S06 is soft on the automatic-run interval. The requirement is about five minutes, and the verification has to avoid waiting on real time. If that proves awkward, the test work grows slightly.
- S09 is soft because how more than one instance coordinates so a change applies once is a design decision that lands at S3. The estimate could move by half a day either way.
- S10 depends on the contract format and the error shape chosen at S3. The work is mechanical once those are fixed, but it touches every operation, so it is the largest of the new stories.
- S11 is firm against S01. Readiness may overlap the health-check work in S08, which can absorb a little of it.

No spike is needed. The two open questions are answerable from the PRD and the design context, and the dates above are before the S3 design work they feed.
