---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: NFR-0001
artifact_type: nfr                   # fixed for this template
title: "Order processing backend NFR delta"
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
approver: { kind: human, name: "Komal Bahetwar", role: "Architect", date: 2026-10-09 }
gate: G1                             # gate this artifact is approved at (if any)
sources: [PRD-0001]                  # the PRD whose non-functional scope this quantifies
jurisdiction: []                     # [SG, IN, ...] when regulatory-relevant; delete if none
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Order processing backend NFR delta

## Baseline reference

There is no prior platform NFR baseline for this new service, so this delta sets
the initial quantified budgets rather than changing existing ones. Every target
below is a first assignment of a budget, not a comparison with an earlier
version. The numbers fit a launch-scale system and are expected to be revised
once real telemetry exists.

## Delta

| NFR | Baseline | New target | How verified at G5 |
|---|---|---|---|
| Peak request volume | None, new service | Launch-scale assumption: 10 requests per second sustained peak, bursting to 25 requests per second for 60 seconds | A load test drives the assumed peak and the burst, sustained for 30 minutes, and reports error rate and latency |
| Read and list latency | None, new service | p95 under 300 ms and p99 under 800 ms at the assumed peak | A performance test at the assumed peak reports p95 and p99 |
| Write latency (create, status change, cancel) | None, new service | p95 under 500 ms and p99 under 1000 ms at the assumed peak | A performance test at the assumed peak reports p95 and p99 |
| Availability | None, new service | 99.5% monthly availability for order operations | A monthly availability review against recorded health and error data, with an alerting threshold set below the target |
| Automatic-transition timeliness | None, new service | Every order that is PENDING becomes PROCESSING within about 5 minutes (end to end, from becoming eligible) | A controlled test exercises eligibility without a real-time wait; a sampled timing check on the release candidate confirms every sample is within about 5 minutes |
| Correctness under concurrent changes | None, new service | When two changes race on one order, exactly one is applied and the other reports a conflict; no change is lost | A concurrent test submits two competing changes and asserts exactly one succeeds and the order reflects only that change |
| Testability | None, new service | The full automated suite runs in one command and completes in under 10 minutes; no test waits on real time for more than 5 seconds | A CI run records the suite duration, and a review of test timings confirms the wait bound |
| Observability | None, new service | Every state change emits one structured log line carrying the order id and the old and new status, plus one counter metric per transition, and both are present for 100 percent of transitions in integration tests. Every log line and every error response carries a correlation identifier that ties a request to the change it caused, present for every request in integration tests. Health checks report liveness and readiness and each responds within 200 ms | Integration tests assert one log line and one counter metric for every transition across all state changes and assert a correlation identifier on every log line and every error response for every request, and a test of the health checks confirms each signal responds within 200 ms |
| Horizontal scalability | None, new service | Adding capacity adds throughput, and the automatic move applies at most once per order even when more than one instance runs | A test runs two instances against one data store and asserts each eligible order moves once, plus a review confirming no request depends on instance-local state |
| Interface contract and error model | None, new service | One machine-readable interface contract describes every operation, lints clean, and validates in CI; every failure returns one documented error shape with a stable code and a meaningful status | Contract lint and contract tests run in CI, plus error-path tests for validation, not-found, and conflict |
| Operability | None, new service | The service and its data store start with one command on a clean machine, and required data-store changes apply on startup without a manual step | A clean-environment run of the documented start command is followed by a readiness check |
| List result bound | None, new service | The list returns at most 20 results by default and at most 100 on request, ordered by creation time | Integration tests confirm the default bound, the maximum, and the ordering |
| Data residency | None, new service | Single region; no personal data captured | A data inventory review and a deployment review confirm the single region and the absence of personal data |

## Notes

- The launch volume figure, 10 requests per second, is an assumption, not a
  measurement. We have no production traffic for this service, so the number is
  a stated starting point that the load test checks and later telemetry revises.
  The burst and the 30-minute sustain belong to the same assumption.
- The five-minute transition budget is the requirement's own bound, taken from
  the brief and restated in PRD-0001; it is not inherited from a platform
  baseline.
- Availability is set at 99.5% monthly because the service runs in one region
  with no redundancy requirement yet. A higher target would require a different
  design and a redesigned budget.
- No residency or personal-data budget beyond single region and no personal data
  applies, consistent with the regulatory position in PRD-0001.
- Extensions deliberately kept out of this delta, including full distributed
  trace export, are recorded in [FUTURE_PHASES.md](../FUTURE_PHASES.md).

### Delta history

The horizontal scalability, interface contract and error model, and operability
rows, along with the correlation-identifier extension to observability, were
added as a controlled post-G2 delta to close gaps against the design context's
NFR table. Correctness under concurrency, testability, and observability were
already covered. Full distributed trace export is deferred to a future phase
recorded in [FUTURE_PHASES.md](../FUTURE_PHASES.md).
