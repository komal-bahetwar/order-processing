---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S08
artifact_type: story                 # fixed for this template
title: "Emit order lifecycle observability"
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
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G2                             # gate this artifact is approved at (if any)
sources: [PRD-0001]
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Emit order lifecycle observability

## Story

As fulfilment and operations, I want to see every order state change, so that we can tell an order is moving and notice when a change stops happening.

## Traceability

Delivers the observability budget in NFR-0001, which PRD-0001 names as the non-functional budget this change touches. Every acceptance criterion below cites NFR-0001.

## Acceptance criteria

- NFR-0001: Given an order changes state, when the change is applied, then exactly one structured log line is emitted that carries the order id and the old and new status, and one is present for every transition the service performs.
- NFR-0001: Given an order changes state, when the change is applied, then exactly one counter metric is emitted for that transition, and one is present for every transition the service performs.
- NFR-0001: Given any request, when it is handled, then every log line and every error response carries a correlation identifier that ties the request to the change it caused, present for every request.
- NFR-0001: Given the service is running, when liveness is checked, then a liveness signal responds within 200 ms.
- NFR-0001: Given the service is running, when readiness is checked, then a readiness signal responds within 200 ms.

## Notes and assumptions

- This story states observable behavior only. How the log line, the counter, the health signals, and the correlation identifier are produced or propagated is a design decision for S3.
- The transitions in scope are the ones delivered by S04, S05, and S06: PENDING to PROCESSING, PENDING to CANCELLED, PROCESSING to SHIPPED, and SHIPPED to DELIVERED.
- The log line carries the order id and the two statuses. The counter counts transitions, with one series per transition.
- Every log line and every error response carries a correlation identifier, so a request ties to the change it caused.
- The 200 ms bound applies to each health signal on its own, not to the two together.

## Dependencies

- S01, which creates the orders whose state is observed.
- S04, S05, and S06, which supply the transitions this story observes.
