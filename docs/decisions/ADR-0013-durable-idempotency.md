---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0013
artifact_type: adr
title: "Add durable idempotency to order creation"
status: draft
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "<...>", role: "Architect", date: <YYYY-MM-DD> }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0006]
jurisdiction: []
created: 2026-10-10
updated: 2026-10-10
superseded_by: null
---

# Add durable idempotency to order creation

## Context and problem statement

An order can commit while the HTTP response is lost, and a retry of
`POST /api/orders` creates a second order. Same-status no-ops, the job
transition guards, and optimistic concurrency do not deduplicate creation. The
improvement brief asked for durable, optional idempotency on creation only.

## Decision drivers

1. A retried create must not create a second order.
2. Requests without a key keep today's behavior.
3. The deduplication must survive a process restart, so it is durable.
4. The order and its idempotency record must commit atomically.
5. Concurrent claims are coordinated by the database, not by a process lock.
6. The response is replayed from the original snapshot, not recomputed.
7. The mechanism must not become an authorization or identity control.

## Options considered

### Option 1: No idempotency

Rejected: the brief's reproduced defect stands.

### Option 2: Process-local (in-memory) deduplication

Cheap, but it does not survive a restart and does not coordinate across
instances. Rejected.

### Option 3: A durable idempotency record in the same transaction as the order (chosen)

Accept an optional `Idempotency-Key`, store a record keyed by
`(scope, key)` with a request fingerprint and the original response, and commit
it with the order in one transaction. A unique constraint coordinates
concurrent claims.

## Decision

Adopt Option 3.

- An optional `Idempotency-Key` header (1 to 128 characters from letters,
  digits, `.`, `_`, `-`) on create only. No header means current behavior.
- Scope is `create-order/v1`, application-wide, including the operation and
  version, because no trusted authentication exists yet. Keys are opaque and
  case sensitive and are not an authorization or identity control.
- The record stores a deterministic normalized-request fingerprint (item order,
  duplicate lines, quantities, product ids, and normalized prices; JSON
  whitespace and property order ignored), the response status, content type,
  body snapshot, `Location`, order id, creation time, and expiry.
- The order and the record are added to one unit of work and committed in one
  transaction. A concurrent claim is rejected by the unique constraint
  `ux_idempotency_records_scope_key`; the loser reads the winner and replays.
  The expected unique-constraint conflict is translated to a dedicated
  exception; other persistence failures are not.
- A retry with the same key and an equivalent request replays the stored
  response body snapshot as a 201 with the order's canonical `Location`. The
  record also stores the original status code, content type, and location so the
  response is captured, not recomputed from the current order. A different
  request under the same key returns 409 `IDEMPOTENCY_KEY_REUSED`. A claim that
  cannot be resolved returns 409 `IDEMPOTENCY_REQUEST_IN_PROGRESS`.
- Successful results are retained for a configurable minimum (default 24
  hours). A scheduled cleanup deletes expired records; it is safe across
  workers and never removes a live claim. After expiry the key may be reused.
- Keys are never logged raw; only a bounded fingerprint is compared.

## Consequences

A client can retry creation safely. The cost is one table, one migration, and a
cleanup job. The response snapshot is stored, so a retry after the order has
moved on still replays the original creation result. No authentication is added
and no caller identity is invented.

## Status

Proposed. The artifact frontmatter stays `draft` until the gate ratifies this
decision, at which point the MADR status becomes accepted.
