---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0003
artifact_type: doc
title: "Order lifecycle, sequence diagrams, and reliability notes"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001, ADR-0002, ADR-0003, ADR-0006]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Order lifecycle, sequence diagrams, and reliability notes

This document is the behavioral view of the order processing service. It gives
the state model and the allowed transitions, then walks the critical flows as
sequence diagrams, including the race between a cancel and the automatic move.
It closes with the failure-mode and reliability rules that make the change safe
under retries and more than one instance. The structural view is in DOC-0001
and the persistence design is in DOC-0002.

## State model

An order holds exactly one status at a time. There are five states and two are
terminal.

```mermaid
stateDiagram-v2
    [*] --> PENDING : create order
    PENDING --> PROCESSING : automatic move
    PENDING --> CANCELLED : customer cancel
    PROCESSING --> SHIPPED : fulfilment and operations
    SHIPPED --> DELIVERED : fulfilment and operations
    DELIVERED --> [*]
    CANCELLED --> [*]
```

### Allowed transitions

| From | To | Trigger | Actor | Entry point |
|---|---|---|---|---|
| None | PENDING | Create an order | Customer | POST /api/orders |
| PENDING | PROCESSING | Automatic move | System, on behalf of fulfilment and operations | Scheduled recurring job |
| PENDING | CANCELLED | Cancel the whole order | Customer | POST /api/orders/{id}/cancel |
| PROCESSING | SHIPPED | Advance status | Fulfilment and operations | PATCH /api/orders/{id}/status |
| SHIPPED | DELIVERED | Advance status | Fulfilment and operations | PATCH /api/orders/{id}/status |
| Any | Same state | Repeat of the current status | Any caller | PATCH /api/orders/{id}/status |

Every other requested change is rejected with a 409 and the order keeps its
status. DELIVERED and CANCELLED are terminal: nothing moves out of them, and a
cancel on either is rejected. A same-status request is a 200 no-op, per
ADR-0006; it is not a transition and emits no lifecycle change.

The automatic move is the only path from PENDING to PROCESSING. We deliberately
do not expose that transition as a manual change: the status endpoint accepts
only PROCESSING to SHIPPED and SHIPPED to DELIVERED. The design context exposed
PENDING to PROCESSING for admin and testing, and we reject that option for now.
A manual request that targets PROCESSING from PENDING, or that targets PENDING
or CANCELLED at all, is rejected with a 409.

The status methods on the Order entity are the single authority for this table.
The application layer maps a requested target status to the named method and
lets the domain guard reject an illegal change. The controller never assigns a
status directly.

## Sequence diagrams

Participants are abbreviated: the client, the Api controller, the Application
use-case service, the Domain entity, the repository and unit of work, and
PostgreSQL.

### Create an order

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as OrdersController (Api)
    participant S as OrderService (Application)
    participant D as Order (Domain)
    participant R as IOrderRepository
    participant U as IUnitOfWork
    participant DB as PostgreSQL

    C->>API: POST /api/orders with items
    API->>API: validate request shape and ranges
    alt request invalid
        API-->>C: 400 with the shared error shape
    else request valid
        API->>S: CreateAsync(dto)
        S->>D: Order.Create(items)
        D->>D: require at least one item, quantity greater than zero, price not negative
        D->>D: derive TotalAmount from quantity times unit price, rounded half-up
        D-->>S: Order in PENDING
        S->>R: Add(order)
        S->>U: SaveChangesAsync
        U->>DB: INSERT order and items in one transaction
        DB-->>U: committed
        U-->>S: committed
        S-->>API: order
        API-->>C: 201 Created with Location and the order
    end
```

The total is never accepted from the caller. Validation at the edge rejects a
malformed request with a 400; the domain constructor enforces the same
invariants and is the backstop if the edge is bypassed.

### Retrieve an order by id

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as OrdersController (Api)
    participant S as OrderService (Application)
    participant R as IOrderRepository
    participant DB as PostgreSQL

    C->>API: GET /api/orders/{id}
    API->>API: validate the id is a uuid
    alt id malformed
        API-->>C: 400 INVALID_ID
    else id valid
        API->>S: GetAsync(id)
        S->>R: GetByIdAsync(id)
        R->>DB: SELECT order and items
        alt order found
            DB-->>R: order
            R-->>S: order
            S-->>API: order
            API-->>C: 200 with items, total, and status
        else order absent
            DB-->>R: no row
            R-->>S: null
            S-->>API: not found
            API-->>C: 404 ORDER_NOT_FOUND
        end
    end
```

