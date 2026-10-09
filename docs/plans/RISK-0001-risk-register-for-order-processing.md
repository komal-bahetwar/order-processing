---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: RISK-0001
artifact_type: risk                  # fixed for this template
title: "Risk register for order processing"
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
approver: { kind: human, name: "Komal Bahetwar", role: "EM", date: 2026-10-09 }
gate: G2                             # gate this artifact is approved at (if any)
sources: [PRD-0001, PLAN-0001, PLAN-0002, PLAN-0003]
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Risk register: order processing

## Risks

| Risk | Likelihood | Impact | Mitigation | Owner | Status |
|---|---|---|---|---|---|
| Two changes to one order at the same time produce a lost or doubled change | med | high | S07 fixes the outcome: at most one of two simultaneous changes is applied, and the change that does not win reports a conflict. The concurrency tests prove it. S3 chooses how the losing change is detected, reviewed at G3 and again under the four-eyes rule at G4. | Komal Bahetwar (Tech Lead) | open |
| The automatic move from PENDING to PROCESSING fails silently, so orders sit pending | med | high | S06 fixes eligibility and the no-op cases and tests them. NFR-0001 requires one log line and one counter per transition, verified at G5. PRD-0001-S08 delivers that per-transition telemetry, so a missing transition is visible; the alert rule that fires on it is an operations concern, added when the service runs and homed in the S7 alert and runbook artifacts (A7.2/A7.3). | Komal Bahetwar (EM) | open |
| The automatic move runs on more than one instance and moves an order twice | med | med | S09 fixes the outcome: each eligible order moves once even when more than one instance runs, and a status change racing the move applies at most one change. The two-instance test against one data store proves it. How the instances coordinate is chosen at S3 and reviewed at G3. | Komal Bahetwar (Architect) | open |
| The persistence and migration layer behaves differently than the design assumes, in ordering, concurrency, or the derived total | med | high | S3 data model and migration design (A3.4) reviews the assumptions. Integration tests run against a real database, per the NFR-0001 testability budget. Migrations are forward-only with a compensation path. | Komal Bahetwar (Architect) | open |
| The third-party scheduler dependency adds risk the change does not need | med | med | Keep the processing logic independent of the scheduler so it can be tested and swapped. Record the choice as an ADR at S3. Test the routine by invoking it directly rather than waiting on a schedule. | Komal Bahetwar (Architect) | open |
| Money precision and rounding stay unresolved (OQ-0001, due 2026-10-23) | med | med | Close the question before S3. S3 records the precision and rounding policy. S01's estimate stays soft until it closes. | Komal Bahetwar (PM) | open |
| Duplicate product lines stay unresolved (OQ-0001, due 2026-10-16) | med | low | Close the question before S3. Either behavior is acceptable if it is consistent. S01 and S03 estimates stay soft until it closes. | Komal Bahetwar (PM) | open |
| Mechanism leaks into early artifacts and turns into scope creep | low | med | S2 stories stay behavior-only and mechanism choices wait for S3. The reviewer checks the no-mechanism quality bar at each artifact. | Komal Bahetwar (EM) | open |
| The integration tests depend on a containerized database that may not start in the sandbox | med | med | Pin the database image and document the one-command run. Keep the suite inside the NFR-0001 ten-minute bound. Document a fallback if the container cannot start. | Komal Bahetwar (Tech Lead) | open |
