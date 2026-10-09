---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: BRIEF-0001
artifact_type: brief
title: "Order intake and lifecycle for the online storefront"
status: approved                     # draft | in_review | approved | baselined | superseded | retired
stage: S0                            # producing stage
work_item: TKT-0001
# kind: human | agent. Agents record model; a human author drops the model field.
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
# Added as executor->reviewer rounds run:
#   - { kind: agent, name: reviewer-agent, rounds: 2, verdict: approved }
#   - { kind: human, name: "<...>", role: "<...>" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Head of Product", date: 2026-10-09 }
gate: G0                             # gate this artifact is approved at (if any)
sources: []                          # upstream artifact ids this derives from
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Order intake and lifecycle for the online storefront

## Problem

We are opening our storefront to customers and we have no dependable record of
the orders that come in. Orders arrive through whatever channel the customer
chose, and someone reworks each one by hand. A customer cannot be told where an
order stands, our fulfilment team cannot see which orders are ready to move, and
we keep no consistent history of what was ordered or what happened to it.
Mistakes surface late, when they are expensive to correct.

We need one place where an order is captured with everything the customer asked
for, followed through a small, agreed set of states, and closed out cleanly when
it ships or when the customer cancels. Nothing today does that.

Source: this brief records a request that came from the take-home assignment
brief at `docs/assignment.md`.

## Who it affects

- Customers. They place orders with the items they want, expect to look an order
  up, and expect to cancel it while it has not yet started moving.
- Fulfilment and operations. They need a trustworthy list of orders ready to
  process, and they need to record when an order ships and when it is delivered.
- Finance and operations. They rely on a consistent order record and a total per
  order for later reconciliation of what we sold.

The capabilities this touches are order intake, order lifecycle tracking, order
fulfilment handoff, and order cancellation.

## Why now

We are standing up the storefront now. If orders start arriving before we have a
single order record, every order becomes a manual exercise and our promises to
customers about status become guesses. Building this after launch costs more,
because we would be reconciling a backlog of inconsistent orders while new ones
keep arriving.

## Rough size

Medium (M). The work is one bounded workflow: capture an order, read it back,
list and filter it, move it through four states, cancel it while it is still
pending, and move orders that are still pending on to the next step
automatically. There are no external integrations, no customer accounts, and no
money movement. It is larger than a point fix because it introduces an order
lifecycle and must stay correct when two things try to change the same order at
once.

This is a tier 2 change: a significant domain workflow with a small set of
states and one automatic move. It carries no personal data, no payment data, and
no tenant boundary, so it does not rise to tier 3. For this tier-2 take-home,
the affected capabilities are identified in prose here and in the triage, and we
deliberately do not produce a separate capability-map delta artifact at S0; that
is a documented scope decision, not a silence.

## Functional requirements

These are the outcomes a customer or the business can observe. They state what
must hold and stay silent on how we build it.

- FR-1: A customer can place an order that contains one or more items, including
  several items in the same order, and the order is captured with those items and
  its total.
- FR-2: A customer can retrieve the full details of an order by its order id. An
  unknown id returns a clear not-found outcome.
- FR-3: A customer can list orders and narrow the list by status.
- FR-4: Every order has a status in the lifecycle PENDING, PROCESSING, SHIPPED,
  DELIVERED. A new order starts in PENDING, and it can be updated through the
  lifecycle in that order.
- FR-5: A customer can cancel an order only while it is PENDING. A cancellation
  attempt on an order that has moved on is rejected.
- FR-6: A PENDING order moves to PROCESSING automatically within about 5 minutes
  of becoming eligible, with no manual step, and orders that have already left
  PENDING are not affected by that automatic move.

## Success criteria

- An order created with two or more items is stored once and read back with all
  of its items and its total intact.
- An order can be retrieved by id, and requesting an id that does not exist
  returns a not-found result.
- Listing returns every order, and returns only the matching orders when a
  status filter is applied.
- An order begins in PENDING, reaches PROCESSING, then SHIPPED, then DELIVERED,
  and no other status change is accepted.
- A PENDING order can be cancelled, and a cancel attempt on an order in any
  later status is rejected.
- Every PENDING order reaches PROCESSING within about 5 minutes of becoming
  eligible, with no manual step, and orders that have already left PENDING are
  not affected by that automatic move.

## Non-goals

- Payments, refunds, and returns.
- Inventory reservation and stock checks.
- Customer accounts, login, and access control.
- Product catalogue and price management; an order line carries the price agreed
  at order time.
- Notifications by email or message.
- Delivery tracking beyond the SHIPPED and DELIVERED states.
- Reporting, analytics, and multi-tenant or multi-region operation.

## Regulatory and contractual drivers

None apply. This change handles no personal data and touches no regulated
activity, so we are not tagging a jurisdiction, and no filing or contractual
deadline drives the timing.

## Open questions

- Who moves an order from PROCESSING to SHIPPED and from SHIPPED to DELIVERED:
  the customer, our operations team, or an automatic step? Owner: Komal
  Bahetwar (candidate, interim PM). Due: before G1.
- What makes an order eligible to leave PENDING, and does a later status change
  ever need to be reversed? Owner: Komal Bahetwar (candidate, interim PM). Due:
  before G1.
- Should a customer be able to cancel part of an order, or only the whole order?
  Owner: Komal Bahetwar (candidate, interim PM). Due: before G1.
- What order volume and list size should we assume at launch, and does the list
  need a bound? Owner: Komal Bahetwar (candidate, interim PM). Due: before G1.
