---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S06
artifact_type: story                 # fixed for this template
title: "Move pending orders to processing automatically"
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

# Move pending orders to processing automatically

## Story

As fulfilment and operations, I want every PENDING order to become PROCESSING automatically within about five minutes, so that orders start moving without anyone having to nudge them.

## Traceability

Delivers FR-8 from PRD-0001. The PRD places the flow under Automatic move from pending to processing, and the timeliness success criterion is the acceptance conditions below.

## Acceptance criteria

- FR-8: Given an order becomes PENDING, when about five minutes pass, then the order has become PROCESSING with no manual step.
- FR-8: Given an order has already left PENDING, when the automatic move runs, then the order is not affected.
- FR-8: Given an order is CANCELLED, when the automatic move runs, then the order is not affected.
- FR-8: Given no order is PENDING, when the automatic move runs, then no order changes.
- FR-8: Given the automatic move runs twice over the same unchanged set of orders, then the result after the second run is the same as after the first.

## Notes and assumptions

- The only condition that makes an order eligible to leave PENDING this way is that its status is PENDING.
- The move is forward only. It never reverses a status and never touches an order that has left PENDING.
- The about-five-minute bound is the requirement's own bound, restated in NFR-0001. It is verified without waiting on real time.
- Repeating the move over orders it has already handled is harmless and changes nothing.
- Enforcing who may act is out of scope. The actor named for the automatic move is the system on behalf of fulfilment and operations.

## Dependencies

- S01, which creates the orders this story moves.
