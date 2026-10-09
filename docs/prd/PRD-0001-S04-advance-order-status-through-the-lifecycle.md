---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S04
artifact_type: story                 # fixed for this template
title: "Advance order status through the lifecycle"
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

# Advance order status through the lifecycle

## Story

As fulfilment and operations, I want to move an order from PROCESSING to SHIPPED and from SHIPPED to DELIVERED, so that the order record shows where the order actually is.

## Traceability

Delivers FR-6 from PRD-0001. The PRD places the flow under Advance order status, and the accepted and rejected transition success criteria are the acceptance conditions below.

## Acceptance criteria

- FR-6: Given an order is PROCESSING, when a change to SHIPPED is requested, then the order becomes SHIPPED.
- FR-6: Given an order is SHIPPED, when a change to DELIVERED is requested, then the order becomes DELIVERED.
- FR-6: Given an order is PENDING, when a change to SHIPPED or DELIVERED is requested, then it is rejected and the order keeps its status.
- FR-6: Given an order is PROCESSING, when a change to a different status other than SHIPPED is requested, then it is rejected and the order keeps its status.
- FR-6: Given an order is SHIPPED, when a change to a different status other than DELIVERED is requested, then it is rejected and the order keeps its status.
- FR-6: Given an order is DELIVERED or CANCELLED, when a change to a different status is requested, then it is rejected and the order keeps its status.
- FR-6: Given an order holds a status, then it holds exactly one status at a time from PENDING, PROCESSING, SHIPPED, DELIVERED, and CANCELLED.

## Notes and assumptions

- The manual changes this story delivers are PROCESSING to SHIPPED and SHIPPED to DELIVERED.
- PENDING to PROCESSING is the automatic move in FR-8, delivered by S06. PENDING to CANCELLED is the customer cancel in FR-7, delivered by S05. A request that sets an order to the status it already holds is a no-op in FR-9, delivered by S07.
- DELIVERED and CANCELLED are terminal. Nothing moves out of them.
- Enforcing who may act is out of scope. The actor named for the manual changes is fulfilment and operations.
- A rejected change leaves the order, its items, its total, and its status untouched.

## Dependencies

- S01, which creates the orders this story advances.
