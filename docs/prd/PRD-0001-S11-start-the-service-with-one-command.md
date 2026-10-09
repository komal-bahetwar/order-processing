---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001-S11
artifact_type: story                 # fixed for this template
title: "Start the service with one command"
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

# Start the service with one command

## Story

As an operator, we want to start the service with one command on a clean machine, so that setting it up does not depend on undocumented manual steps.

## Traceability

Delivers the operability budget in NFR-0001. Every acceptance criterion below cites NFR-0001.

## Acceptance criteria

- NFR-0001: Given a clean machine, when the documented single command is run, then the service and its data store start with no manual step.
- NFR-0001: Given the service starts, when it comes up, then required data-store changes apply on startup without a manual step.
- NFR-0001: Given the service and its data store are starting, when readiness is checked, then readiness reports ready only after the data store is available.

## Notes and assumptions

- This story states observable behavior only. How the command starts the service and the data store, and how data-store changes are applied, are design decisions for S3.
- One command means the operator does not run a separate provisioning step, a separate change step, or a separate wait before the service is usable.
- Readiness reflects the data store, not just the process being up.

## Dependencies

- S01, which provides the order capability the service exposes.
