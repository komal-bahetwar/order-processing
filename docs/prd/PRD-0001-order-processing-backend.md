---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: PRD-0001
artifact_type: prd                   # fixed for this template
title: "Order processing backend"
status: approved                     # draft | in_review | approved | baselined | superseded | retired
stage: S1                            # producing stage
work_item: TKT-0001
# kind: human | agent. Agents record model; a human author drops the model field.
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
# Added as executor->reviewer rounds run:
#   - { kind: agent, name: reviewer-agent, rounds: 2, verdict: approved }
#   - { kind: human, name: "<...>", role: "<...>" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 3, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "PM", date: 2026-10-09 }
gate: G1                             # gate this artifact is approved at (if any)
sources: [BRIEF-0001]                # the approved brief this derives from
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Order processing backend

## Problem statement

We are opening our storefront and we have no dependable record of the orders
that come in. Orders arrive through whatever channel the customer chose, and
someone reworks each one by hand. A customer cannot be told where an order
stands, fulfilment cannot see which orders are ready to move, and we keep no
consistent history of what was ordered or what happened to it. Mistakes surface
late, when they are expensive to correct.

The cost of waiting is concrete. Every order becomes a manual exercise, our
promises to customers about status become guesses, and standing the capability
up after launch means reconciling a backlog of inconsistent orders while new
ones keep arriving. We need one dependable record per order, a small agreed set
of states, and clear outcomes for the operations a customer or our operations
team can perform on it.

## Goals

- Give customers, fulfilment and operations, and finance one dependable record
  of each order, from the moment it is placed to the moment it closes.
- Follow every order through a small, agreed set of states so a status question
  has one answer we can trust.
- Keep an order correct when two changes reach it at the same time, so no change
  is silently lost.
- State every requirement as an outcome we can verify, so delivery and testing
  can prove the product does what this document says.

## Non-goals

- Payments, refunds, and returns.
- Inventory reservation and stock checks.
- Customer accounts, login, and access control. We name who performs each
  lifecycle action, but enforcing that only that actor may act is out of scope.
- Product catalogue and price management. An order item carries the price agreed
  at order time.
- Notifications by email or message.
- Delivery tracking beyond the SHIPPED and DELIVERED states.
- Reporting, analytics, and multi-tenant or multi-region operation.
- Partial cancellation. A customer cancels a whole order or nothing.

## Personas and users

- Customer. Places an order with the items they want, looks an order up, and
  cancels it while it has not yet started moving.
- Fulfilment and operations. Work from a trustworthy list of orders ready to
  process, and record when an order ships and when it is delivered.
- Finance and operations. Rely on a consistent order record and a total per
  order for later reconciliation of what we sold.

## Flows

This change is delivered through a programmatic interface. Wireframes and a coded prototype
(A1.6) are out of scope, so the flows below describe business operations and
their outcomes rather than screens.

### Place order

A customer submits an order with one or more items. The order is captured once,
with its items and its derived total, and it starts in PENDING. A submission
with no items, or with an item whose quantity is not greater than zero or whose
unit price is negative, is rejected and nothing is captured.

### View order

A customer asks for an order by its id and sees the order, its items, its total,
and its current status. An id we do not hold returns a not-found outcome.

### List and filter orders

A customer asks for the list of orders and may narrow it to one status. The list
is ordered oldest first by creation time, with a deterministic tie-break, so
repeated requests over the same data return the same sequence. The brief asks
whether the list needs a bound; the answer is in NFR-0001.

### Advance order status

Fulfilment and operations move an order from PROCESSING to SHIPPED and from
SHIPPED to DELIVERED. A request that would move an order in any other way is
rejected and leaves the order unchanged. A request to set the status an order
already holds is accepted and changes nothing.

### Cancel an order

A customer cancels an order while it is PENDING, moving it to CANCELLED. A
cancel request on an order in any other status is rejected and leaves the order
unchanged. Cancellation applies to the whole order.

### Automatic move from pending to processing

Any order that is PENDING becomes PROCESSING within about 5 minutes, with no
manual step. Orders that have already left PENDING are not touched. The brief
asks what makes an order eligible to leave PENDING; eligibility is simply status
= PENDING.

## Functional requirements

Each requirement is an outcome a customer or the business can observe, and each
is testable on its own.

- FR-1: A customer can create an order that contains one or more items. Each
  item records the product, a quantity greater than zero, and the unit price
  agreed at order time. An order with a single item and an order with several
  distinct items are both accepted.
- FR-2: Order creation rejects an order that has no items, and rejects an order
  in which any item has a quantity of zero or less or a negative unit price. A
  rejected order is not captured, and the response identifies which rule failed.
- FR-3: The total of an order equals the sum over its items of quantity
  multiplied by unit price. The total is derived from the items and is never
  supplied by the caller; the total read back with an order always equals that
  sum.
- FR-4: A customer can retrieve an order by its order id and receives the
  order's items and total along with its status. A request for an id that is not
  known returns a not-found outcome, distinguishable from a successful
  retrieval.
