---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0007
artifact_type: adr
title: "Allow the same product on more than one order line and keep the lines separate"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, OQ-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Allow the same product on more than one order line and keep the lines separate

## Context and problem statement

An order can contain several items, and a caller can send the same product id
on two lines, for example with different quantities or different agreed prices.
OQ-0001 leaves open whether those lines are kept separate or combined. The
choice changes what the order record means and what the total shows, so it has
to be decided once and applied consistently by the domain, the store, and the
contract.

## Decision drivers

1. FR-1: an order with several items is accepted, and the requirement does not
   forbid repeating a product across lines.
2. FR-2: each line records the unit price agreed at order time, so two lines
   for the same product can legitimately carry different prices.
3. FR-3: the total is the sum over lines of quantity times unit price, so the
   line structure is the input to the total.
4. The choice must be consistent across the domain, the schema, and the
   contract, and testable.
5. The record should reflect what the caller submitted, so a later reader can
   reconstruct the order as placed.
6. The rule should not add validation the requirements do not ask for.

## Options considered

### Option 1: merge lines with the same product

Sum the quantities and combine the lines into one.

- Good: one line per product, which reads cleanly in a summary.
- Bad: it destroys the per-line agreed price, so the total can change from what
  the caller submitted when the prices differ, and the original order cannot be
  reconstructed. It works against drivers 2 and 5.

### Option 2: keep the lines separate

Store each submitted line as its own row. No uniqueness constraint on order and
product, and no merge in the domain.

- Good: the record matches what was submitted, each line keeps its own agreed
  price, and the total is the exact sum of the lines. It meets drivers 1
  through 6 with no extra rule.
- Bad: a reader who wants per-product totals has to aggregate, and the same
  product can appear twice in the response.

### Option 3: reject duplicate products

Refuse an order whose lines repeat a product.

- Good: one line per product without destroying data.
- Bad: it adds a validation rule that neither FR-1 nor FR-2 requires and that
  would reject orders the caller is entitled to place. It fails driver 6.

## Decision

Option 2. The same product may appear on more than one line, and the lines are
kept separate. Each line keeps the quantity and the unit price agreed at order
time, and the total is the sum of the line amounts. The duplicate-lines
question in OQ-0001 is closed by this decision; the answer lives here and in
DOC-0002.

## Consequences

The schema has no unique constraint on order and product, and the contract
shows one order item per submitted line. A client that wants a per-product
total aggregates the response, and the service does not pre-aggregate for it.

The cost is that two lines for one product can have different prices, which is
correct for this requirement but would look wrong to a reader who assumes a
catalog price. If a future change validates prices against a product catalog,
the merge or reject decision should be revisited and this decision superseded.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
