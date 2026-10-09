---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S03
artifact_type: story                 # fixed for this template
title: "List and filter orders by status"
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

# List and filter orders by status

## Story

As a customer, I want to list orders and narrow the list to a single status, so that I can find the orders I care about without scanning all of them.

## Traceability

Delivers FR-5 from PRD-0001 and the list result bound in NFR-0001. The PRD places the flow under List and filter orders, and the listing and bound success criteria are the acceptance conditions below.

## Acceptance criteria

- FR-5: Given orders exist, when a customer lists orders with no status filter, then every order is returned.
- FR-5: Given orders exist in more than one status, when a customer narrows the list to a single status, then exactly the orders in that status are returned.
- FR-5: Given no order matches the filter, when the list is requested, then an empty list is returned.
- FR-5: Given the same orders are listed repeatedly with no change to them, when the list is requested, then the sequence is the same every time, oldest first by creation time with a deterministic tie-break.
- NFR-0001: Given more orders exist than the default bound, when a customer lists orders without asking for a larger bound, then at most 20 results are returned.
- NFR-0001: Given more orders exist than the maximum bound, when a customer asks for a larger bound, then at most 100 results are returned.
- NFR-0001: Given a list is returned under a bound, when the results are read, then they are ordered by creation time.

## Notes and assumptions

- A list returns orders only, with no change to any order.
- The default bound is 20 and the maximum a caller can request is 100. The bound applies to the list after the status filter.
- The filter matches on one status at a time or on no status at all.

## Dependencies

- S01, which creates the orders this story lists.
