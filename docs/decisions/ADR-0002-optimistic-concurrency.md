---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0002
artifact_type: adr
title: "Use optimistic concurrency with an explicit version column for order changes"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Use optimistic concurrency with an explicit version column for order changes

## Context and problem statement

Two changes can reach one order at the same time, most importantly a customer
cancel and the automatic move from PENDING to PROCESSING. If both read the
order in PENDING and both write, the second write overwrites the first and one
change is silently lost. FR-10 requires that at most one succeeds, that the
change that does not win reports a conflict, and that an order never reflects
both changes. We have to choose how concurrent changes to one order are
detected and rejected.

## Decision drivers

1. FR-10: two simultaneous changes to one order apply at most one change, and
   the losing change reports a conflict.
2. Correctness under concurrent changes in NFR-0001: exactly one change is
   applied, no change is lost.
3. Portability across relational databases: the mechanism must not depend on a
   system feature that only one database engine exposes.
4. No lock held across work: the mechanism must not hold a database lock across
   the read, the domain work, and the commit.
5. Horizontal scalability in NFR-0001: the mechanism must hold when more than
   one instance runs, with no dependence on in-process state.
6. Testability: a competing-write conflict must be reproducible in a test
   without waiting on real time.
7. Diagnostics: a losing change must be distinguishable from a validation
   failure so the caller gets a conflict rather than a generic error.

## Options considered

### Option 1: an implicit database system row-version token

Read the order with its row version, apply the change, and update with a
predicate that includes the original version. Where the database exposes a
system row-version, the token is that hidden column: in PostgreSQL it is the
system column `xmin`, mapped through EF Core's `UseXminAsConcurrencyToken()`,
and EF Core appends `AND xmin = @original` to the update. If no row matches,
the version changed, EF Core raises `DbUpdateConcurrencyException`, and the use
case translates that to a conflict at the API and to a skip in the automatic
move.

- Good: no application column is added; no lock is held between read and write;
  the database enforces the check, so it holds across instances; and the
  failure is a distinct exception the edge can map to a conflict.
- Bad: it only works on databases that expose a system row-version. That pins
  the system to PostgreSQL and complicates a move to another database, because
  the token has no portable equivalent. The losing change also fails and must
  retry or report, rather than proceed.

### Option 2: an explicit integer version column

Add an integer `version` column to the order. Application code increments it on
every update and includes the value it read in the update predicate:
`UPDATE orders SET ..., version = version + 1 WHERE id = @id AND version =
@original`. If no row matches, another writer changed the order first, so the
use case surfaces a conflict.

- Good: the token is an ordinary column, so it works on any relational database
  and does not pin us to one engine. The database still enforces the check
  through the update predicate, so it holds across instances with no in-process
  state. No lock is held across the domain work. The conflict is a distinct
  outcome, and the token is visible and directly testable.
- Bad: it adds a column and the responsibility to increment it on every write.
  A write path that forgets to increment silently disables the check, so the
  increment has to be centralized in the persistence mapping rather than left
  to each caller.

### Option 3: pessimistic locking

Lock the order row when it is read, for example with `SELECT ... FOR UPDATE`,
and hold the lock until the change commits.

- Good: the second change waits and then reads the updated row, so it sees the
  winner's result.
- Bad: the lock is held across the read, the domain work, and the commit, which
  couples transaction duration to application work and increases contention on
  a hot order. It can deadlock if two orders are locked in different orders. It
  is more coordination than a one-row state change needs.

## Decision

Option 2, optimistic concurrency with an explicit integer `version` column.
Application code reads the order with its version, increments the column on
each write, and the update predicate checks the version it read. A mismatch
raises a conflict, surfaced as a conflict error: a 409 `CONCURRENCY_CONFLICT`
on the request path, and a log-and-skip in the automatic move. Application code
does not retry a losing API change silently; the change reports a conflict so
the caller can decide, per FR-10. The automatic move treats a version mismatch
as "another change already handled this order", logs it, and continues the
batch, because the order has left PENDING by definition of the winner. Retrying
a read-modify-write as an API policy is deferred until a real need appears.

## Consequences

One extra column and the responsibility to increment it on every write. In
exchange, the mechanism is portable across relational databases, and the
database still enforces the conflict check: the version predicate makes two
concurrent updates mutually exclusive no matter how many instances run. No lock
is held across domain work. This is what lets the concurrency test and the
two-instance test in the test strategy prove FR-10 and the horizontal
scalability budget with the same mechanism.

Every write path has to handle the conflict. The API maps it to status 409 with
the `CONCURRENCY_CONFLICT` code, and the automatic move catches it per order so
one contested order does not fail the batch. Because the token is an ordinary
version column, a move to another relational database keeps the same behavior.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
