---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0004
artifact_type: adr
title: "Represent money as a fixed-scale decimal with an explicit half-up rounding policy"
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

# Represent money as a fixed-scale decimal with an explicit half-up rounding policy

## Context and problem statement

FR-3 requires the order total to equal the sum over items of quantity times
unit price, and the total read back to equal that sum exactly. Financial and
operations rely on that number. Money in this change is the unit price agreed
at order time, the line amount, and the order total. Floating point cannot
represent most decimal fractions exactly, so the same arithmetic can produce
different results depending on evaluation order and platform.

OQ-0001 leaves open what precision and rounding apply to item unit prices and
order totals. This decision closes that question and records the policy.

## Decision drivers

1. FR-3: the total is exactly the sum of quantity times unit price, and it is
   never supplied by the caller.
2. Determinism: the same inputs produce the same amounts on every machine and
   in every evaluation order.
3. The rounding rule is explicit, so every writer and reader computes the same
   value.
4. The representation fits the stack: PostgreSQL `numeric` and C# `decimal`
   without a conversion that loses precision.
5. The policy is simple enough to state in one sentence and test at the
   boundary values.
6. Line amounts and the order total are reconciled by finance later, so
   rounding must not silently accumulate.

## Options considered

### Option 1: floating point

Use `double` in C# and a floating type in the store.

- Good: fast and universally available, and it accepts any magnitude.
- Bad: decimal fractions such as 0.10 are not exact, so sums drift and two
  orders with the same items can total differently. It fails drivers 1, 2, and
  6, and it is the way money bugs are introduced.

### Option 2: fixed-scale decimal with half-up rounding

Use C# `decimal` and PostgreSQL `numeric(18,2)`. The scale is two decimal
places. A unit price is accepted at up to two decimal places, and any extra
precision is rounded half-up to two decimals before the line is computed. A
line amount is quantity times the rounded unit price, rounded half-up (round
half away from zero) to two places. The order total is the sum of the two-place
line amounts, so it is exact at that scale and needs no further rounding.
Amounts in this change are non-negative, so half-up and half-away-from-zero
coincide.

- Good: decimal arithmetic is exact for the values that matter, the scale and
  the rounding rule are explicit, the representation maps directly to
  `numeric(18,2)`, and line rounding is the only rounding step, so it cannot
  accumulate differently in different places. It meets drivers 1 through 6.
- Bad: the caller and the service must agree that prices carry at most two
  decimal places; extra precision is rounded half-up to two decimals before the
  line is computed, not stored, which is a rule to document and test.

### Option 3: integer minor units

Store every amount as an integer count of the smallest unit, for example
cents.

- Good: exact integer arithmetic, and the classic choice for a ledger.
- Bad: the API and the requirement speak in major units with a decimal price,
  so every boundary converts, and the conversion is the place an off-by-one or
  a scale error hides. This change has no ledger, no ledger-style balancing,
  and no currency table, so the conversion cost is not paid back. It is a
  reasonable future move if a ledger is added.

## Decision

Option 2. Money is a fixed-scale decimal with scale two. In C#, amounts are
`decimal` and rounding uses half-up (round half away from zero). In PostgreSQL,
amounts are `numeric(18,2)`. A caller-supplied unit price is accepted at up to
two decimal places, and any extra precision is rounded half-up to two decimals
before the line is computed. A line amount is then `quantity * unitPrice`
rounded to two places, and the order total is the sum of the two-place line
amounts. The total is derived from the items at write time and is never
accepted from the caller. The precision and rounding question in OQ-0001 is
closed by this decision; the answer lives here and in DOC-0002.

## Consequences

Every amount in the system has one representation, one scale, and one rounding
rule, so the total a customer reads equals the sum the domain computed. Tests
cover the boundary values: a half-cent line rounds half-up, a total with many
lines equals the exact sum of the rounded lines, and a caller-supplied total is
ignored.

The cost is a documented input rule: values beyond two decimal places are
rounded at the line boundary. If a future change adds multi-currency support or
a ledger with balancing, integer minor units become the better representation,
and this decision should be superseded rather than stretched.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
