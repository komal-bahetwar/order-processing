---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: OQ-0001
artifact_type: open-questions        # fixed for this template
title: "Open questions for PRD-0001"
status: approved                     # draft | in_review | approved | baselined | superseded | retired
stage: S1                            # producing stage
work_item: TKT-0001
# kind: human | agent. Agents record model; a human author drops the model field.
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
# Added as executor->reviewer rounds run:
#   - { kind: agent, name: reviewer-agent, rounds: 2, verdict: approved }
#   - { kind: human, name: "<...>", role: "<...>" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G1                             # gate this artifact is approved at (if any)
sources: [PRD-0001]                  # the PRD these questions attach to
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Open questions: PRD-0001

This list carries the questions open at the G1 baseline. The four questions
carried from BRIEF-0001 are closed with the resolution below, and each
resolution names where the answer now lives. The two questions added at G1 were
resolved by the S3 design, so none remain open.

| Question | Owner | Due date | Status | Resolution |
|---|---|---|---|---|
| Who moves an order from PROCESSING to SHIPPED and from SHIPPED to DELIVERED: the customer, fulfilment and operations, or an automatic step? | Komal Bahetwar (candidate, interim PM) | 2026-10-09 | resolved | Fulfilment and operations move the order through a status change; the customer does not. Cancel is the only customer-initiated transition. Enforcing who may act is out of scope, and the enforcement point is named for a later design decision. The answer now lives in the PRD flows and in FR-6; the enforcement decision will live in a design artifact at S3. |
| What makes an order eligible to leave PENDING, and does a later status change ever need to be reversed? | Komal Bahetwar (candidate, interim PM) | 2026-10-09 | resolved | Eligibility is simply status = PENDING. The lifecycle is forward-only. DELIVERED and CANCELLED are terminal, with no reversal; a correction is a new order or a compensating action. The answer now lives in the PRD status rules, FR-6, FR-7, and FR-8. |
| Should a customer be able to cancel part of an order, or only the whole order? | Komal Bahetwar (candidate, interim PM) | 2026-10-09 | resolved | Whole order only. Partial cancellation is a non-goal. The answer now lives in the PRD non-goals. |
| What order volume and list size should we assume at launch, and does the list need a bound? | Komal Bahetwar (candidate, interim PM) | 2026-10-09 | resolved | We assume a launch scale of 10 requests per second sustained peak, and we bound the list to at most 20 results by default and at most 100 on request, ordered by creation time. The answer now lives in NFR-0001. |
| When an order repeats the same product on more than one line, do we keep the lines separate or combine them? | Komal Bahetwar (candidate, interim PM) | 2026-10-09 | resolved | Keep the lines separate. The same product may appear on more than one line, each line keeps the quantity and unit price agreed at order time, and the total is the sum of the line amounts. The answer now lives in ADR-0007. |
| What precision and rounding apply to item unit prices and order totals? | Komal Bahetwar (candidate, interim Architect) | 2026-10-09 | resolved | Money is a fixed-scale decimal, C# `decimal` and PostgreSQL `numeric(18,2)`, with half-up rounding at the line. A line amount is quantity times unit price rounded half-up to two places, and the order total is the sum of the rounded lines. The answer now lives in ADR-0004. |
