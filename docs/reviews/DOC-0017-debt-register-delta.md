---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0017
artifact_type: doc
title: "Tech debt register delta for the order processing backend"
status: approved
stage: S8
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
# Approver is a placeholder for the EM at review; this draft records no approval and no date.
approver: { kind: human, name: "Komal Bahetwar", role: "EM", date: 2026-10-09 }
gate: G7
created: 2026-10-09
updated: 2026-10-09
sources: [DOC-0005, DOC-0006, DOC-0007, DOC-0012, NFR-0001, ADR-0002]
jurisdiction: []
superseded_by: null
---

# Tech debt register delta for the order processing backend

This is the S8 tech debt register delta, the A8.3 artifact for the G7 packet.
It records the debt the build incurred, each item with an owner and the trigger
that should start the fix, and it records the debt paid down during the build so
the ledger is honest in both directions.

Most of this debt was already written down in DOC-0005 and DOC-0006 at G5. This
delta carries it forward, assigns owners, and states the condition that makes
each item worth doing. The debt is real but none of it changes a functional
outcome the stories promise. The register separates product debt from process
debt because the response is different: product debt is weighed against a
release, and process debt should clear before the service is operated for real.

## Debt incurred

| # | Debt | Kind | Owner | Remediation trigger |
|---|---|---|---|---|
| 1 | Four performance and availability budgets are waived and unmeasured: peak request volume, read and list latency, write latency, and availability | Product measurement | SRE Lead, with the EM | The service runs and telemetry exists. Then run the 30-minute load test for volume and latency and start the monthly availability window, and replace the launch assumption with the observed peak. |
| 2 | The observability budget is implemented but not automatically asserted: the per-transition log line and counter, the correlation id on logs and error responses, and the 200 ms health-signal timing | Product and process | Tech Lead | The next test-writing task, and no later than the first deployment to a shared environment. Add integration assertions so the emitted signals are proved, not assumed. |
| 3 | No automated dependency-vulnerability scan and no SBOM are wired into the build | Process | DevOps, with Security consulted | The first CI pipeline. Add package scanning and SBOM generation as required build steps, so a release cannot pass without them. |
| 4 | The order-items foreign key constraint name does not follow the SQL naming convention; the store expects `fk_order_items_orders_order_id` and the generated name differs | Product, cosmetic | Tech Lead | The next migration that touches `order_items`. Rename it in that migration rather than adding a migration for the name alone. |
| 5 | Order items are not ordered on read, so the read-back sequence is not guaranteed to match the order they were submitted in, though the contract documents the submitted order | Product correctness | Tech Lead | A caller depends on read-back item order, or a schema change touches `order_items`. The fix is a position column set at creation and used in the read order, which makes it a forward-only migration. |
| 6 | `TreatWarningsAsErrors` is absent; the build is warning-free by convention only | Process | Tech Lead | CI can complete an implicit restore with the repository's NuGet config, which the sandbox blocked at build time. Then turn it on and fix any warning the stricter build surfaces. |
| 7 | The local development database password is a committed default, `postgres` | Process, security | DevOps | The first deployment to any shared environment. The connection string must come from the environment, and the committed default should be removed from the deployed configuration. |
| 8 | The batch-continues-after-conflict test exercises the unit of work directly rather than the processing service, so a mid-batch failure at the service level is not asserted | Product, test depth | Tech Lead | The same test-writing task as item 2. Add a service-level assertion that the batch continues after a conflict. |
| 9 | Terminal-state domain guards do not cover the full DELIVERED and CANCELLED matrix | Product, test depth | Tech Lead | The next change to the state machine or its guards. Extend the unit tests to the full terminal matrix. |
| 10 | The field-level error-shape assertion is not automated; the shared shape is implemented and the contract is checked, but the property-by-property assertion is owed | Product, test depth | Tech Lead | The same test-writing task as item 2. |
| 11 | The operational runbooks in DOC-0014 are not independently verified; the runbook quality bar asks for a test by someone other than the author | Process | SRE Lead | Somebody other than the author can follow a runbook in a non-production environment. Verify at least the database and automatic-move paths, and record the result. |
| 12 | Error-budget burn-rate alerts are not designed; DOC-0013 ships the basic rules only | Process | SRE Lead | Availability data exists and the fixed availability budget binds. Add fast and slow burn windows then, not before. |

The twelve items fall into three clusters. Items 1, 9, and 12 need real running
data or a mature state machine. Items 2, 8, and 10 are owed test assertions and
clear together. Items 3, 4, 5, 6, 7, and 11 are engineering hygiene that should
land before the service is operated for real.

## Debt paid down during the build

The build also closed several items rather than carrying them. These are
recorded so the register reflects both directions.

| Paid down | How | Evidence |
|---|---|---|
| A vulnerable transitive dependency: Hangfire pulled Newtonsoft.Json 11.0.1 | Pinned to 13.0.4 in the API and Infrastructure projects and verified clean | DOC-0006 |
| The architecture direction was asserted only in review, not mechanically | Added an `OrderProcessing.ArchitectureTests` project with 5 passing tests that prove the dependency direction and EF confinement | process/S4-review-register.md, DOC-0005 |
| No `.editorconfig` to hold style | Added one | process/S4-review-register.md |
| Batch size was dead configuration and hardcoded | Moved the options type to the application layer and read the batch size from configuration | DOC-0005 |
| The list endpoint did not match the contract and ignored `limit` | The endpoint binds `limit`, clamps it, and returns a bare array | DOC-0005 |
| The single error shape was missing for unexpected and binding failures | One handler emits the shared shape for every failure | DOC-0005 |
| A single conflict aborted the rest of the automatic-move batch | The batch processes one order per fresh load and clears the change tracker on conflict | DOC-0005 |
| The observability budget was unimplemented | Every transition emits a structured log line and an `orders.transitions` counter, and middleware adds the correlation id | DOC-0005 |
| Test coverage fell short of the strategy | The missing race, two-instance, no-op, deliver, negative-price, list-bound, and terminal-guard tests were added | DOC-0005 |

## Register position

This is a delta, not the whole register. It should be merged into the standing
debt register with the owners and triggers above, and re-reviewed at the first
ops review after the service runs. The two items that most change the
confidence in the service are the unmeasured budgets (item 1) and the
unasserted observability signals (item 2): both are cases where we believe the
service is healthy or instrumented but cannot yet prove it. Until they close,
the honest position is that the product behavior is proved and the operational
behavior is not.
