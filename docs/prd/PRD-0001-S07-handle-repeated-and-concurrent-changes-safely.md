---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S07
artifact_type: story                 # fixed for this template
title: "Handle repeated and concurrent changes safely"
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
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G2                             # gate this artifact is approved at (if any)
sources: [PRD-0001]
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Handle repeated and concurrent changes safely

## Story

As a customer and as an operations user, I want repeated and simultaneous changes to an order to be handled safely, so that no change is silently lost and an order never reflects two competing changes at once.

## Traceability

Delivers FR-9 and FR-10 from PRD-0001. The PRD states the same-status no-op under Advance order status and both safe-change success criteria in the success criteria section.

## Acceptance criteria

- FR-9: Given an order holds a status, when a request sets it to that same status, then the request is accepted, the order is unchanged, and no additional change results.
- FR-9: Given the same request is sent again, when it is handled, then the result is the same as the first.
- FR-10: Given two changes to the same order are submitted at the same time, when both are handled, then at most one succeeds.
- FR-10: Given two changes to the same order are submitted at the same time, when one does not win, then it reports a conflict outcome and the order reflects only the winning change.
- FR-10: Given a customer cancels a PENDING order at the same time as the automatic move handles it, when both are handled, then the order reflects exactly one of the two outcomes.

## Notes and assumptions

- The race between a customer cancel and the automatic move on a PENDING order is the central case this story must protect.
- A change that does not win reports a conflict. It is not retried silently, and it does not overwrite the winner's result.
- A same-status request is accepted as a no-op for any status, including a terminal one.
- At no point does an order reflect both of two competing changes.

## Dependencies

- S01, which creates the orders this story protects.
- S04, S05, and S06, which supply the changes that can repeat or race.
