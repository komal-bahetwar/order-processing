---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0005
artifact_type: adr
title: "Return one documented error shape with stable codes and meaningful status codes"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 2, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G3
sources: [PRD-0001, NFR-0001]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Return one documented error shape with stable codes and meaningful status codes

## Context and problem statement

The interface contract and error model budget in NFR-0001 requires one
machine-readable contract for every operation and one documented error shape
for every failure, with a stable code and a meaningful status. An integrator
should be able to tell a validation failure from a missing order from a
conflict by reading one shape, without endpoint-specific parsing. We have to
choose the error shape and where it is produced.

## Decision drivers

1. NFR-0001: one documented error shape with a stable code and a meaningful
   status, for validation failures, not-found cases, and conflicts alike.
2. The correlation identifier in NFR-0001 is carried on every error response,
   so a failure ties to the request that caused it.
3. The shape is part of the machine-readable contract, so contract tests can
   assert it for every operation.
4. The edge maps domain failures to the right status once, not per controller.
5. The stable code is decoupled from the human-readable message, so wording can
   change without breaking integrators.
6. Unexpected failures still return the same shape rather than a stack trace or
   a framework default.

## Options considered

### Option 1: per-endpoint ad hoc errors

Each endpoint returns whatever body is convenient, or an empty body with a
status code.

- Good: nothing shared to design.
- Bad: integrators cannot write one error handler, the shape drifts between
  endpoints, and contract tests have nothing stable to assert. It fails drivers
  1 and 3.

### Option 2: one shared error model surfaced through the interface contract

One error schema, based on RFC 7807 `ProblemDetails`, is defined in the OpenAPI
document and produced at the edge by centralized exception handling. The
schema carries `type`, `title`, `status`, `code`, `detail`, and `traceId`. The
`code` is the stable machine value from a fixed set; `detail` is the
human-readable explanation; `traceId` is the correlation identifier. Domain
exceptions and validation failures both flow through the same handler, which
chooses the status and the code.

- Good: one shape to parse, one handler to maintain, a stable code set, the
  correlation identifier in a fixed place, and a schema that the contract and
  contract tests share. It meets drivers 1 through 6.
- Bad: the handler has to know the domain exception codes, so the mapping is a
  small, deliberate coupling between the edge and the domain vocabulary.

### Option 3: generic framework errors with a blank 500

Let unexpected and even expected failures surface as a generic server error.

- Good: no mapping code.
- Bad: a missing order and a conflict become indistinguishable 500s, which
  fails driver 1 outright and hides real defects.

## Decision

Option 2. Every failure returns the shared error shape defined in the OpenAPI
contract. The stable codes are `VALIDATION_ERROR`, `INVALID_ID`,
`ORDER_NOT_FOUND`, `INVALID_ORDER_STATE`, `ORDER_NOT_CANCELLABLE`,
`CONCURRENCY_CONFLICT`, and `INTERNAL_ERROR`. The status mapping is:
validation and malformed id to 400, unknown order to 404, an illegal
transition or a cancel of a non-pending order to 409, a concurrency conflict to
409, and anything unexpected to 500. The correlation identifier is carried as
`traceId` on every error response and every log line, per NFR-0001.

A same-status request is not an error; it is a 200 no-op, recorded in ADR-0006.
That keeps the error set small and the codes meaningful.

## Consequences

Integrators handle one error shape, and the contract tests assert it on the
validation, not-found, and conflict paths for every operation. The stable code
set is a public interface: changing or removing a code is a contract change,
while `detail` wording can change freely.

The cost is a central mapping table that must stay in step with the domain
exception vocabulary. When a new domain rule is added, its code is added to the
table, the OpenAPI description, and the error-path tests in the same change.

## Status

Proposed. The artifact frontmatter stays `draft` until G3 ratifies this
decision, at which point the MADR status becomes accepted.
