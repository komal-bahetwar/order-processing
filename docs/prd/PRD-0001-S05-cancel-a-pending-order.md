---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S05
artifact_type: story                 # fixed for this template
title: "Cancel a pending order"
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

# Cancel a pending order

## Story

As a customer, I want to cancel an order while it is still PENDING, so that I can stop an order I no longer want before it starts moving.

## Traceability

Delivers FR-7 from PRD-0001. The PRD places the flow under Cancel an order, and the cancellation and rejection success criteria are the acceptance conditions below.

## Acceptance criteria

- FR-7: Given an order is PENDING, when the customer cancels it, then it becomes CANCELLED.
- FR-7: Given an order is PROCESSING, SHIPPED, or DELIVERED, when the customer cancels it, then the cancel is rejected and the order keeps its status.
- FR-7: Given an order is already CANCELLED, when the customer cancels it again, then the cancel is rejected and the order keeps its status.
- FR-7: Given a customer cancels a PENDING order, when the cancellation is accepted, then the whole order is cancelled.

## Notes and assumptions

- Cancellation is whole-order only. Partial cancellation is a non-goal.
- Cancel is the only customer-initiated status change.
- The cancel of a PENDING order can arrive at the same time as the automatic move in S06. S07 covers that race; the order ends in exactly one status.
- Enforcing who may act is out of scope. The actor named for cancel is the customer.
- A rejected cancel leaves the order, its items, its total, and its status untouched.

## Dependencies

- S01, which creates the orders this story cancels.
