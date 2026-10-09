---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0008
artifact_type: change-record
title: "Change record for the order processing backend"
status: approved
stage: S6
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "EM", date: 2026-10-09 }
gate: G6
sources: [PRD-0001, PLAN-0001, DOC-0005, DOC-0006, DOC-0007, TP-0002]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Change record for the order processing backend

This is the S6 change record for the order processing backend, the A6.1 artifact
for the G6 packet. It states what changed and why, the risk class, the G5
evidence, the deployment scope and the run we actually did, the commit
reference, and the rollback summary. It is a draft for the Engineering Manager.
No agent has approved it, and the EM records the gate decision.

## What changed and why

We shipped the order processing backend as a new service. It captures an order
with one or more items and derives the order total from them, retrieves an order
by id, lists and filters orders by status with a bounded result, advances an
order through the manual lifecycle steps, cancels a pending order, and moves
pending orders to processing on a schedule. The stack is ASP.NET Core on .NET
10, EF Core 10 with Npgsql, PostgreSQL 18, and Hangfire for the scheduled move.
The service is a modular monolith with the request path and the scheduler in one
process, and it applies its database migrations at startup.

The why is in PRD-0001. Orders were being reworked by hand, no one could say
where an order stood, and there was no consistent history of what was ordered or
what happened to it. This change gives customers, fulfilment, and finance one
dependable record per order and a small agreed status set. PLAN-0001 sequences
the eleven stories into one increment.

## Risk class

Normal. The change took the full S0 to S6 path and every gate along the way,
which is the definition of a normal change. It is not a standard change, because
it is not a pre-approved, repeatable, low-risk operation with an abbreviated
path. It is not an emergency change, because no production incident forced it
and there is no retroactive reconstruction to do.

The risk tier is 2, a significant domain workflow, per the artifact catalog. The
service handles no personal data, has no authentication or authorization
surface, and has no tenant boundary in scope, so the S1 decision to produce no
regulatory matrix applies and is recorded in PRD-0001. The S3 decision to
produce no threat model is recorded in the C4 views, DOC-0001, where the G3
decision is recorded, and in the security evidence, DOC-0006.

## Evidence

The G5 packet is linked here so an auditor can replay the change without
interviewing anyone.

- TP-0002, the test plan for the release candidate.
- DOC-0005, the test report: 45 tests pass with no failures (19 domain unit, 5
  architecture, 21 integration against a real PostgreSQL 18 container), and the
  build is clean with 0 warnings and 0 errors.
- DOC-0006, the security evidence: a manual dependency review that pinned the
  vulnerable transitive Newtonsoft.Json to 13.0.4, no committed secrets, and the
  shared error shape that exposes no stack traces or database details.
- DOC-0007, the UAT sign-off: the PM accepted UAT on 2026-10-09, with FR-1 to
  FR-10 all passing against the evidence in DOC-0005.

Four non-functional budgets are not measured by this run: peak request volume,
read and list latency, write latency, and availability. The Architect and the
Engineering Manager granted a waiver for all four on 2026-10-09, recorded in
DOC-0005 and reflected in DOC-0007. The rationale is that these are launch-scale
assumptions with no production load to measure against, and the functional and
architecture evidence is green. The budgets stay tracked and must be measured
once the service runs under load.

## Deployment scope

For this take-home, production is the local Docker Compose environment. There is
no cloud environment, no orchestrator, no managed database, and no blue/green or
rolling deployment. The scope is the `order processing` stack in
`docker-compose.yml`: PostgreSQL 18 on `postgres:18-alpine`, and the API
container built from the `Dockerfile` on the .NET 10 runtime image. The database
comes up first and must be healthy, then the API starts, and the API applies its
migrations at startup before it reports ready.

## Deployment evidence

The deployment was run on 2026-10-09. The observed result was:

- `docker compose up --build` brought up PostgreSQL 18, which reported healthy,
  and then the API container.
- `GET /health/ready` returned 200, so the API reported ready with its data
  store reachable.
- A created order returned `{"status":"PENDING","totalAmount":450.00}`.
- The list endpoint returned that order.
- Migrations applied on startup, with no manual step.
- The API host port and the database host port are overridable: `API_PORT`
  (default 8080) and `POSTGRES_PORT` (default 5432).
- A missing `libgssapi_krb5` in the runtime image was fixed in the Dockerfile,
  which now installs `libgssapi-krb5-2`. The startup log is clean afterwards.

## Commit reference

The work landed on the branch `feature/order-processing` and was merged to
`main`. The merge commit is `6a8d578`, "merge(s4): order processing service,
tests, migration, local run (G4 approved)". The change record, manifest,
rollback plan, and release notes are not part of that code merge; they are the
S6 packet built on top of it.

## Rollback summary

The rollback plan is DOC-0010. In short: stop the API container, redeploy the
previous image or revert the compose change, and re-run the previous commit.
Migrations are forward-only, so a rollback of the service does not reverse the
schema; a schema defect is corrected with a new compensation migration. The
local data volume can be reset with `docker compose down -v`. The plan was
exercised as a `docker compose down` and `up` cycle during the deployment check,
and a full previous-version redeploy is written as a documented step for a real
environment.

## Client communications

No client communications are required, so A6.5 does not apply. The change is not
client-facing, and there is no MSA with any customer, so there is no material-change
notice to send.
