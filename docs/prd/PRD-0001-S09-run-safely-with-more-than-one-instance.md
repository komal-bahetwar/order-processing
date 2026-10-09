---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S09
artifact_type: story                 # fixed for this template
title: "Run safely with more than one instance"
status: approved                     # draft | in_review | approved | baselined | superseded | retired
stage: S2                            # producing stage
work_item: TKT-0001
# kind: human | agent. Agents record model; a human author drops the model field.
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
# Added as executor->reviewer rounds run:
#   - { kind: agent, name: reviewer-agent, rounds: 2, verdict: approved }
#   - { kind: human, name: "<...>", role: "<...>" }
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

# Run safely with more than one instance

## Story

As operations, we want the service to stay correct when it runs as more than one instance, so that adding capacity to handle more orders never breaks an order or applies a change twice.

## Traceability

Delivers the horizontal scalability budget in NFR-0001. Every acceptance criterion below cites NFR-0001.

## Acceptance criteria

- NFR-0001: Given two instances run against one data store, when an order is eligible for the automatic move, then each eligible order moves once and no order moves twice.
- NFR-0001: Given a status change races the automatic move, when both are handled, then the order applies at most one change.
- NFR-0001: Given read, list, and status requests, when any instance handles them, then the result does not depend on state held in a single instance.

## Notes and assumptions

- This story states observable behavior only. How the instances coordinate so a change applies once is a design decision for S3.
- The automatic move is the change most exposed to more than one instance, because each instance could otherwise attempt the same move.
- Adding a second instance adds throughput because each request is handled independently and no request depends on state held in a particular instance.

## Dependencies

- S01, which creates and holds the orders these instances serve.
- S04 and S05, which supply the manual status change and the cancel that can race the automatic move.
- S06, which supplies the automatic move.
