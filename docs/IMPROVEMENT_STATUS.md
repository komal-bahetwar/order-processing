# Improvement status

This document tracks the work packages in
`docs/ORDER_PROCESSING_IMPROVEMENT_BRIEF.md` against what is actually in the
repository: the status, the implementation location, the tests that exercise
it, and the evidence. It is the traceability index for the improvement effort.
It separates what is implemented from what is planned, so no designed-only item
is read as shipped.

Last updated: 2026-10-10. Test suite at this point: 114 tests, all passing
(62 unit, 11 architecture, 41 integration).

## Status by work package

| ID | Priority | Status | Implementation | Tests |
|---|---|---|---|---|
| IMP-01 | P1 | Implemented | `Order.Items` is a `ReadOnlyCollection` (`src/OrderProcessing.Domain/Order.cs`) | `DomainHardeningTests`; `ApiHardeningTests.A_fresh_context_exposes_a_genuinely_read_only_collection` |
| IMP-02 | P1 | Implemented | Domain null/identity guards in `Order.Create` and `OrderItem`; null-safe `CreateOrderRequestValidator` | `DomainHardeningTests`; `ApiHardeningTests` (null element 400) |
| IMP-03 | P1 | Implemented | `src/OrderProcessing.Domain/Money.cs` range and normalization rules; `OrderItem`/`Order` use them | `DomainHardeningTests`; `ApiHardeningTests` (over-ceiling and overflow 400) |
| IMP-04 | P1 | Implemented | `Order.IsLegal` one legal relation; `OrderStatusNames` name-only parsing; validators and `OrderService` | `DomainHardeningTests` (25-pair and 20-method matrices, status names); `ApiHardeningTests` (`status=999` 400) |
| IMP-05 | P2 | Implemented | Explicit `DateTimeOffset` into `Order.Create` and the transitions; `TimeProvider` injected in `OrderService` and `OrderProcessingService` | `OrderTests` (explicit-time tests) |
| IMP-06 | P2 | Implemented | `Directory.Build.props`, `global.json` | `BoundaryTests.The_build_and_package_policies_are_enabled` |
| IMP-07 | P2 | Implemented | `Directory.Packages.props` central versions; no per-project version pins | `BoundaryTests.The_build_and_package_policies_are_enabled` |
| IMP-08 | P2 | Implemented | Extended `tests/OrderProcessing.ArchitectureTests` (`LayeringTests`, `BoundaryTests`) | The architecture test project itself |
| IMP-09 | P2 | Implemented | This document plus the AI-use log | `docs/AI_USAGE.md` |
| IMP-10 | P2 | Not started (Wave 3) | Requires an `Idempotency-Key` contract, a new table, and new error codes | n/a |
| IMP-11 | P2 | Implemented (Wave 3) | Correlation middleware (`src/OrderProcessing.Api/Correlation/CorrelationMiddleware.cs`), request completion logging, and a job `BackgroundRunId` scope; the contract adds the `X-Correlation-ID` header (ADR-0011) | `CorrelationTests` (7) and `BackgroundRunScopeTests` |
| IMP-12 | P2 | Implemented (Wave 3) | Optional Seq sink in `src/OrderProcessing.Api/Program.cs`; `seq` service in `docker-compose.yml`; README walkthrough | Verified: Seq UI 200 and the API serves with the sink configured; the query smoke is manual (sign in) |
| IMP-13 | P1 | Implemented | `OrderProcessingOptionsValidator`, `ValidateOnStart`, the budgeted drain in `OrderProcessingService`, fail-fast registration in `Program.cs`, `[AutomaticRetry]` on the job | `OptionsValidationTests`; `BacklogDrainTests`; `TwoInstanceProcessingTests` |
| IMP-14 | P1 | Not started (Wave 3) | Requires a cursor contract extension | n/a |

Wave 3 items change the OpenAPI contract or the deployment, so each needs its
own specification or ADR delta and a human gate before implementation, as the
brief requires.

## Walkthrough index

Labels for the interview walkthrough, pointing at the authoritative documents
rather than restating them.

- **Architecture**: `docs/design/DOC-0001-c4-views.md` (the end-to-end
  diagram), `ADR-0008` (modular monolith), `ADR-0001` (layering and the
  framework-free domain).
- **Patterns actually used**: `ADR-0001` (repository, unit of work, factory),
  and the design context `docs/Order_Processing_System_HLD.md` section 17.
- **Concurrency**: `ADR-0002` (the `version` optimistic token, a human
  correction from the AI's `xmin`), `docs/design/DOC-0003`.
- **Background processing and retries**: `ADR-0003`, the `[AutomaticRetry]`
  policy on `ProcessPendingOrdersJob`, and the budgeted drain in
  `OrderProcessingService`.
- **Correlation**: the `X-Correlation-ID` header and the request completion log
  (IMP-11; ADR-0011); the background-run identifier is the `BackgroundRunId`
  scope.
- **Idempotency**: a future extension, IMP-10; the contract of record does not
  yet include an `Idempotency-Key`.
- **Structured logs in a browser (Seq)**: implemented, IMP-12; the console sink
  always works and Seq is an optional secondary sink.
