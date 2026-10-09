# Future phases

This document holds extensions deliberately kept out of the current baseline.
Each one moves into a new PRD or NFR delta when its trigger fires. Nothing here
is committed scope, and nothing here has been estimated or scheduled. Item-level
order status is the first functional example.

## Functional requirement extensions

The ten functional requirements in PRD-0001 are the current baseline. This table
records how each could grow, and what would make us start.

| Requirement (as it stands now) | Phase 2 | Phase 3 | Trigger |
|---|---|---|---|
| FR-1, create an order with one or more items | Product catalogue and server-side pricing, so the unit price is not caller-supplied | Per-channel price lists and buyer-specific pricing | A catalogue exists and the agreed price should come from it |
| FR-2, reject an order with no items or bad values | Validate items against the catalogue and available stock, not only shape and range | Rule-driven validation per product type | Callers start submitting products we do not sell |
| FR-3, derive the total from quantity times unit price | Tax and discounts in the total, with a documented allocation and rounding rule | Multi-currency totals with a stored exchange basis | Finance needs a tax-inclusive or discounted total |
| FR-4, retrieve an order by id | Serve per-item and per-fulfilment-unit status in the order read | Buyer-scoped reads, so a caller sees only their own orders | Customers ask to track part of an order separately |
| FR-5, list and filter orders by status | Richer listing and search across date, buyer, item, and status | Full-text search, saved filters, and cursor pagination | Support cannot find an order from the list as it stands |
| FR-6, hold one status from a five-state lifecycle | Item-level status, so an order with three products carries status per item or per fulfilment unit, not only per order; a returned or reversal status | Configurable lifecycles per product type | Part of an order is fulfilled, returned, or reversed on its own |
| FR-7, cancel a whole order while PENDING | Partial cancellation and refund on cancel | Buyer or role ownership of lifecycle actions, so only the buyer or an entitled role may cancel | Customers cancel part of an order, or cancel after payment |
| FR-8, move PENDING to PROCESSING automatically | Notifications on status change | Partial fulfilment and split shipment driven by availability | Customers ask why they were not told an order moved |
| FR-9, treat a same-status request as a no-op | Idempotency keys for repeated submissions | Idempotency across channels with a shared key store | Clients retry and we cannot tell a retry from a new request |
| FR-10, apply at most one of two concurrent changes | Conflict handling extended to item-level changes | A client-visible retry contract for losing changes | Item-level changes can race on the same order |

## Non-functional requirement extensions

The rows below match the NFR-0001 delta. Each states where the current budget
could move and what would justify the move.

| Requirement (as it stands now) | Phase 2 | Phase 3 | Trigger |
|---|---|---|---|
| Peak request volume | Higher sustained and burst targets once real telemetry replaces the launch assumption | Multi-region peak with regional routing | The first months of telemetry show the launch assumption is wrong |
| Read and list latency | Tighter p95 and p99 targets under the higher peak | Read replicas and a cache in front of the read path | Read traffic grows faster than write traffic |
| Write latency (create, status change, cancel) | Tighter write targets | A queue in front of writes for bursty periods | Write peaks exceed what one data store absorbs |
| Availability | 99.9% monthly | Multi-region with a higher target and a measured recovery objective | A single-region outage costs more than a second region |
| Automatic-transition timeliness | Keep the about-five-minute bound at a larger pending backlog | Queued or sharded processing so the bound holds as volume grows | The pending backlog outgrows one processing pass |
| Correctness under concurrent changes | Pessimistic or queued handling at higher volume, where optimistic retries stop being enough | Conflict-free handling at item level | Conflict rates rise with volume, or item-level changes arrive |
| Testability | Keep the one-command suite inside its time bound as the suite grows | Parallel and selective test execution | The suite approaches the ten-minute bound |
| Observability | Distributed trace export, plus SLO dashboards and alerts at S7 | Cross-service trace correlation and error-budget burn alerts | More than one service sits in the request path, or trace export is needed to diagnose an incident |
| Horizontal scalability | Job sharding, read replicas, caching, and partitioning | Regional partitioning | Sustained load exceeds what one data store plus one job runner holds |
| Interface contract and error model | Contract versioning, an idempotency-key header, and rate limiting | A deprecation policy and consumer-driven contract negotiation | External integrators build against the contract, or a client floods it |
| Operability | Blue/green or rolling deploys, backups and disaster recovery, zero-downtime migrations | Multi-region failover and routine restoration drills | The service has an availability target that a restart would breach |
| List result bound | Cursor pagination beyond the maximum on request | Client-tunable page sizes with server caps | Callers need to page past 100 results |
| Data residency | Multi-region, or personal data if accounts are added | Region-pinned storage with jurisdictional routing | Customer accounts are added, or a client requires data to stay in a region |

## Architecture, integration, and deployment extensions

The rows below change the shape of the system rather than a single requirement.
Each states where it could go and what would start it. Like the sections above,
nothing here is committed scope, and each moves into a new PRD or NFR delta when
its trigger fires.

| Concern | Phase 2 | Phase 3 | Trigger |
|---|---|---|---|
| Service boundaries | Extract one capability, such as the automatic move or the read path, into its own deployable service behind the existing interface | Split further by capability, with each service owning its data and release cycle | Independent scaling, an independent deployment cadence, separate team ownership, or a bounded context that needs its own store |
| Domain events and messaging | Publish order created, status changed, and cancelled events through a transactional outbox to a message bus or queue | Notification, analytics, and fulfilment consumers subscribe to the events instead of calling or reading the order service | A consumer appears that should not depend on the order service directly |
| Managed database | Move the store to a managed PostgreSQL service with automated backups | Managed high availability with a standby and automated failover | The service is operated beyond a local environment, or the store has to survive a host failure |
| Container packaging and orchestration | Package the service as a container image and run it on an orchestrator | Autoscaling and declarative environment management | The service runs anywhere other than the local compose environment |
| Blue/green or rolling deployment with automated rollback | Deploy without downtime using rolling or blue/green, with automatic rollback on a failed health check | Canary releases with progressive traffic shifting | The service has an availability target that a restart would breach |
| Backups and disaster recovery | Scheduled backups and a tested restore | A measured recovery objective, proven by routine restore drills | The service holds data that cannot be regenerated, or a recovery time is promised |
| Read replicas and caching | Serve read and list traffic from read replicas | Add a cache in front of the hot read path | Read traffic grows faster than write traffic |
| Multi-region operation | Run in more than one region with regional routing | Region-pinned storage with jurisdictional routing | A residency obligation or an availability target demands a second region |

## Sequencing

Phases here are sequenced by trigger, not by date. A phase starts when its
trigger fires and the work moves into a new PRD or NFR delta, which then runs the
normal lifecycle and its gates. Until that happens, the extension stays in this
document and out of committed scope.