Retrieval is read-only. It does not load the order for change, does not touch
the concurrency token, and never changes the order.

### List and filter orders

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as OrdersController (Api)
    participant S as OrderService (Application)
    participant R as IOrderRepository
    participant DB as PostgreSQL

    C->>API: GET /api/orders?status=...&limit=...
    API->>API: validate the status value and the limit
    alt filter or limit invalid
        API-->>C: 400 with the shared error shape
    else valid
        API->>S: ListAsync(status, limit)
        S->>S: clamp the limit to the default of 20, or the requested value capped at 100
        S->>R: GetAsync(status, limit)
        R->>DB: SELECT with the optional status predicate, order by created_at, then id
        DB-->>R: rows, possibly empty
        R-->>S: orders
        S-->>API: orders
        API-->>C: 200 with the list, empty when nothing matches
    end
```

The order is oldest first by creation time with id as the deterministic
tie-break, so repeated reads over unchanged data return the same sequence. The
bound is applied after the filter. A requested limit above 100 succeeds and is
clamped to 100, matching the contract; it is not a validation error.

### Advance order status

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as OrdersController (Api)
    participant S as OrderService (Application)
    participant D as Order (Domain)
    participant U as IUnitOfWork
    participant DB as PostgreSQL

    C->>API: PATCH /api/orders/{id}/status
    API->>API: validate the requested status value
    API->>S: UpdateStatusAsync(id, target)
    S->>U: load the order for change
    U->>DB: SELECT order and items
    alt order absent
        API-->>C: 404 ORDER_NOT_FOUND
    else order found and target equals current status
        S-->>API: order unchanged
        API-->>C: 200 no-op, no lifecycle change
    else target is the allowed next status
        S->>D: Ship() or Deliver()
        D->>D: guard the predecessor status, then change status and updated_at
        S->>U: SaveChangesAsync
        U->>DB: UPDATE ... AND version = @original
        alt row updated
            DB-->>U: one row
            API-->>C: 200 with the updated order
        else row changed by another writer
            DB-->>U: no row
            U-->>S: DbUpdateConcurrencyException
            API-->>C: 409 CONCURRENCY_CONFLICT
        end
    else change not allowed
        S->>D: attempt the requested change
        D-->>S: DomainException
        API-->>C: 409 INVALID_ORDER_STATE
    end
```

The same-status branch is checked before a domain method is called, so it never
trips the guard. Every lifecycle change emits one structured log line and one
transition counter, per the observability budget in NFR-0001.

### Cancel an order

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as OrdersController (Api)
    participant S as OrderService (Application)
    participant D as Order (Domain)
    participant U as IUnitOfWork
    participant DB as PostgreSQL

    C->>API: POST /api/orders/{id}/cancel
    API->>S: CancelAsync(id)
    S->>U: load the order for change
    U->>DB: SELECT order and items
    alt order absent
        API-->>C: 404 ORDER_NOT_FOUND
    else order is PENDING
        S->>D: Cancel()
        D->>D: guard PENDING, then set CANCELLED
        S->>U: SaveChangesAsync
        U->>DB: UPDATE ... AND version = @original
        alt row updated
            DB-->>U: one row
            API-->>C: 200 CANCELLED
        else row changed by another writer
            U-->>S: DbUpdateConcurrencyException
            API-->>C: 409 CONCURRENCY_CONFLICT
        end
    else order is not PENDING
        S->>D: Cancel()
        D-->>S: DomainException ORDER_NOT_CANCELLABLE
        API-->>C: 409 ORDER_NOT_CANCELLABLE
    end
