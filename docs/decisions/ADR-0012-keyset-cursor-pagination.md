---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0012
artifact_type: adr
title: "Adopt keyset cursor pagination for the order list"
status: draft
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "<...>", role: "Architect", date: <YYYY-MM-DD> }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0009]
jurisdiction: []
created: 2026-10-10
updated: 2026-10-10
superseded_by: null
---

# Adopt keyset cursor pagination for the order list

## Context and problem statement

The list endpoint returned a bounded array with no way to reach orders past the
limit, so a client could not list all matching orders, which the original
assignment expects. The result also had to stay backward compatible: the
response is an array today, and a single-page client must keep working.

## Decision drivers

1. A client can traverse all matching orders in bounded pages.
2. The response body stays an array; existing clients are unaffected.
3. The continuation is consistent with the stable `(CreatedAt, Id)` ordering.
4. No unbounded reads, and no new generic query framework.
5. A client token is never interpreted as SQL.

## Options considered

### Option 1: Offset pagination (`page`/`pageSize`)

Simple, but it drifts under concurrent inserts (rows shift between pages), and it
was already rejected once in design in favor of a bounded limit. Rejected.

### Option 2: Keyset cursor pagination over `(CreatedAt, Id)` (chosen)

Return the array with an optional opaque cursor and an `X-Next-Cursor` response
header. The cursor encodes the last row's `(CreatedAt, Id)`; the next page seeks
past it. This reuses the existing index and is stable under inserts.

### Option 3: No pagination

Rejected: the bounded limit was the original gap.

## Decision

Adopt Option 2.

- Add an optional `cursor` query parameter and an `X-Next-Cursor` response
  header, omitted on the final page. The response body stays an array of orders
  and the default and capped `limit` are unchanged for first-page clients.
- The cursor is an opaque base64url encoding of the last row's `(CreatedAt
  UtcTicks, Id)`. It is validated for length and format and rejected with 400
  `VALIDATION_ERROR` when malformed; it is never treated as SQL.
- The seek predicate is `CreatedAt > @c OR (CreatedAt = @c AND Id > @id)` on the
  database, so it agrees with the database's tie-break ordering for same-
  timestamp rows. The query reads `limit + 1` rows to detect whether a next page
  exists.
- Pagination is not a snapshot: concurrent inserts or status changes can affect
  traversal, and that is documented.

## Consequences

A client can traverse the full result set with a bounded, stable cursor, and
existing clients are unaffected. The cost is a small cursor codec and a seek
predicate. The cursor carries no secret and no caller identity.

## Status

Proposed. The artifact frontmatter stays `draft` until the gate ratifies this
decision, at which point the MADR status becomes accepted.
