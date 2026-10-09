---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0018
artifact_type: test-report
title: "Order processing coverage report"
status: draft
stage: S5
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers: []
approver: { kind: human, name: "<...>", role: "QA Lead", date: <YYYY-MM-DD> }
gate: G5
sources: [TP-0002, DOC-0005]
jurisdiction: []
created: 2026-10-10
updated: 2026-10-10
superseded_by: null
---

# Order processing coverage report

This is the coverage evidence for the test suite after the improvement work
(Waves 1 to 3). It supplements the S5 test report DOC-0005, which recorded the
45-test run at the original release; the suite is now 138 tests.

## How to reproduce

```bash
dotnet test OrderProcessing.slnx --collect:"XPlat Code Coverage"
```

The command writes a Cobertura file per test project under
`tests/**/TestResults/**/coverage.cobertura.xml`. The figures below are the
highest coverage observed for each assembly across the three test projects.

## Coverage

| Assembly | Line | Branch | Lines | Branches |
|---|---|---|---|---|
| OrderProcessing.Domain | 93.3% | 100.0% | 126 / 135 | 40 / 40 |
| OrderProcessing.Application | 93.1% | 78.6% | 402 / 432 | 77 / 98 |
| OrderProcessing.Infrastructure | 95.4% | 75.0% | 774 / 811 | 21 / 28 |
| OrderProcessing.Api | 33.3% | 21.4% | 214 / 642 | 59 / 276 |
| Total | 75.0% | 44.6% | 1516 / 2020 | 197 / 442 |

## Interpretation

The three inner layers are well covered. The domain, the application services
and validators, and the persistence layer including the idempotency store and
the unit of work are all above 93 percent line coverage, and the domain has full
branch coverage. This is where the correctness work lives, and where the review
found the bugs that tests now pin.

The Api assembly reads low, and that is expected rather than a gap in the
product logic:

- `Program.cs` is top-level bootstrapping (configuration, dependency injection
  wiring, the migration and recurring-job startup, middleware registration). It
  is exercised end to end by the integration tests but a large share of its
  lines are declarative wiring the coverage tool counts as uncovered.
- The Swagger and Hangfire dashboard surfaces are mapped in Development but are
  not part of the automated suite.
- The health endpoints and the correlation middleware are covered through the
  integration requests, but the branching inside the middleware (for example the
  generated-versus-supplied identifier paths) leaves branch coverage lower.

The API's behavior is verified by the 60 integration tests; a low line figure
there reflects bootstrap and developer surfaces, not untested product logic.

## Gaps and how to raise coverage

- Add a host-startup smoke test that boots the API and asserts it reports ready;
  this would exercise more of `Program.cs`.
- Add tests for the Swagger and Hangfire dashboard routes if they should be part
  of the release evidence.
- Raise Api branch coverage by testing the correlation and error paths that are
  only partly covered today.

## Relationship to other artifacts

The tests and their intent are in the test plan TP-0002. The functional results
and the defects found during the original release are in DOC-0005. The per-work-
package traceability is in `docs/IMPROVEMENT_STATUS.md`.
