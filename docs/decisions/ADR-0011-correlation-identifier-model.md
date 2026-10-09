---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: ADR-0011
artifact_type: adr
title: "Adopt a correlation identifier model and request completion logging"
status: approved
stage: S3
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-10 }
gate: G3
sources: [NFR-0001, ADR-0005, ADR-0010]
jurisdiction: []
created: 2026-10-10
updated: 2026-10-10
superseded_by: null
---

# Adopt a correlation identifier model and request completion logging

## Context and problem statement

The service already pushed `HttpContext.TraceIdentifier` into the log as
`CorrelationId` and used it as the error `traceId`. That is a start, but a caller
could not send or read a correlation identifier, there was no request completion
event, and a background run had no shared identifier across its events. The
improvement brief asked for a small, explicit identifier model rather than a
second arbitrary ID.

## Decision drivers

1. A caller can supply and receive a correlation identifier across retries.
2. The existing error `traceId` meaning is preserved, not redefined.
3. Every request produces one completion event with method, path, status,
   elapsed, and the identifiers.
4. Every background run shares one identifier across its events.
5. Correlation values are never used for authorization, uniqueness, or identity.

## Options considered

### Option 1: Keep only the internal request identifier

Cheapest, but a caller cannot correlate a request with the service's logs, and a
background run stays opaque. Rejected.

### Option 2: Accept a caller correlation header and keep the request id distinct (chosen)

Read a single validated `X-Correlation-ID`, echo it (or a generated value) on the
response, keep `RequestId` as `TraceIdentifier` for the error `traceId`, add a
Serilog request completion event, and give each background run a
`BackgroundRunId` through an `ILogger` scope.

### Option 3: Replace the request identifier with the caller value

Rejected: a caller can reuse a value across retries, so it must not become the
unique request identity or the error `traceId`.

## Decision

Adopt Option 2.

- `CorrelationId`: a single `X-Correlation-ID` request header, validated to a
  nonempty value of at most 64 characters from letters, digits, `.`, `_`, and
  `-`. It is echoed in the `X-Correlation-ID` response header. A missing header
  gets a generated value; an invalid or repeated header is rejected with 400
  `VALIDATION_ERROR` and a freshly generated response header.
- `RequestId`: `HttpContext.TraceIdentifier`, distinct per request, still the
  error `traceId`.
- `BackgroundRunId`: a fresh identifier per job invocation, pushed as an
  `ILogger` scope by the job adapter so the processing service's events share it.
- A Serilog request completion event logs method, path, status, elapsed, and the
  identifiers, without query strings or bodies.
- The response header is set through `OnStarting`, because the exception handler
  clears response headers before writing an error body.

## Consequences

A caller can correlate a request with the logs by `CorrelationId`, and an
operator can correlate a background run by `BackgroundRunId`. The error `traceId`
keeps its meaning. The cost is a small middleware component and a validation
rule, both covered by tests. Correlation values are opaque: they are never used
for authorization, uniqueness, or metric labels.

## Status

Proposed. The artifact frontmatter stays `draft` until the gate ratifies this
decision, at which point the MADR status becomes accepted.
