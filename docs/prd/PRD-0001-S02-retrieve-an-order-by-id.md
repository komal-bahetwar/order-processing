---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S02
artifact_type: story                 # fixed for this template
title: "Retrieve an order by id"
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

# Retrieve an order by id

## Story

As a customer, I want to retrieve an order by its id, so that I can see its items, its total, and its current status.

## Traceability

Delivers FR-4 from PRD-0001. The PRD places the flow under View order, and the retrieval success criterion is the acceptance condition below.

## Acceptance criteria

- FR-4: Given an order exists, when a customer retrieves it by its id, then the order, its items, its total, and its current status are returned.
- FR-4: Given no order holds the requested id, when a customer retrieves it, then a not-found outcome is returned.
- FR-4: Given a not-found outcome, when it is compared with a successful retrieval, then the two outcomes are distinguishable.
- FR-4: Given an order is retrieved, then the retrieval does not change the order or its status.

## Notes and assumptions

- The order id identifies exactly one order.
- Retrieval is read-only: it never changes an order, its items, its total, or its status.
- An order that was created stays retrievable; nothing in this change removes an order.

## Dependencies

- S01, which creates the orders this story retrieves.
