---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0006
artifact_type: adr
title: "Treat a repeat of the current status as an application no-op and defer duplicate-submission suppression"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Treat a repeat of the current status as an application no-op and defer duplicate-submission suppression

## Context and problem statement

FR-9 requires that a request to set an order to the status it already holds is
accepted, returns the order unchanged, and creates no additional change, and
that a repeated identical request produces the same result as the first. The
domain transitions are guarded, so a naive call would throw on a same-status
request because the order is not in the required predecessor state. We also
have a wider idempotency question: a client that retries a create or a cancel
after a timeout can send the same request twice, and the service has no
duplicate-suppression mechanism. We have to decide where the same-status case
is handled and how far idempotency goes in this change.

## Decision drivers

1. FR-9: a same-status request is accepted as a no-op for any status,
   including a terminal one, and leaves the order unchanged.
2. FR-6: the domain rejects every illegal transition, and that guard is the
   single authority for what the lifecycle allows.
3. The interface should not report a conflict for a request that changed
   nothing.
4. Duplicate-submission suppression needs a client-supplied key and storage for
   seen keys, which is a larger feature than this change requires.
5. The behavior must be explicit and testable, not an accidental side effect.
6. Retrying a status change after a timeout should be safe to send again.

## Options considered

### Option 1: handle the same-status case in the domain

Let `Order` return without change when the requested status equals the current
one.

- Good: one place for status logic.
- Bad: it weakens the transition guard, which is the authority for what the
  lifecycle allows. The guard would need to distinguish "same status, fine"
  from "illegal status, reject", and the domain would take on transport-level
  idempotency. It muddies driver 2.

### Option 2: application-layer no-op plus the domain guard, defer duplicate-submission suppression

The application service compares the requested status with the current status
first. If they match, it returns the order unchanged with a 200 and does not
call a transition method. Otherwise it maps the request to the named domain
method (`Ship`, `Deliver`), and the domain guard rejects anything illegal with
a 409. Duplicate-submission suppression for create and cancel is not built in
this change: a repeated create makes a second order, and a repeated cancel of
an already-cancelled order returns 409 because the order has left PENDING.
A future `Idempotency-Key` header on the write endpoints is the planned
mechanism.

- Good: FR-9 is met for every status; the domain guard stays the single
  authority; the no-op is explicit and testable; and the unreliable-network
  case is documented rather than half-built. It meets drivers 1 through 6.
- Bad: the application service and the domain both know about status equality,
  so the rule is split in two. A repeated create still makes a second order,
  which is a known limitation, not a hidden one.

### Option 3: full idempotency keys for every write now

Require an `Idempotency-Key` header and store request hashes and responses so
a retry returns the first result.

- Good: solves duplicate submission properly.
- Bad: adds a storage table, a retention policy, a header contract, and a
  replay path, all for a change with no client that requires it. It fails
  driver 4 and delays the rest of the build.

## Decision

Option 2. The application service treats a repeat of the current status as a
no-op and returns 200; the domain guard continues to reject every illegal
transition, including a cancel of a non-pending order. Duplicate-submission
suppression is deferred to a future idempotency mechanism, and the limitation
is recorded here so it is a known gap rather than an assumption.

## Consequences

A retried status change is safe to send again: the same value is a no-op, and a
value that has already been overtaken is rejected by the domain guard. The
concurrency token from ADR-0002 still applies, so a same-status no-op that
overtakes a concurrent change reports a conflict rather than overwriting it.

The known limitation is that create and cancel are not deduplicated. If a
client retries a create after a timeout, two orders exist; if it retries a
cancel after the order has moved on, it receives 409. A future change adds
`Idempotency-Key` on the write endpoints and a seen-key store, and supersedes
the deferred part of this decision.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
