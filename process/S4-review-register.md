# S4 review register

Round records and dispositions for the implementation review. The reviewer runs
on a different model (deepseek-v4-pro) than the author (deepseek-flash).

## Round 1

Verdict: CHANGES REQUESTED.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | blocker | The list endpoint does not match the OpenAPI contract: the contract uses `limit` with a capped bound and returns a bare array, while the implementation uses `page`/`pageSize` and a wrapped paged result. The `limit` parameter is ignored. | closed: the endpoint now binds `limit`, clamps it, and returns a bare array. |
| 2 | major | The single error shape is not produced for unexpected failures or model-binding failures, and an extra `instance` property violates the contract. | closed: one writer now emits exactly type/title/status/code/detail/traceId for every failure, including 500 INTERNAL_ERROR and model-binding 400s. |
| 3 | major | A single concurrency conflict poisons the rest of the automatic-move batch, because stale entities stay tracked and the next save retries and rolls back the batch. | closed: the batch now fetches ids, processes one order per fresh load, and clears the change tracker on a conflict so the batch continues. |
| 4 | major | The observability budget (per-transition log line, counter metric, correlation identifier) is unimplemented. | closed: every transition emits a structured log line and an `orders.transitions` counter; a middleware pushes the correlation id onto every log line; metrics and traces are exported. |
| 5 | major | Test coverage does not meet the test strategy mapping (race, two-instance, same-status no-op endpoint, deliver, negative price at integration, list default/empty/ordering, terminal-state domain guards). | closed: added the two-instance move, the batch-continues-after-conflict case, the same-status no-op, deliver, negative price, list zero-limit and over-maximum, and the terminal-state domain guards. |
| 6 | minor | Batch size is dead configuration; the service hardcodes it. | closed: the options type moved to the application layer and the batch size is read from configuration. |
| 7 | minor | The foreign key name does not follow the SQL naming convention. | open (debt) |
| 8 | minor | Hardcoded development database credentials in source. | accepted for local dev; must come from the environment in a real deployment |
| 9 | minor | Order items are not ordered on read, so read-back order is not guaranteed though the contract documents submitted order. | accepted (debt): adding a position column is a schema change deferred to a later phase |
| 10 | minor | No `.editorconfig`, `TreatWarningsAsErrors`, or architecture test to hold the dependency direction mechanically. | closed: a `OrderProcessing.ArchitectureTests` project now enforces the dependency direction and EF confinement (5 tests), and an `.editorconfig` is added. `TreatWarningsAsErrors` is deferred because the sandbox blocks the user NuGet config on an implicit restore; the build is warning-free by convention. |

Passed: layering and dependency direction, domain invariants, the concurrency
token and its translation, persistence matching the data model, parameterized
queries, and the health endpoints. Build is clean.

## Round 2

Verdict: APPROVED. The blocker and the four majors were confirmed fixed, and no
regressions were found. Build clean, 19 unit and 21 integration tests pass.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | minor | The correlation-id middleware was registered inside the exception handler, so handler log lines lacked the correlation id. | closed: the middleware now wraps the exception handler |
| 2 | minor | The batch-continues test exercises the unit of work directly rather than the processing service. | accepted (debt): the production path is correct; a service-level mid-batch assertion can be added in S5 |
| 3 | minor | Terminal-state domain guards cover Process from CANCELLED but not the full DELIVERED and CANCELLED matrix. | accepted (debt) |
| 4 | minor | The observability and health assertions are not automated (log line, counter, correlation id, 200 ms health). | accepted (debt): the implementation is present; the assertions land with the S5 test plan |

Round 1 minors 7, 9, and 10 remain recorded as accepted debt.

## Human review at G4

The human asked whether clean architecture is enforced rather than assumed. In
response, a `OrderProcessing.ArchitectureTests` project was added. It asserts
that the domain depends on no other layer and no ORM, that the application layer
does not depend on infrastructure, the api, or the ORM, that infrastructure does
not depend on the api, that controllers do not reach into persistence, and that
the application abstractions are implemented in infrastructure. All five pass,
so the one-way dependency direction is proved mechanically rather than promised.
