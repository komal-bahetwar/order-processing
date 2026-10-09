---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0009
artifact_type: adr
title: "Harden domain invariants, money ranges, status parsing, and scheduling"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-10 }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0002, ADR-0004, ADR-0006]
jurisdiction: []
created: 2026-10-10
updated: 2026-10-10
superseded_by: null
---

# Harden domain invariants, money ranges, status parsing, and scheduling

## Context and problem statement

An external improvement brief reviewed the service and found four correctness
gaps and one reliability gap, all reproduced. A create request with a null item,
a price above the storage ceiling, and a decimal arithmetic overflow each
returned HTTP 500 rather than a validation error. The list filter accepted an
out-of-range numeric status (`?status=999`) and returned 200 with an empty array.
The aggregate exposed its mutable backing list, so in-process code could add or
remove items and make the stored total disagree with the items. The scheduling
options were bound without validation, a run processed a single batch, and a
failure to register the mandatory recurring job was logged while startup
continued.

The fixes must not change the public contract. The endpoints, DTO shapes, status
strings, money rounding policy, and the existing stable error codes are frozen;
invalid input that previously produced 400 must keep producing 400.

## Decision drivers

1. Invalid input is a client error, not a server error; it must return 400.
2. Aggregate invariants must hold even when the HTTP validators are bypassed.
3. Money must stay within the `numeric(18,2)` storage range without losing the
   deliberate two-decimal half-up rounding (ADR-0004).
4. The status contract is a set of defined names, not the underlying enum
   numbers.
5. The automatic move is a mandatory capability and must be reliable and
   observable.
6. The public contract and the frozen design are preserved.

## Options considered

### Option 1: Leave the behavior and document the 500s as known limitations

Cheapest, but it accepts reachable 500s and a mutable aggregate, which the
review identified as real defects. Rejected.

### Option 2: Fix each gap where it appears, without shared rules

Add null checks in the validator, catch the overflow in the handler, and so on.
This spreads the invariant across layers, invites divergence, and lets a bypass
of the HTTP layer reach persistence. Rejected.

### Option 3: Centralize the rules in the domain and validators (chosen)

Put the money range and normalization rules and the status-name parsing in the
domain, expose a genuinely read-only collection, declare one legal transition
relation, and validate scheduling options at startup with a bounded backlog
drain. The HTTP validators reuse the same rules so the two layers cannot
diverge.

## Decision

Adopt Option 3.

- Expose the items as a `ReadOnlyCollection<OrderItem>` and reject null inputs,
  null elements, and an empty product id in the domain. These map to the
  existing 400 `VALIDATION_ERROR` response; no new public error code is added.
- Add domain money rules (`Money`): normalize a price to two decimals half-up,
  reject a negative price, and reject a normalized price, line total, or order
  total above the `numeric(18,2)` ceiling of `9,999,999,999,999,999.99` using
  division and remaining-capacity checks that cannot overflow.
- Parse statuses by defined name only (`OrderStatusNames`), case-insensitively,
  rejecting numeric strings and undefined names; keep one legal transition
  relation with exactly four transitions.
- Validate the scheduling options at startup (positive bounded batch, budget at
  least the batch, a cron the scheduler's parser accepts), drain the backlog
  across batches within the run budget, and treat a failure to register the
  recurring job as a startup failure. Set an explicit finite retry policy of
  three attempts on the job.

## Consequences

Invalid input now returns 400 with no persisted rows, and the aggregate can no
longer be mutated outside its methods. The domain carries the money and status
rules, so a direct application-service call cannot bypass them. The service is
honest about its backlog: a run reports processed, skipped, selected, and
whether it stopped at its budget.

The cost is a small amount of new domain code and a stricter startup: a bad
configuration or an unreachable Hangfire store now prevents the service from
starting. That is deliberate, because the automatic move is mandatory. The
public contract, rounding policy, and stable error codes are unchanged, so no
OpenAPI or DTO delta is required.

## Status

Proposed. The artifact frontmatter stays `draft` until the gate ratifies this
decision, at which point the MADR status becomes accepted.
