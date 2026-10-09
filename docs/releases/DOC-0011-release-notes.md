---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0011
artifact_type: release-notes
title: "Release notes for the order processing API"
status: approved
stage: S6
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G6
sources: [DOC-0008, PRD-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Release notes for the order processing API

These are the S6 release notes for the order processing API, the A6.4 artifact
for the G6 packet, written for the API consumer. This is the first release of
the service, so there is no earlier version to upgrade from. It is a draft for
the Product Manager and records no approval. The change and its evidence are in
DOC-0008, and the requirements are in PRD-0001.

## Endpoints and behavior

The base path is `/api/orders`. The base URL in the local Docker Compose
environment is `http://localhost:8080`. The machine-readable contract is
`openapi/order-processing.yaml`.

| Method | Path | Behavior |
|---|---|---|
| POST | `/api/orders` | Creates an order with one or more items, derives the total, starts the order in PENDING, and returns 201 with a `Location` header. A request with no items, a quantity of zero or less, or a negative unit price is rejected and no order is stored. |
| GET | `/api/orders/{id}` | Returns the order, its items, its total, and its status. An unknown id returns 404. Retrieval never changes the order. |
| GET | `/api/orders?status=&limit=` | Lists orders oldest first by creation time, with a deterministic tie-break. The optional `status` filter returns only that status. The optional `limit` defaults to 20 and is capped at 100, so a value over 100 is clamped, not rejected. A filter that matches nothing returns an empty array. |
| PATCH | `/api/orders/{id}/status` | Advances an order from PROCESSING to SHIPPED or from SHIPPED to DELIVERED. Setting the status the order already holds is accepted as a 200 no-op. Any other change returns 409 and leaves the order unchanged. |
| POST | `/api/orders/{id}/cancel` | Cancels a PENDING order, moving it to CANCELLED. A cancel on an order in any other status returns 409 and leaves it unchanged. Cancellation applies to the whole order; partial cancellation is not supported. |

## Status lifecycle

A new order starts in PENDING. From there:

- PENDING becomes PROCESSING automatically within about five minutes, with no
  manual step.
- PENDING can be cancelled by the customer, moving it to CANCELLED.
- PROCESSING moves to SHIPPED, and SHIPPED moves to DELIVERED, on request.
- DELIVERED and CANCELLED are terminal.

Every order holds exactly one status. The manual request operations only ever
move PROCESSING to SHIPPED and SHIPPED to DELIVERED; every other requested
change is rejected.

## Error shape

Every failure returns one shape with the same fields, so a consumer writes one
error handler. The fields are `type`, `title`, `status`, `code`, `detail`, and
`traceId`. `code` is the stable, machine-readable value; `detail` is
human-readable and may change without notice. `traceId` is a correlation
identifier that also appears on the service log line for the request, so a
consumer can quote it when reporting a problem.

The stable codes are `VALIDATION_ERROR` (400), `INVALID_ID` (400),
`ORDER_NOT_FOUND` (404), `INVALID_ORDER_STATE` (409), `ORDER_NOT_CANCELLABLE`
(409), `CONCURRENCY_CONFLICT` (409), and `INTERNAL_ERROR` (500). Responses never
carry a stack trace or database detail. An unexpected failure returns 500 with
`INTERNAL_ERROR` and a fixed detail string, so it is not distinguishable from
the outside from any other internal failure.

## Money handling

Amounts are decimals with two places. A line total is the quantity multiplied by
the unit price, rounded half-up to two decimals. The order total is the sum of
the two-decimal line totals. The total is derived from the items, and a
caller-supplied total is ignored. A unit price with more than two decimal places
is rounded half-up to two decimals before the line is computed.

## Known limitations

These are carried as debt. None of them changes the functional behavior the
endpoints above promise, and all of them are tracked for a later phase.

- The performance and availability budgets are waived. Peak request volume, read
  and list latency, write latency, and availability were not measured for this
  release. The Architect and the Engineering Manager granted the waiver on
  2026-10-09, and the budgets must be measured once the service runs under load.
- The observability assertions are owed. The per-transition log line, the
  transition counter, the correlation identifier, and the health-check timing
  are implemented, but no automated test asserts them yet.
- Automated dependency-vulnerability scanning and an SBOM are not wired. The
  dependency review for this release was manual.
- The local development database password is a committed default. A real
  deployment must supply the connection string from the environment.
- There is no authentication or authorization. The API names who performs each
  lifecycle action in PRD-0001, but it does not enforce that only that actor may
  act.
- The list endpoint supports a `limit` bound only. There is no offset or cursor
  pagination, so a consumer that needs to walk more than 100 orders makes
  repeated filtered calls.

## Compatibility

This is the first release, version 1.0.0 in the contract. There is no prior
version and no deprecation to announce. The API is single-region and holds no
personal data, consistent with the residency position in PRD-0001.