- FR-5: A customer can list orders and can narrow the list to a single status.
  The list is returned oldest first by creation time, ties broken
  deterministically so the same set of orders always yields the same sequence.
  A filter that matches nothing returns an empty list.
- FR-6: Every order holds exactly one status in the lifecycle PENDING,
  PROCESSING, SHIPPED, DELIVERED, CANCELLED. A new order starts in PENDING. The
  accepted changes are PENDING to PROCESSING automatically (FR-8), PENDING to
  CANCELLED by the customer (FR-7), PROCESSING to SHIPPED on a request from
  fulfilment and operations, and SHIPPED to DELIVERED on a request from
  fulfilment and operations. Any other requested change is rejected and leaves
  the order's status unchanged.
- FR-7: A customer can cancel an order only while its status is PENDING, moving
  it to CANCELLED. A cancel request on an order in any other status is rejected
  and the order keeps its status.
- FR-8: A PENDING order becomes PROCESSING automatically within about 5
  minutes of the moment it becomes PENDING, with no manual step. An order that
  has already left PENDING, including one that is CANCELLED, is not affected.
- FR-9: A request to set an order to the status it already holds is accepted,
  returns the order unchanged, and creates no additional change. A repeated
  identical request produces the same result as the first.
- FR-10: When two changes to the same order are submitted at the same time, at
  most one succeeds. The change that does not win reports a conflict outcome and
  does not overwrite the winner's result, so an order never reflects both
  changes.

## Success criteria

These are the measurable conditions the S8 review will score. Each maps to the
brief's criteria.

- An order created with two or more items is captured once and read back with
  all of its items and a total equal to the sum of quantity times unit price.
- Order creation with no items, with a quantity of zero or less, or with a
  negative unit price is rejected, and no order is captured.
- Retrieval by id returns the order, and retrieval of an unknown id returns a
  not-found outcome.
- Listing returns every order, and applying a status filter returns exactly the
  orders in that status; the sequence is stable across repeated requests.
- Every order begins in PENDING, and the transitions PENDING to PROCESSING,
  PROCESSING to SHIPPED, and SHIPPED to DELIVERED are accepted; every other
  transition is rejected.
- A PENDING order can be cancelled, and a cancel request on an order in any
  later status is rejected.
- Every order that is PENDING reaches PROCESSING within about 5 minutes
  without a manual step, and orders that have already left PENDING are
  unaffected.
- A same-status request is accepted with no change, and when two changes race on
  one order exactly one is applied and the other reports a conflict.

## Non-functional requirements

The quantified budgets for this change are in the NFR spec delta, NFR-0001. The
budgets this change touches are:

- Volume, on a stated launch-scale assumption.
- Read and list latency, and write latency, as p95 and p99 targets at the
  assumed peak.
- Availability, as a monthly target.
- Automatic-transition timeliness, within about 5 minutes.
- Correctness under concurrent changes, so at most one of two racing changes is
  applied.
- Testability, a single command with no multi-minute waits.
- Observability through structured logs, metrics, and health checks.
- Horizontal scalability, adding capacity adds throughput while the automatic move still applies at most once per order when more than one instance runs.
- Interface contract and error model, one machine-readable contract for every operation and one documented error shape for every failure.
- Operability, the service and its data store start with one command on a clean machine.
- The list result bound, at most 20 by default and at most 100 on request.
- Residency, single region with no personal data.

NFR-0001 carries the numbers, the baseline statement, and how each budget is
verified at G5.

## Regulatory requirements

No regulatory requirements matrix (A1.3) is produced for this change. The system
handles no personal data and touches no regulated activity, and no filing or
contractual deadline applies. No jurisdiction applies, so no jurisdiction tags
are set. The same position is recorded in BRIEF-0001 and the S0 triage record.

## Out of scope

- Payments, refunds, and returns.
- Inventory reservation and stock checks.
- Customer accounts, authentication, login, and access control. This document
  names which actor performs each lifecycle action; enforcing that boundary is
  out of scope.
- Product catalogue and price management.
- Notifications by email or message.
- Delivery tracking beyond the SHIPPED and DELIVERED states.
- Reporting and analytics.
- Multi-tenant and multi-region operation.
- Partial cancellation; cancel applies to a whole order only.
- Wireframes and a coded prototype (A1.6). The programmatic interface is the
  delivery surface for this change.

### Future phases

Extensions deliberately kept out of this baseline are recorded in
[FUTURE_PHASES.md](../FUTURE_PHASES.md), which holds the functional and
non-functional extensions and the trigger that moves each into a new PRD or NFR
delta. Item-level order status, an order that carries status per item or per
fulfilment unit rather than only per order, is the first example.

## Open questions

The open questions for this baseline are listed in OQ-0001, each with an owner
and a due date. The four questions carried from BRIEF-0001 are closed there,
with the resolution and the artifact that now holds the answer. Two new
questions remain open, both with an owner and a due date.
