---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S10
artifact_type: story                 # fixed for this template
title: "Return a consistent contract and error model"
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

# Return a consistent contract and error model

## Story

As an integrator, we want one documented contract and one error shape, so that we can build against the service predictably instead of guessing what each operation takes, returns, and fails with.

## Traceability

Delivers the interface contract and error model budget in NFR-0001. Every acceptance criterion below cites NFR-0001.

## Acceptance criteria

- NFR-0001: Given the service's operations, when the contract is published, then it is one machine-readable contract that describes every operation, lints clean, and validates in CI.
- NFR-0001: Given any failure, when it is returned, then it uses one documented error shape with a stable code and a meaningful status.
- NFR-0001: Given the contract is published, when contract tests run against the service, then the service conforms to the contract for every operation.

## Notes and assumptions

- This story states observable behavior only. The contract format and the error shape are design decisions for S3.
- One error shape means the same fields and meaning for validation failures, not-found cases, and conflicts. It carries a stable code and a meaningful status.
- The contract lints clean and validates in CI, so the service cannot drift from what integrators build against.

## Dependencies

- S01, S02, S03, S04, and S05, whose operations the contract describes.
