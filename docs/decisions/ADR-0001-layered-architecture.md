---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0001
artifact_type: adr
title: "Adopt a layered architecture with a framework-free domain"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Adopt a layered architecture with a framework-free domain

## Context and problem statement

The order processing backend is a new service, and its order lifecycle rules
are the part the requirements care about most: what a valid order is, which
status changes are legal, and how the total is derived. Those rules will change
more often than the transport or the store. We have to decide where they live
and how the parts of the service depend on each other, so the rules can be
tested without a database or an HTTP request and so a change of store or
transport does not force a rewrite of tested behavior.

The stack is fixed for this change: .NET 10, ASP.NET Core, EF Core 10 with
Npgsql, PostgreSQL 18, and a persistent scheduled-job library. The decision
here is how the code is organized inside that stack, not which stack to use.

## Decision drivers

1. Business invariants live in one place and are testable with no database,
   no HTTP, and no scheduler.
2. The domain model does not depend on EF Core, ASP.NET Core, or the scheduler,
   so it can outlive them.
3. Dependencies point one way, from the edges to the core, so a transport or
   storage change does not ripple into the rules.
4. Persistence is hidden from application logic, with one commit boundary per
   request or per job batch.
5. The design stays small enough for a two-day build, without abstractions that
   earn nothing.
6. The lifecycle is a state machine, and the design leaves a local seam for
   richer per-state behavior later.

## Options considered

### Option 1: everything in the request handlers

Controllers or endpoint handlers validate the request, apply the rules, and
save the order.

- Good: the least code to write up front, and one place to read.
- Bad: the rules sit in transport code and cannot be exercised without an HTTP
  request. Persistence and framework details leak into business rules, and any
  interface change forces edits to already-tested logic. It fails drivers 1, 2,
  and 3.

### Option 2: layered architecture with a framework-free domain

Four modules. Domain holds the Order and OrderItem entities, the status rules,
and the domain exceptions, and references nothing. Application holds the
use-case services, the DTOs, the request validators, and the repository and
unit-of-work interfaces. Infrastructure holds the EF Core DbContext, the
repository implementations, the migrations, and the scheduler adapter. Api
holds the controllers, the error-handling middleware, dependency injection, and
the health endpoints, and is a composition root. Dependencies are one way:
Api and Infrastructure point inward to Application and Domain, Application
points to Domain, and Domain points to nothing. Application defines
`IOrderRepository` and `IUnitOfWork`; the unit of work owns the commit, so no
repository calls `SaveChanges` and one request or one job batch is one
transaction.

- Good: the rules are testable in isolation; the store and the transport can
  be substituted behind the interfaces; the shape is conventional, so it is
  quick to build and easy to hand over. A small repository plus one unit of
  work keeps EF Core out of Application without a generic base class that adds
  nothing.
- Bad: more projects and interface indirection than a single-project build, and
  a little mapping between domain objects and DTOs.

### Option 3: full hexagonal ports-and-adapters

Every external concern sits behind a port, with an adapter for each, and the
application core depends only on ports.

- Good: the strongest isolation, and adapters are easy to swap in tests.
- Bad: this change has few external concerns, so the extra ports and adapter
  boilerplate are not paid back at this size. Drivers 1 through 4 are already
  met by Option 2, and driver 5 argues against the extra layer.

## Decision

Option 2, a layered architecture with a framework-free domain and one-way
dependencies. The repository and unit-of-work boundary sits in Application:
`IOrderRepository` exposes only the operations the use cases need, and
`IUnitOfWork.SaveChangesAsync` is the single commit point.

We considered the State pattern because the order lifecycle is a state machine,
and we are not using it. With five states and almost no per-state behavior,
explicit guarded methods on the Order entity (`Process`, `Ship`, `Deliver`,
`Cancel`) enforce the legal transitions, keep construction and transition rules
in one class, and are directly testable. The State pattern would add a class
per state and a context indirection to vary behavior that does not vary. If
states later acquire distinct behavior, the domain methods are the seam that
makes the refactor local.

## Consequences

The domain rules become the most heavily tested part of the codebase, with no
database in the test. The EF Core model and the HTTP surface can change without
touching the rules. We accept more projects and a small amount of DTO mapping
as the cost.

The unit-of-work decision means every write path commits exactly once. A
use case that needs to read, change, and save an order holds the same context
for the whole operation, which is what makes the optimistic concurrency token
in ADR-0002 effective: the token is checked in the same transaction that loaded
the aggregate.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