```

Cancellation is whole-order only. A cancel can arrive while the automatic move
is running; the race is handled in the last diagram below.

### Automatic move from pending to processing

```mermaid
sequenceDiagram
    autonumber
    participant HF as Hangfire scheduler
    participant JOB as ProcessPendingOrdersJob (adapter)
    participant PS as OrderProcessingService (Application)
    participant D as Order (Domain)
    participant U as IUnitOfWork
    participant DB as PostgreSQL

    loop every configured interval, default five minutes
        HF->>JOB: run under a distributed lock
        JOB->>PS: ProcessPendingOrdersAsync(batchSize)
        PS->>U: load a bounded batch of PENDING orders
        U->>DB: SELECT ... WHERE status = PENDING ORDER BY created_at, id LIMIT batchSize
        DB-->>U: batch
        loop each order in the batch
            PS->>D: Process()
            D->>D: guard PENDING, then set PROCESSING
            alt transition applied
                PS->>PS: emit log line and transition counter
            else other change won
                PS->>PS: catch the concurrency conflict, log, continue
            end
        end
        PS->>U: SaveChangesAsync
        U->>DB: UPDATE each order AND version = @original
        DB-->>U: committed rows, or a conflict for a row another writer changed
        PS-->>JOB: processed count
    end
```

Eligibility is simply status equal to PENDING. An order that has left PENDING,
including one that is CANCELLED, is not selected and is not touched. The
processing rule is in Application and Domain, so the whole flow is exercised by
calling the service directly, with no scheduler and no waiting.

### The race: cancel versus automatic move

Both writers read the same PENDING order with the same version, 7 in this
example. Each update carries the version it read.

```mermaid
sequenceDiagram
    autonumber
    participant C as Customer request
    participant JOB as Automatic move
    participant DB as PostgreSQL (orders)

    C->>DB: SELECT order, version = 7, status = PENDING
    JOB->>DB: SELECT order, version = 7, status = PENDING
    C->>DB: UPDATE status = CANCELLED WHERE id AND version = 7
    DB-->>C: one row, version is now 8
    JOB->>DB: UPDATE status = PROCESSING WHERE id AND version = 7
    DB-->>JOB: no row, the stale version matches nothing
    Note over JOB: DbUpdateConcurrencyException, log, skip this order, continue the batch
    Note over C: the customer change stands, the order is CANCELLED and never both
```

Exactly one change applies. The winner is whichever committed first; the loser
reports a conflict on the request path (409) or is skipped by the automatic
move. The order never reflects both changes. This is the core case the
correctness under concurrent changes budget in NFR-0001 asks for, and the
mechanism is the optimistic token from ADR-0002.

## Failure modes and reliability

### Idempotency of the automatic move

The move is idempotent at three levels. The query selects only PENDING orders,
so an order it already moved is not selected again. The domain guard rejects
`Process()` unless the order is PENDING, so a reprocessing attempt on an order
that has moved on is a guard violation, not a second move. If two runs overlap,
the optimistic token makes the second write fail, and the loser is skipped. A
run over an unchanged set of orders therefore leaves the result identical to
the first run, which is what PRD-0001-S06 requires.

### Retries

The scheduler retries a failed recurring job with backoff, so a transient
database outage recovers without a manual step; the recurring schedule is
persisted in PostgreSQL, so a restart does not lose it. Retries are per job
run, not per order: a single contested or malformed order is caught, logged,
and skipped so it cannot fail the whole batch. On the request path there is no
silent retry. A change that loses a race reports 409, the change that is
illegal reports 409, and the caller decides what to do. This matches ADR-0002
and ADR-0006.

### Multi-instance safety

The service holds no request state in a single instance, so any instance can
serve any request. The automatic move is coordinated twice: Hangfire takes a
distributed lock per recurring job, so only one instance runs a given tick, and
the optimistic token makes each per-order update safe even if two runs overlap
across the lock boundary. Migrations run under a database advisory lock at
startup, so concurrent starts do not race the schema. Together these meet the
at-most-once and horizontal scalability budgets in NFR-0001, and the
arrangement is recorded in ADR-0003.

### Other failure paths

- Order not found: 404, and nothing is changed.
- Malformed id or request: 400 before any domain call.
- Illegal transition or cancel of a non-pending order: 409, and the order is
  untouched.
- Same-status repeat: 200 no-op, logged as a request but not as a lifecycle
  change.
- Duplicate submission of a create or a cancel is not suppressed in this
  change; a repeated create makes a second order and a repeated cancel after
  the order has moved returns 409. This is a known limitation recorded in
  ADR-0006, and a future idempotency key is the planned remedy.
- Database unavailable at startup: readiness stays not-ready until the store is
  reachable, so a caller is not routed to an instance that cannot serve.

## Related artifacts

The structural view is DOC-0001, the persistence design is DOC-0002, the
interface contract is `openapi/order-processing.yaml`, and the test coverage
for these flows is TP-0001.
