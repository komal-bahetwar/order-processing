---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0008
artifact_type: adr
title: "Adopt a modular monolith with explicit module boundaries"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0001, ADR-0003]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Adopt a modular monolith with explicit module boundaries

## Context and problem statement

We are building the order processing backend as one bounded workflow: create an
order, retrieve it, list and filter orders, advance the status, cancel while
pending, and move pending orders to processing automatically. It has no user
interface, no authentication, no personal data, and no tenant boundary. It is a
take-home that a reviewer has to be able to run locally with one command.

We have to choose the overall architecture style rather than drift into one.
The choice is between one deployable process with clear internal modules, a set
of services split by capability now, and a distributed event-driven set of
services now. The answer turns on what this change actually needs and on what
we can safely defer.

The stack is fixed for this change: .NET 10, ASP.NET Core, EF Core 10 with
Npgsql, PostgreSQL 18, and a persistent scheduled-job library. The decision here
is the shape of the system, not the stack.

## Decision drivers

1. Delivery speed. The build is time-boxed, and the style should not add
   deployment, messaging, or coordination work that this change does not pay
   back.
2. Operational simplicity. One thing to run, one place to look when it fails,
   and a local run that a reviewer can reproduce.
3. Clear boundaries for future change. The lifecycle rules must stay separable
   from the transport, the store, and the scheduler, so a later change lands in
   one place.
4. The option to extract later, only when a trigger fires. A capability that
   might become its own service has to be extractable without rewriting the
   rules.
5. Testability. The domain rules and the automatic move must be exercised with
   no broker, no second process, and no waiting on real time.

## Options considered

### Option 1: a modular monolith with clear internal modules and one deployable unit

Four modules inside one process. Domain holds the entities and the transition
rules and references nothing. Application holds the use-case services, the DTOs,
the validators, and the repository and unit-of-work interfaces. Infrastructure
holds EF Core, the migrations, and the scheduler adapter. Api holds the
controllers, the error handler, dependency injection, and the health endpoints.
Dependencies point one way, from the edges to the core, per ADR-0001.

- Good: one deployable unit and one data store, so the local run is one command
  and there is one transaction boundary. The rules are testable in isolation.
  The module boundaries are the seam that would make an extraction local, and
  they cost little while the change stays small.
- Bad: the boundaries are held by discipline and the dependency direction, not
  by a network or a compiler. If a module reaches across a boundary, a later
  extraction gets harder. A single process is also one scaling unit, so one hot
  path scales the whole service.

### Option 2: microservices split by capability now

Split create, retrieve, list, status change, cancel, and the automatic move into
separately deployable services, each with its own boundary.

- Good: each capability can scale and deploy on its own, and a team can own one
  service without touching the others.
- Bad: it introduces network calls, partial failure, distributed transactions,
  and service discovery for a workload that does not need them. The order
  aggregate spans several operations, so a split turns one transaction into a
  saga. It fails drivers 1, 2, and 5, and buys boundary clarity (driver 3) at a
  cost the change cannot justify.

### Option 3: a distributed event-driven set of services now

Publish domain events to a broker and run the automatic move, notifications, and
analytics as consumers from day one, each as its own service.

- Good: decoupled consumers, and a natural path to independent scaling and
  deployment.
- Bad: a broker, an event schema, idempotent consumers, and eventual
  consistency are all infrastructure the current requirements do not ask for.
  The automatic move is a periodic sweep over PENDING orders, not a stream of
  events, and the notification and analytics consumers do not exist yet. It
  fails drivers 1, 2, and 5, and defers ordering, replay, and dead-letter work
  to a time when no consumer justifies it.

## Decision

Option 1, a modular monolith: one deployable unit with explicit module
boundaries and a one-way dependency direction. The four modules and the
direction are recorded in ADR-0001 and shown in DOC-0001. In C4 terms the
process is the container and the modules are its internal structure; the modules
are not independently deployable.

We defer microservices and distributed services. Neither independent scaling nor
an independent deployment cadence is required now: the whole service is one
workload run by one small team, and the automatic move is the only background
path. Extraction stays available as a future move, but it only becomes worth its
cost when a trigger fires. The triggers that would change this answer:

- Independent scaling. One capability needs a different scaling profile from the
  rest, for example reads that far outpace writes, or a processing backlog that
  outgrows one runner.
- Independent deployment cadence. One capability has to ship on a different
  schedule, so releasing it should not release the rest.
- Separate team ownership. A distinct team owns a capability and needs its own
  repository and release train.
- A bounded context that needs its own store. A capability develops its own data
  model and life, where sharing one store is no longer the simpler choice.

Until one of these is true, a split is cost without a buyer.

## Consequences

The change is simpler to build and run. One process, one store, and one
transaction boundary mean the local run is one command, and a write that
touches an order and its items commits once. The boundaries make the code easier
to navigate and keep the rules independent of the framework, the transport, and
the scheduler, which is what lets the domain tests run with no database.

The cost is that the boundaries are a convention, not a wall. If a module
reaches past its interface into another module's internals, or the dependency
direction is violated, the design quietly becomes a tangle and a later
extraction has to unpick it. We keep the rules honest by keeping the reference
direction one way and the interfaces small, and we treat a boundary violation as
a review finding rather than a style preference.

There is also one scaling unit. The service runs as more than one instance, and
the automatic move is safe across instances (ADR-0003), so horizontal scaling is
available without a split. What we give up is scaling one capability on its own.
That is acceptable while the whole service is the scale unit the requirement
names.

## Status

Proposed. The artifact frontmatter stays draft until G3 ratifies this decision,
at which point the MADR status becomes accepted.
