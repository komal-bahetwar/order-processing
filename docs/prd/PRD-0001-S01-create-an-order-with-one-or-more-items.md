---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S01
artifact_type: story                 # fixed for this template
title: "Create an order with one or more items"
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

# Create an order with one or more items

## Story

As a customer, I want to place an order with one or more items, so that I have one dependable record of what I bought and what it costs.

## Traceability

Delivers FR-1, FR-2, and FR-3 from PRD-0001. The PRD places the flow under Place order, and the creation and rejection success criteria are the acceptance conditions below.

## Acceptance criteria

- FR-1: Given a customer places an order with one item whose quantity is greater than zero, when the order is created, then it is captured with that item and starts in PENDING.
- FR-1: Given a customer places an order with several distinct items, when the order is created, then every item is captured on the order.
- FR-2: Given an order with no items, when it is submitted, then it is rejected, no order is captured, and the rejection identifies the rule that failed.
- FR-2: Given an order with an item whose quantity is zero or less, when it is submitted, then it is rejected, no order is captured, and the rejection identifies the rule that failed.
- FR-2: Given an order with an item whose unit price is negative, when it is submitted, then it is rejected, no order is captured, and the rejection identifies the rule that failed.
- FR-3: Given an order with several items, when it is read back, then its total equals the sum over its items of quantity multiplied by unit price.
- FR-3: Given a total is supplied with an order, when the order is created, then the total is derived from the items and the supplied value has no effect.

## Notes and assumptions

- Every new order begins in PENDING.
- An item records the product, a quantity greater than zero, and the unit price agreed at order time.
- An order with one item and an order with several distinct items are both valid. A zero unit price is allowed; a negative unit price is not.
- Product catalogue and price management are out of scope, so the price on an item is the price agreed at order time.
- Whether repeated product lines in one order are kept separate or combined is open in OQ-0001, due 2026-10-16. Until it is resolved, either behavior is acceptable as long as it is consistent.
- Money precision and rounding are open in OQ-0001, due 2026-10-23. The total is the exact sum of the item amounts under whatever precision is agreed.

## Dependencies

- None. This story is the base that S02 to S08 build on.
