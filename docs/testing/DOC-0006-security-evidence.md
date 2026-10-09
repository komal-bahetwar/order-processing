---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0006
artifact_type: evidence
title: "Security evidence for the order processing backend"
status: approved
stage: S5
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Security Lead", date: 2026-10-09 }
gate: G5
sources: [NFR-0001, ADR-0005, DOC-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Security evidence for the order processing backend

This is the S5 security evidence for the order processing backend. It states the
trust boundary, the controls we verified, the dependency review, and the gaps
recorded as debt. It is a draft for the QA Lead at G5 and it does not record a
gate decision.

## Trust boundary

All HTTP input is untrusted. Every request is validated at the edge in the Api
module before any domain call, and the domain constructor repeats the invariants
as a backstop, so a request that bypasses the edge is still rejected. The
service exposes five operations: POST /api/orders, GET /api/orders/{id},
GET /api/orders, PATCH /api/orders/{id}/status, and POST /api/orders/{id}/cancel.

There is no authentication or authorization surface, no personal data, and no
tenant boundary in this change. The scope in PRD-0001 names who performs each
lifecycle action but does not enforce it, and the data set holds orders and order
items only.

## No threat model, and why

No threat model was produced, and that is the deliberate G3 decision recorded in
DOC-0001. The STRIDE-per-element exercise has no authentication elements, no
sensitive data, and no cross-tenant path to threaten, so it would have produced a
document without a subject. DOC-0001 records the two authorization points that
are named and not implemented: the administrative status change would carry a
fulfilment policy, and cancel would require the caller to own the order. Adding
either one is the trigger to produce a threat model and a revised authorization
design. This evidence relies on that decision rather than repeating it.

## Data access

All data access goes through EF Core 10 with Npgsql. Queries are parameterized,
and there is no string-concatenated SQL in the Infrastructure or Application
code. The repository interface exposes no IQueryable and no EF Core types, so the
use cases cannot build an arbitrary query. A source review found no raw SQL
execution.

## Secrets

No secrets are committed. The database password in docker-compose.yml and
appsettings.Development.json is the local development default, postgres, and is
not a production credential. In a real deployment the connection string must
come from the environment, and the committed local default is recorded as debt
below. The repository holds no API keys, tokens, or certificates.

## Error responses

Error responses expose a stable code, a correlation identifier as traceId, a
human-readable detail, and a status. ADR-0005 defines the shape and the seven
stable codes: VALIDATION_ERROR, INVALID_ID, ORDER_NOT_FOUND,
INVALID_ORDER_STATE, ORDER_NOT_CANCELLABLE, CONCURRENCY_CONFLICT, and
INTERNAL_ERROR. Responses do not expose stack traces, exception type names,
internal paths, or database details. Unexpected failures return 500 with
INTERNAL_ERROR and a fixed detail string, so an internal failure reads the same
as any other failure to a caller.

## Dependency review

A dependency review during S4 found one issue. Hangfire pulled a vulnerable
transitive Newtonsoft.Json 11.0.1. It was pinned to 13.0.4 in the API and
Infrastructure projects, and the build was verified clean afterward. No other
known vulnerable dependency was found in the manual review.

## Gaps recorded as debt

- An automated dependency-vulnerability scan is not wired in this environment.
  The S4 review was manual.
- An SBOM is not generated in this environment, so there is no machine-readable
  dependency inventory for the release candidate.
- The local development database password is a committed default and must come
  from the environment in a real deployment.

Each gap is a process control rather than a product behavior, so none of them
changes the functional outcome the stories promise. They are carried into the
debt list in DOC-0005 and are for the QA Lead to weigh at G5.
