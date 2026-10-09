# Agent Implementation Brief: Improve `order-processing`

**Purpose:** strengthen the existing `order-processing` backend through domain/build improvements, durable create-request idempotency, request correlation, browser-based structured logging with Seq, and focused API/background reliability improvements. Improve the working system without replacing it or expanding into an unnecessary production platform.

**Document date:** 2026-10-09.

**Target repository:** `order-processing`.

**Status:** proposed improvement brief, not a human-approved implementation specification or gate decision.

This document is intended to be supplied to the agent working inside `order-processing`. It contains the rationale, implementation boundaries, acceptance criteria, and validation requirements. It is self-contained and portable: file paths are relative to the target repository, and no other project checkout is required.

## 1. Instructions to the implementing agent

Improve the target using the prioritized work packages below. First inspect the current checkout: the findings describe a reviewed snapshot, not a guarantee that the repository has not changed since then.

Follow the target repository's `AGENTS.md`, approved specifications, artifact-allocation rules, and human gates. Record this work through its existing process. Do not treat this brief as permission to bypass a gate, invent an approval, merge, or deploy. Where a recommendation changes approved behavior, draft the necessary specification/ADR/contract delta and obtain the required human decision before implementation.

Work only within the target repository's approved scope. Do not import another project's implementation or development-process machinery.

Use existing tooling, test frameworks, project boundaries, and error conventions. Keep changes focused on these improvements. Seq is the only newly requested infrastructure service. Its Serilog sink is an intentional dependency addition; unrelated dependency upgrades and production-platform expansion remain outside scope.

### Previously verified target baseline

The reviewed target revision was `604c1bca53123b49688ea559ca6ba9812e8abfae`.

At that time:

- There were 45 passing tests, including 21 PostgreSQL-backed integration cases.
- The target had the working API, persistence, scheduler, and concurrency handling.

These are historical validation results. Rerun the relevant baseline before changing code; do not present the old counts as current evidence.

## 2. Compatibility requirements

Preserve the target's existing valid behavior unless an explicitly approved change says otherwise.

| Area | Required preservation |
|---|---|
| Architecture | Keep Domain, Application, Infrastructure, API, and the three existing test projects |
| API | Keep `/api/orders`, existing methods, DTO field names, status strings, and response shapes |
| Lifecycle | PENDING -> PROCESSING -> SHIPPED -> DELIVERED; PENDING -> CANCELLED |
| Manual status changes | Keep automatic-only PENDING -> PROCESSING and the existing manual forward transitions |
| Repeated status update | Keep an update to the current status as an application-level no-op |
| Cancellation | Keep cancellation legal only from PENDING; do not make cancelled-order retries succeed implicitly |
| Money | Keep decimal arithmetic and rounding to two decimals using `MidpointRounding.AwayFromZero` |
| Duplicate products | Keep separate duplicate-product lines and their captured prices |
| Totals | Keep the persisted `TotalAmount` and its current API representation |
| Timestamps | Keep new orders' `UpdatedAt` nullable/unset; successful transitions set it; failed transitions and no-ops do not change it |
| Concurrency | Keep version advancement on successful domain transitions and EF's original-version predicate |
| Persistence | Keep PostgreSQL, existing identifiers, EF materialization, migrations, and item relationships |
| Scheduling | Keep Hangfire and the five-minute default |
| Error model | Keep the existing shared error shape, correlation identifier, and established public error codes |
| Correlation | Keep existing error `traceId` semantics; add an explicit correlation response header without silently redefining the field |
| Idempotency | Keep requests without a key working; keyed create requests receive the additional documented retry guarantee |
| Logging | Keep console logging and the application usable without Seq; add Seq as a configurable secondary sink |
| Package versions | Preserve existing exact versions while centralizing them; add a compatible exact Seq-sink version intentionally |

Correcting invalid-input handling is intentional: malformed items, invalid statuses, and amounts that cannot be represented in storage should be rejected explicitly rather than accepted silently or allowed to reach HTTP 500.

Preservation does not mean suppressing real failures. Unexpected database errors must still be logged and surfaced as server errors, and background failures must remain visible to the scheduler.

## 3. Prioritized work packages

| ID | Priority | Improvement | Main outcome |
|---|---|---|---|
| IMP-01 | P1 | Truly read-only aggregate item collection | Prevent in-process mutation outside domain methods |
| IMP-02 | P1 | Domain-level null/identity guards and bounded inputs | Validate invariants even when HTTP validators are bypassed |
| IMP-03 | P1 | Shared money range rules | Prevent arithmetic/storage failures while retaining rounding |
| IMP-04 | P1 | Central transition relation and exhaustive matrices | Make all legal/illegal state changes executable specifications |
| IMP-05 | P2 | Explicit domain time inputs | Make lifecycle tests deterministic without changing timestamp behavior |
| IMP-06 | P2 | Central compiler settings and SDK selection | Make build policy consistent and repeatable |
| IMP-07 | P2 | Central package management | Choose versions once without changing the dependency graph |
| IMP-08 | P2 | Stronger architecture and domain-surface checks | Protect real boundaries rather than just project naming |
| IMP-09 | P2 | Traceable acceptance criteria | Keep claims, tests, and implemented scope aligned |
| IMP-10 | P2 | Durable order-creation idempotency | Client retries do not create duplicate orders |
| IMP-11 | P2 | Correlation IDs and request/job log context | Find the events belonging to one request or background run |
| IMP-12 | P2 | Serilog-to-Seq browser logging | Review structured application logs through a local web UI |
| IMP-13 | P1 | Validated scheduling and resilient backlog processing | Make mandatory automatic processing reliable and observable |
| IMP-14 | P1 | Traversable order listing | Reach all matching orders without unbounded responses |

P1 concerns assignment correctness and invariant protection. P2 concerns retry safety, observability, maintainability, and evidence. These are proposed improvements, not claims that the target already implements them. UUIDv7, customer ownership, and larger production features remain optional.

## 4. IMP-01: Make the aggregate collection genuinely read-only

### Problem

The target exposes its actual backing list through:

```csharp
public IReadOnlyCollection<OrderItem> Items => _items;
```

The interface is read-only; the runtime object is not. In-process code can cast it to `List<OrderItem>` and change it. Since `TotalAmount` was computed earlier, that can also make the items disagree with the stored total.

The improved aggregate should copy the input and expose a genuinely read-only collection, such as `ReadOnlyCollection<OrderItem>`.

### Implementation direction

- Keep the private mutable backing collection needed by EF.
- Expose a stable read-only wrapper, not the backing list or a publicly mutable collection.
- Ensure both ordinary creation and EF's private-constructor materialization initialize the wrapper correctly.
- Preserve the existing public abstraction where practical; changing the runtime collection is sufficient.
- Preserve private setters/constructors required by EF. Do not replace the entity with an incompatible constructor-only object layout.
- Keep field-based navigation mapping in `AppDbContext`.
- Do not add public item-add/remove methods or recalculate persisted totals as a substitute for protecting the collection.

### Acceptance criteria

1. Mutating the caller's original input list after creation does not affect the order.
2. The public collection cannot be cast back to the mutable backing list.
3. Where it implements `ICollection<OrderItem>`, `Add`, `Remove`, and `Clear` throw `NotSupportedException`.
4. Totals and item contents remain unchanged after attempted mutation.
5. An order loaded in a **fresh DbContext** exposes the same protection; EF identity-map reuse is not sufficient evidence.
6. API serialization, order creation, retrieval, status transitions, and stale-writer protection still work.

**Likely target files:** `src/OrderProcessing.Domain/Order.cs`, `src/OrderProcessing.Infrastructure/Persistence/AppDbContext.cs` if mapping needs adjustment, domain tests, integration tests.

## 5. IMP-02: Strengthen creation invariants and input validation

### Problem

The target checks some invariants at the HTTP boundary but not in the domain. It does not explicitly reject a null item sequence, a null element, or an empty ProductId in `OrderItem`.

A previously reproduced create request with `{"items":[null]}` returned HTTP 500.

Add domain null checks, identity checks, and approved bounded enumeration.

### Required hardening

- Reject a null sequence and null elements with the target's normal domain exceptions.
- Keep empty-order rejection and its existing domain error code.
- Reject `Guid.Empty` ProductId directly in `OrderItem`, not only in FluentValidation.
- Keep zero and negative quantities invalid and zero-priced items valid.
- Validate null request elements before accessing their child properties.
- Ensure direct application-service use cannot accidentally bypass these domain invariants.
- Return the shared HTTP 400 validation response for invalid creation inputs, with no persisted partial order.
- Do not introduce blanket catches for `NullReferenceException` or broad success-shaped fallbacks.

### Bounded inputs: recommended, but contract-changing

The proposed limits of 100 lines and quantity 1,000 are engineering assumptions. Those values did not come from the assignment and are not currently the target's contract.

**Recommendation:** propose `MaxLines = 100` and `MaxQuantity = 1_000` as a controlled validation-contract change. Document the rationale and obtain the target's required approval. If approved:

- Use named domain constants rather than magic numbers.
- Enforce the limits in both the domain and HTTP validation.
- Stop domain enumeration after at most `MaxLines + 1` elements.
- Update the request schema, examples, relevant ADR/specification, and README.
- Keep database constraints consistent if they are intentionally extended; use a new migration, not an edit to applied migration history.
- Do not claim bounded domain enumeration bounds the entire HTTP request: JSON deserialization happens earlier. Treat HTTP body limits as a separate decision if needed.

If the numerical limits are not approved, implement the nonbreaking guards and money-safety rules, and report the limits as deferred. Do not silently impose new business assumptions.

### Acceptance criteria

1. Null sequence, empty sequence, null element, empty ProductId, nonpositive quantity, and negative price are rejected through direct domain calls.
2. Invalid API requests return HTTP 400 in the existing shared shape, not HTTP 500.
3. None of these failures creates an order or order-item row.
4. Free items, multiple valid items, and duplicate products still work.
5. If approved limits are implemented, test exactly at and one beyond each limit.
6. If line limits are implemented, an instrumented lazy sequence is pulled at most `MaxLines + 1` times; make the test bounded so a defect fails instead of hanging.

**Likely target files:** `Order.cs`, `OrderItem.cs`, `CreateOrderRequestValidator.cs`, creation DTO/schema documentation, unit and API integration tests.

## 6. IMP-03: Introduce shared monetary safety rules without changing rounding

### Problem

The target's storage columns are `numeric(18,2)`, but domain/request validation does not enforce their upper bound or prevent multiplication/aggregate overflow.

Two previously reproduced requests returned HTTP 500:

- Quantity 1 and unit price `10000000000000000`, above the storage ceiling.
- Quantity `2147483647` and unit price `79228162514264337593543950335`, causing decimal arithmetic failure.

Introduce shared money rules that enforce storage/arithmetic ranges while preserving the target's deliberate rounding policy. Do **not** replace rounding with rejection of extra decimal places.

### Required policy

```text
Maximum stored amount: 9,999,999,999,999,999.99
Arithmetic type: decimal
Price normalization: two decimals, MidpointRounding.AwayFromZero
Line total: quantity multiplied by normalized unit price
Order total: sum of line totals
Negative input price: rejected
Zero-priced items: accepted
Excess fractional precision: rounded, not rejected
```

### Implementation direction

- Put reusable range/normalization rules in the Domain layer without referencing EF or PostgreSQL assemblies.
- Explicitly validate the normalized stored price, line total, and aggregate total against the ceiling.
- Ensure comparisons/arithmetic cannot overflow **before** validation rejects the input.
- A division-based bound can check whether multiplication is safe; an incremental remaining-capacity check can prevent a total from overflowing before it is checked.
- Preserve rejection of negative raw input; do not round a small negative price to zero and accept it.
- Define the edge near the maximum explicitly: reject a price whose normalized stored value exceeds the ceiling, while preserving accepted prices whose normalization remains representable.
- Do not rely on optional 100-line/1,000-quantity limits as the sole arithmetic protection.
- Keep domain and HTTP validation aligned through shared rules or reuse; do not maintain divergent copies of the ceiling.
- Preserve existing DomainException/error translation conventions and document any new internal error codes.
- Keep database precision as the final storage boundary, not the first validation layer.

### Acceptance criteria

| Input/condition | Expected outcome |
|---|---|
| Unit price 1.005, quantity 3 | Stored unit price 1.01; line/order total 3.03 |
| Unit price 10.100 | Accepted; normalized representation follows existing serialization behavior |
| Unit price 0 | Accepted |
| Negative price, including a small negative fraction | Rejected |
| Exactly the maximum amount, quantity 1 | Accepted and round-trips through PostgreSQL |
| Normalized price above the ceiling | Domain validation error; HTTP 400 |
| Price within range but line total above ceiling | Domain validation error; HTTP 400 |
| Individually valid lines whose sum exceeds ceiling | Domain validation error; HTTP 400 |
| Extreme decimal price and large positive integer quantity | Rejected without unhandled arithmetic/database exceptions |

Add unit and real-database integration tests. A failed create must not persist anything. Assert the error body as well as the HTTP status.

**Likely target files:** a small shared domain money-rules helper if justified, `OrderItem.cs`, `Order.cs`, creation validator, unit/integration tests, money ADR and OpenAPI schema.

## 7. IMP-04: Make lifecycle rules explicit and test every combination

### Desired improvement

Declare one legal transition relation and test all 25 ordered state pairs, plus all 20 combinations of five starting states and four domain methods.

### Implementation direction

- Introduce a small domain transition rule helper, or equivalently centralize the relation without unnecessary abstraction.
- Keep named `Process`, `Ship`, `Deliver`, and `Cancel` methods; do not expose a public arbitrary-target mutator.
- Preserve `ORDER_NOT_CANCELLABLE` for invalid cancellation and `INVALID_ORDER_STATE` for other illegal transitions.
- Keep Version increment and timestamp update together with a successful transition.
- Keep API same-status no-ops in the application layer. The domain need not accept repeated transitions.
- Preserve existing enum numeric values. Renumbering them to start at 1 would be an unrelated compatibility change.
- Validate boundary status inputs as defined status **names**, not merely parseable enum values. Preserve case-insensitive valid names.
- Reject unknown names, undefined numeric values, and numeric strings masquerading as valid names if the approved string-enum contract only permits names. Apply the rule consistently to list filtering and status updates.

### Acceptance criteria

1. Exactly four domain transitions are legal:

```text
PENDING -> PROCESSING
PENDING -> CANCELLED
PROCESSING -> SHIPPED
SHIPPED -> DELIVERED
```

2. All 25 state pairs are checked; all other pairs are refused.
3. All 20 state/method cases are checked: four succeed and sixteen fail.
4. Every failed method leaves Status, Version, CreatedAt, UpdatedAt, items, and total unchanged.
5. Every successful method increments Version once and changes only the intended lifecycle fields.
6. Delivered and cancelled orders are terminal.
7. Repeating the current status through the API leaves Version and timestamps unchanged.
8. Manual PENDING-to-PROCESSING remains rejected.
9. `GET /api/orders?status=999` returns HTTP 400, not HTTP 200 with an empty array.
10. Valid case-insensitive status names remain accepted where they were accepted before.

**Likely target files:** domain lifecycle helper and `Order.cs`, list/update validators, `OrderService.cs`, unit and API integration tests.

## 8. IMP-05: Make domain time deterministic

### Desired improvement

Pass `DateTimeOffset now` into the core creation and transition paths instead of reading the clock inside those operations.

### Implementation direction

- Let the core domain creation/transition path receive an explicit timestamp.
- Normalize it to UTC.
- Obtain production time in the application layer, preferably with the framework's `TimeProvider` registered through DI.
- Reuse the existing framework rather than adding a third-party clock package or a large abstraction.
- Preserve current public convenience methods if needed by existing callers; delegate to one explicit-time implementation instead of duplicating transition logic.
- Avoid time-provider state or application dependencies inside domain entities.
- Use a simple test clock or explicit timestamps; do not use sleeps or broad “within a few seconds” assertions as the primary evidence.
- Ensure the processing service uses the same production time seam as manual operations.

### Acceptance criteria

1. Creation uses the supplied timestamp, normalized to UTC.
2. New orders still have `UpdatedAt == null`.
3. Successful transitions set UpdatedAt to the supplied UTC timestamp.
4. CreatedAt never changes on transitions.
5. Illegal transitions and application-level no-ops leave all lifecycle timestamps unchanged.
6. API creation and scheduled/manual transitions can be tested with controlled time.
7. EF round-trip preserves UTC values and nullability.
8. Existing creation and transition behavior remains compatible for production callers.

Explicit time does not imply monotonic time. Do not introduce rejection of “backward” timestamps unless separately specified and approved.

**Likely target files:** `Order.cs`, application services, DI registration, unit/integration test setup.

## 9. IMP-06: Centralize compiler settings and select an SDK

### Desired improvement

`Directory.Build.props` defines shared settings; `global.json` selects an SDK with controlled roll-forward; warnings are build failures.

### Implementation direction

- Add target-owned `Directory.Build.props` for shared `TargetFramework`, `Nullable`, `ImplicitUsings`, and `TreatWarningsAsErrors`.
- Remove redundant per-project declarations while preserving intentional project-specific metadata.
- Keep `net10.0`.
- Prefer the language version associated with the selected SDK unless an explicit language-version choice is justified. Do not set `LangVersion=latest` without considering reproducibility.
- Add `global.json` using a documented, supported .NET 10 SDK available to the intended developer/CI/container environment.
- Select roll-forward behavior intentionally; do not import a machine-specific pin without considering other environments.
- Fix warnings caused or exposed by this change with precise corrections. Do not silence the policy with a broad `NoWarn` list or warnings-as-errors opt-outs.
- Review Docker build compatibility with SDK selection. A host build alone does not prove the Docker SDK image satisfies the pin.

### Acceptance criteria

1. Every source and test project evaluates to the intended target framework, nullable policy, and warnings-as-errors setting.
2. Debug and Release builds succeed with zero warnings/errors.
3. The complete test suite remains green.
4. README prerequisites and Docker build expectations agree with the chosen SDK.
5. Tests or existing validation verify evaluated MSBuild values, not only the first XML occurrence.

**Likely target files:** new root `Directory.Build.props` and `global.json`, seven `.csproj` files, README, architecture tests, Dockerfile only if needed for compatibility.

## 10. IMP-07: Centralize dependency versions without upgrading them

### Desired improvement

`Directory.Packages.props` chooses versions once, enables central package management, and disables per-project version overrides.

### Implementation direction

- Create the target's own `Directory.Packages.props`.
- Enable `ManagePackageVersionsCentrally`.
- Disable `CentralPackageVersionOverrideEnabled` unless a specific approved exception requires otherwise.
- Move each existing exact version into central `PackageVersion` entries.
- Remove per-project version declarations while preserving `PrivateAssets`, `IncludeAssets`, and other package metadata.
- Preserve all deliberate EF, Newtonsoft.Json, Npgsql, Hangfire, and telemetry pins.
- Do not remove direct references that intentionally constrain transitive resolution.
- Verify the restore graph before and after; centralization must not quietly change effective versions.
- Preserve the target's actual FluentAssertions pin, **7.2.0 at the reviewed snapshot**. Any upgrade needs its own rationale and approval.
- Document dependency-specific licensing/compatibility restrictions accurately; avoid presenting an unverified legal conclusion as a testable technical fact.

### Acceptance criteria

1. Central package management is enabled, with exact versions declared centrally.
2. No project declares `Version` or `VersionOverride` through attributes or nested metadata.
3. Intended versions match the pre-change restore result.
4. Per-project overrides are disabled.
5. EF/Npgsql compatibility and the existing transitive Newtonsoft.Json constraint are preserved.
6. Test packages and asset visibility behave as before.
7. Restore, build, full tests, and Docker dependency restoration work.

Derive the central package list from the target's actual projects and restore results, preserving its complete runtime and test dependency graph.

**Likely target files:** new root `Directory.Packages.props`, all package-bearing `.csproj` files, architecture/package governance tests.

## 11. IMP-08: Strengthen executable boundary checks

### Desired improvement

Supplement namespace/type dependency checks with emitted assembly-reference checks, solution-graph checks, and reflection checks over the domain's public API.

### Implementation direction

Extend the target's existing architecture test project; do not collapse its three test projects into one.

Check:

- Domain references no other solution layer or outer framework.
- Application references neither Infrastructure nor API nor persistence/web/scheduler frameworks.
- Infrastructure does not reference API.
- Controllers cannot reach into EF or persistence implementations.
- Repository/unit-of-work implementations remain in Infrastructure.
- Public domain signatures do not expose `float` or `double`, including nullable/array/generic wrappers.
- Status, identity, price, quantity, total, and collection properties have no public setters.
- No public item-mutation method bypasses aggregate invariants.
- Shared compiler and package policies are actually effective.

### Important adaptation rules

- API referencing Infrastructure at the composition root is intentional in the target. Keep it.
- Preserve and validate the target's actual seven-project solution.
- Keep the target's persistence-compatible private setters and constructors.
- Do not import a public `AdvanceVersion()` seam or require version changes to move into a save override.
- Make source-root and restore-assets discovery work for the target's supported test invocation and output layout. Do not assume a fixed `obj/project.assets.json` location or a particular root marker without checking supported build configurations.
- If adding compiled dependency checks, recognize that an unused package/project reference may not appear in emitted metadata. Declared references and emitted/type dependencies provide different evidence.
- Keep process-based MSBuild checks bounded and propagate failures clearly; avoid a test that can hang indefinitely.

### Acceptance criteria

1. Relevant architecture tests validate the existing real graph and dependency direction.
2. Domain public-surface tests enforce invariants without rejecting intentional EF-compatible members.
3. Build/package tests inspect both declarations and evaluated/restored results where required.
4. Tests run from the documented working directory and supported output configuration.
5. Existing controller/persistence boundary tests remain effective.
6. Do not increase the test count with assertions that pass vacuously because there are no matching types.

**Likely target files:** `tests/OrderProcessing.ArchitectureTests/LayeringTests.cs`, focused additional test classes in the existing projects.

## 12. IMP-09: Keep improvements traceable and claims accurate

### Desired improvement

Identify measurable acceptance criteria and distinguish domain responsibilities from persistence, API, and scheduler responsibilities.

Keep implemented behavior, planned work, and future scope clearly separated.

### Implementation direction

- Give every approved improvement a clear implementation location and test/evidence reference.
- Update only directly affected specification, ADR, OpenAPI, README, test-plan/report, and AI-use material.
- Follow the target's artifact allocation and provenance rules.
- Identify any newly introduced validation limits as business assumptions, not assignment text.
- Record the agent's design choices, what had to be adapted, what failed, and how it was corrected.
- Distinguish domain tests, API tests, persistence tests, and actual scheduler tests.
- Do not claim a five-minute firing observation from a direct call to the processing service.
- Do not claim fixed timestamps or IDs when tests still generate them.
- Reconcile approval-status wording only through the applicable human/process action; never manufacture approvals.
- Add a short walkthrough index covering the architecture, patterns actually used, concurrency, idempotency, background retries, correlation, and Seq. Link existing authoritative documents rather than writing a second inconsistent design.
- Distinguish implemented behavior from future-production ideas. Do not claim the original assignment mandates idempotency, Seq, authentication, or distributed tracing.

### Acceptance criteria

1. The affected contracts agree with implemented behavior and tests.
2. Each completed work package lists concrete evidence, not only “tests added.”
3. The AI-use log explains the adaptation and actual issues found.
4. Deferred limits/options and incomplete work are explicitly marked.
5. No documentation labels designed-only features as implemented.

## 13. IMP-10: Add durable idempotency to order creation

### Problem and scope

An order can commit while the HTTP response is lost. Retrying `POST /api/orders` currently creates another order. Existing same-status no-ops, job transition guards, and optimistic concurrency do not deduplicate order creation.

Support an optional `Idempotency-Key` header on creation only. It is a useful production-minded extension, not a requirement stated in the original assignment. Do not change cancellation retry semantics or add generic middleware that pretends every operation has the same idempotency policy.

### Proposed contract

| Condition | Behavior |
|---|---|
| No header | Preserve current creation behavior |
| New valid key | Create once and persist the successful response atomically with the order |
| Same scoped key, equivalent normalized request | Replay the original 201 status, response body, and Location |
| Same scoped key, different normalized request | HTTP 409 with `IDEMPOTENCY_KEY_REUSED` in the shared error shape |
| Concurrent request still being resolved | Wait for a bounded period and replay after commit; if still unresolved, return documented HTTP 409 `IDEMPOTENCY_REQUEST_IN_PROGRESS` with retry guidance |
| Invalid/empty/multiple header values | HTTP 400 in the shared error shape |

Use a documented, bounded key format; a suitable initial proposal is 1-128 characters from letters, digits, `.`, `_`, and `-`. Keys are opaque and case-sensitive. Missing and present-but-empty headers are different cases.

Recommend a configurable **minimum 24-hour successful-result retention**. Expiration must be defined explicitly: after a record is safely expired and removed, reusing the key can create another order. Do not imply indefinite deduplication.

The approved contract must define scope. Until trusted authentication exists, use a documented application-wide scope including operation/version, and recommend high-entropy client-generated keys. Such keys are not an authorization mechanism. Do not invent a caller identity from an untrusted customer header or add authentication solely for this work. If trusted callers are later introduced, add a caller scope through a controlled contract/migration change.

### Implementation direction

- Keep orchestration in Application and database mechanics in Infrastructure. Avoid EF/Serilog dependencies in the application contract.
- Add a durable idempotency record through a new migration, with a unique constraint on the chosen operation/scope/key.
- Store a deterministic normalized-request fingerprint, response status/content type/body/Location, related order ID, creation time, and expiration time.
- Define normalization: preserve item order, duplicate lines, quantities, ProductIds, and normalized captured prices; ignore JSON whitespace/property ordering. Do not compare raw JSON bytes or sort/merge lines.
- Validate request syntax and business inputs before claiming a key. Do not replay an invalid input merely because its rounded fingerprint matches an earlier valid request.
- Store and commit the successful response record in **the same PostgreSQL transaction as the order**. An after-commit cache write is not sufficient.
- Coordinate concurrent claims through database uniqueness, not a “check then insert” race or process-local lock.
- Handle only the expected idempotency unique-constraint conflict; do not translate every persistence failure into a replay. Recover an aborted transaction before querying a winner.
- Roll back both claim and order on transaction failure. Do not retain a completed record for a failed create or cache arbitrary HTTP 500 results.
- Persist the original create snapshot. A retry after the order is shipped must replay the original creation result, not recompute a response from the now-modified order.
- Replays are new HTTP requests: generate/use the current correlation header. Do not replay old transport/request-correlation headers.
- Support cleanup through a small scheduled operation that is safe across workers, does not remove live claims, and is tested with controlled time.
- Do not log raw keys or whole requests. If key diagnostics are needed, use a bounded fingerprint and avoid high-cardinality metric labels.
- Update OpenAPI, error-code documentation, migration/run instructions, and examples.

### Acceptance criteria

1. Sequential retries with the same key/payload produce one order and the original response/Location.
2. A simulated lost response followed by retry recovers that committed result.
3. Different payload under the same key returns 409 and creates nothing.
4. Equivalent JSON and trailing-zero representations normalize consistently; changing quantity, normalized price, line order, or duplicate-line structure is not silently treated as equivalent.
5. Concurrent same-key requests through independent database scopes create exactly one committed order; replay/in-progress behavior follows the contract.
6. Concurrent different keys create independent orders.
7. Rollback or failure before commit leaves no order or falsely completed record and permits a valid retry.
8. A retry still works after host restart and after the order's status changes.
9. Missing-header behavior remains unchanged; malformed headers return 400.
10. Retention boundaries, expiration cleanup, and retries during cleanup are tested.
11. A migration and PostgreSQL integration test prove database-enforced uniqueness and atomicity.

**Likely target files:** `OrdersController.cs`, application creation contract/service, persistence interfaces/implementations, DbContext and a new migration, job registration for cleanup, unit/integration tests, OpenAPI and README.

## 14. IMP-11: Complete request correlation and structured context

### Current behavior

The target already pushes `HttpContext.TraceIdentifier` into Serilog as `CorrelationId`. Error responses also use that request identifier as `traceId`. This is a useful start, not an absence of correlation.

Missing pieces include a client-visible correlation header, an explicit caller-supplied-ID policy, consistent request completion logging, and background-run context. Adding a second unrelated ID without explaining the mapping would make debugging worse.

### Proposed identifier model

| Identifier | Meaning and behavior |
|---|---|
| `CorrelationId` | Accepted `X-Correlation-ID` or a generated opaque ID; echoed in that response header |
| `RequestId` | `HttpContext.TraceIdentifier`; distinct per HTTP request and still used by the existing error `traceId` field |
| `TraceId` / `SpanId` | W3C Activity identifiers when present; do not substitute a caller correlation string for them |
| `BackgroundRunId` | Fresh identifier for one job invocation, shared across that run's log events |
| `OrderId` | Business identity, included when available |

Preserve existing error-body compatibility. A user can search Seq by `CorrelationId` from the header or `RequestId` from an error body. A caller may deliberately reuse a correlation ID across retries; that does not make it an idempotency key or a unique request ID.

### Implementation direction

- Put correlation handling early enough to cover validation, exceptions, 404 responses, health responses, and request-completion logging.
- Accept a single nonempty bounded header, for example at most 64 characters from letters, digits, `.`, `_`, and `-`.
- For invalid or multiple values, return documented HTTP 400 with a newly generated safe correlation header; do not reflect unsanitized input.
- Generate an ID when absent and set the response header before the response starts.
- Push scoped structured properties through the existing logging integration; ensure concurrent requests cannot leak context into each other.
- Use Serilog request logging for a single completion event containing method, safe path, status, elapsed time, CorrelationId, and RequestId. Avoid raw query strings containing secrets.
- Define log levels deliberately: successful completion Information, expected client rejection Warning or lower according to policy, server failure Error. Prevent duplicate canonical exception events while retaining request completion events.
- Log creation outcome and idempotency replay/conflict decisions without bodies or raw keys.
- Add run context to the thin Hangfire adapter, propagating it through `ILogger` scopes to processing-service events. Ordinary recurring scans start their own context; they do not inherit an arbitrary historical create-request ID.
- Reuse existing OpenTelemetry Activity support where helpful and register any new ActivitySource explicitly; do not introduce a second tracing stack.
- Add outbound correlation propagation only for actual existing HTTP integrations. Document future propagation rather than creating unused client wrappers.
- Never use correlation values for authorization, uniqueness, trusted customer identification, or unbounded metric tags.

### Acceptance criteria

1. Missing, valid, invalid, and multiple-header cases behave as documented.
2. Success, validation errors, handled server errors, 404, and health responses carry a usable correlation header.
3. Error `traceId` maps to logged RequestId without silently changing its established meaning.
4. Completion and business events contain consistent structured properties; parallel requests do not share ambient context accidentally.
5. All per-order events in a job can be searched by BackgroundRunId; separate runs get different IDs.
6. Logs contain no authorization tokens, credentials, raw idempotency keys, or request/response bodies.
7. Automated log capture checks fields and level behavior; a browser smoke test searches the same IDs in Seq.

**Likely target files:** `Program.cs`, small API correlation component, exception/model-state response wiring, services' outcome logs, job adapter scopes, tests, OpenAPI headers and README.

## 15. IMP-12: Add Seq for browser-visible Serilog logs

### Outcome

The user should be able to start the local stack, open Seq in a browser, and search creation, transitions, errors, idempotency outcomes, and job events without reading container stdout.

Keep Serilog, `ILogger<T>` usage, structured message templates, and console output. Seq is a secondary telemetry destination, not an order-processing dependency or an audit/transaction store.

### Package and configuration

- Add a compatible, exact `Serilog.Sinks.Seq` version through the target's dependency-management approach.
- Keep sink configuration in API/composition/configuration surfaces; do not put Seq types in Domain or Application.
- Configure endpoint, optional ingestion API key, enabled state, and log levels through configuration/environment.
- Keep the console sink exactly once; account for the existing `.ReadFrom.Configuration()` and code-based console registration so adding sinks does not duplicate events.
- Use configuration/environment for credentials. Do not commit an admin password, ingestion key, or machine-specific path.
- Validate enabled-sink configuration. Invalid configuration should be explicit; temporary remote unavailability should not make API requests fail.
- Use the sink's bounded asynchronous delivery/retry behavior. Document that buffered logs can be dropped during extended outages or abrupt shutdown; do not claim lossless delivery.
- Keep delivery failures diagnosable through a sanitized, nonrecursive console diagnostic path. Do not silently swallow them or send the sink's own failure diagnostics back into the failing sink.
- Dispose/flush through normal host shutdown. Avoid an extra global logger with a competing lifetime.

### Local Docker/browser experience

Add a `seq` service to the local Compose setup with:

- An intentionally selected, documented `datalust/seq` image tag compatible with the development platform; do not leave reproducibility dependent on `latest`.
- A named data volume mounted at `/data`.
- A configurable host UI port, proposed default `5341`, bound to loopback: `127.0.0.1:${SEQ_PORT:-5341}:80`.
- API-container sink URL `http://seq:80`; host-run sink URL `http://localhost:5341` unless the host port is overridden.
- Clear licensing/EULA instructions. `ACCEPT_EULA=Y` is a required operator choice, not a claim that Seq has no licensing terms.
- Initial administrator password supplied through the environment using the selected image's supported initialization mechanism; never a committed reusable default.
- Documented first-run login and optional ingestion API-key configuration. Explain that initialization variables do not automatically reset credentials on an existing persistent volume.
- Retention/storage guidance appropriate for local usage.

A dedicated ingestion-only port may be used if intentionally configured, but is not necessary for this local demo. Do not confuse container port 80 with the host's selected UI port.

Make `docker compose up --build` the documented full demo path including Seq. Also document how to run/test the API without Seq. Do not make PostgreSQL/API readiness depend on Seq being available; telemetry failure must not disable ordering.

Recommended README walkthrough:

```text
1. Supply local Seq administrator configuration and accept the EULA.
2. Start the stack.
3. Open http://localhost:5341, or the configured Seq UI port.
4. Create an order and note X-Correlation-ID and OrderId.
5. Search CorrelationId = 'the-returned-id' or OrderId = 'the-order-id'.
6. Inspect a state transition, an idempotency replay, and a BackgroundRunId.
7. Restart Seq and verify retained events remain available.
```

A smoke test must confirm actual ingestion and searchable fields, not merely that the Seq container is running. Query properties by their real structured names/types; adjust example queries if GUIDs are represented differently.

### Acceptance criteria

1. A reviewer can open the local Seq UI and see structured events from API and background processing.
2. CorrelationId, RequestId, OrderId, statuses, SourceContext, application/environment identity, and BackgroundRunId are present where applicable.
3. Logs can be searched by the documented properties rather than only rendered text.
4. Console logging still works without Seq configuration or when Seq is temporarily unavailable.
5. API operations and database readiness remain functional during a Seq outage.
6. Sink delivery failure is diagnosable, buffers are bounded, and restored connectivity resumes delivery of new events.
7. Persistent volume behavior, authentication, port overrides, and first-run setup are documented and exercised.
8. Secrets and payloads are absent from logs; no duplicate console/Seq emission is introduced accidentally.
9. Unit/integration tests do not require Seq; a separate explicit observability smoke check verifies browser/query behavior.

**Likely target files:** API package references/root central versions, `Program.cs`, appsettings, `docker-compose.yml`, ignored environment-file guidance, README, log-capture tests and smoke procedure.

### Authoritative implementation references

- Serilog sink, buffering and shutdown: https://datalust.co/docs/using-serilog
- Container, storage and initial authentication: https://datalust.co/docs/getting-started-with-docker
- UI/API and ingestion-port distinction: https://datalust.co/docs/urls

Recheck the documentation for the selected image/sink version during implementation. Seq was not implemented or started while writing this brief.

## 16. IMP-13: Strengthen background configuration, backlog handling, and readiness

### Why this is included

A configurable interval, a thin scheduler, independently testable processing, and concurrency awareness support reliable background work. The target already has Hangfire and configurable cron, so keep them rather than replacing them with a timer.

There are concrete gaps: options are bound without validation, only one 200-order batch is processed per run, and recurring-registration failure is logged while startup continues.

### Implementation direction

- Validate processing BatchSize as positive and bounded; validate CronExpression through the scheduler's actual supported parser.
- Validate new retention/drain options as well; fail invalid configuration explicitly during startup.
- Preserve `*/5 * * * *` as the default. Do not assume Hangfire's minute-based recurring cron supports a one-second test interval.
- Keep the processing service directly testable without waiting five minutes.
- Add repeated bounded batch processing within one invocation, constrained by an approved maximum duration/order budget and cancellation.
- Process subsequent batches with fresh state, preserving Version predicates, expected-conflict recovery, and cancellation/shipping rules.
- Design termination and progress checks: competing changes must not cause an infinite loop repeatedly selecting the same IDs.
- Make leftover backlog observable when the invocation budget is exhausted. A five-minute cron does not guarantee five-minute per-order delay under unlimited load.
- Ensure transient job failure leaves later runs able to continue safely; rely on documented Hangfire retry behavior rather than a new retry framework.
- Make the existing job retry policy explicit: finite attempts, scheduler-supported backoff, cancellation behavior, and a visible failed-job/recovery path after exhaustion. Do not assume retries are unlimited or that every failure is transient.
- Keep expected concurrency conflicts on their existing skip/reload path. Do not retry invalid input or forbidden state transitions as though they were infrastructure outages.
- Test a run that commits some transitions and then fails: retry must resume useful work without reapplying committed transitions. A successful state change does not imply exactly-once job execution.
- Do not add automatic request/database write retries indiscriminately. An ambiguous commit can duplicate a create without an idempotency key; explicit transactions and provider execution strategies must be coordinated if introduced.
- Preserve explicit failures for unexpected database faults; do not turn exceptions into a success count of zero.
- Treat failure to register the mandatory recurring job as startup failure or as failed functional readiness with bounded recovery. Database health alone is insufficient evidence.
- Keep liveness independent of dependencies and Seq.
- Log run start/completion/failure with BackgroundRunId, selected/processed/skipped counts and elapsed time, and record an appropriate processing/backlog signal without sensitive/high-cardinality dimensions.

### Acceptance criteria

1. Invalid batch, cron, and processing-budget settings fail explicitly.
2. A backlog larger than one batch is processed across batches during a run up to the configured budget.
3. Budget exhaustion is observable and remaining orders are picked up later.
4. Cancelled/nonpending orders remain unchanged; concurrent writers still commit only legal transitions.
5. A service-level test introduces an expected conflict mid-run and proves subsequent healthy orders proceed.
6. An unexpected failing run is reported as failure; a later run succeeds without duplicate state transitions.
7. Cancellation/shutdown stops work promptly without false success or corrupted state.
8. A registration failure cannot leave readiness reporting the mandatory feature as healthy.
9. Registration/configuration tests prove schedule wiring; any claim about actual ticks has separate scheduler evidence.
10. Retry attempts/backoff and exhaustion are documented; partial-progress retries preserve committed state, and permanent errors are not hidden behind success or endless retries.

**Likely target files:** processing options/service, pending queries/unit-of-work lifecycle, infrastructure registration/job adapter, startup/readiness, tests and operational documentation.

## 17. IMP-14: Add complete, bounded list traversal

### Problem

The current default/cap is 20/100, but there is no next-page mechanism. This prevents clients from listing all matching orders, contrary to the original assignment.

### Implementation direction

- Propose keyset pagination using the existing stable `(CreatedAt, Id)` order.
- Preserve the existing array response and default/capped `limit` for first-page clients.
- Add an optional opaque `cursor` query parameter and a documented next-cursor response header, such as `X-Next-Cursor`; omit the header on the final page.
- Treat this as an approved contract extension and update OpenAPI/README. Do not silently replace the array with a wrapper or version the route without need.
- Bind continuation semantics to the selected status filter/order. Validate token format and length, reject malformed/incompatible tokens, and do not interpret a client token as raw SQL.
- Ensure the seek predicate's ID tie-break ordering agrees with PostgreSQL, including same-timestamp cases.
- Use bounded reads and existing useful indexes; avoid a new generic query framework.
- Document that this is not a snapshot: concurrent inserts/status changes can affect traversal. Do not claim fixed-snapshot results unless implemented separately.
- Log/query counts where appropriate without logging raw cursor content or creating per-cursor metric labels.

### Acceptance criteria

1. A dataset larger than 100 orders is traversed completely with bounded pages and no duplicate/missing orders when the dataset is unchanged.
2. Filtering remains consistent across pages; same-timestamp ties work.
3. Existing no-cursor clients still receive the same array shape and limit behavior.
4. Final-page and empty-result behavior are explicit.
5. Invalid/incompatible cursor and status inputs return the shared HTTP 400 shape.
6. Integration tests seed enough data to exercise real page boundaries, not just assert `Count <= 100`.

**Likely target files:** controller/query DTO/validators, application list contract, repository query, OpenAPI, integration tests, README.

## 18. Changes that must not be introduced without justification

| Proposed change | Decision for this improvement effort | Reason |
|---|---|---|
| Reject prices with more than two decimals | Do not copy | Target deliberately rounds; changing this breaks accepted inputs |
| Reject duplicate ProductIds | Do not copy | Target deliberately keeps separate lines and prices |
| CustomerId and ownership model | Separate future scope | Requires contract/data migration and authentication design; not merely domain hardening |
| JWT and outbox | Do not introduce in current work packages | Authentication is separate scope; an outbox is conditional future integration work |
| UUIDv7 | Optional later proposal | Useful locality/time choice, but not necessary to fix the current correctness gaps |
| Transition-independent public version advancement | Do not copy | Target's working stale-write protection depends on its existing transition version increment |
| Derived total replacing persisted total | Do not copy wholesale | Protect collection invariants without changing EF schema/query behavior |
| Non-null UpdatedAt on creation | Do not copy | Target's API represents “not yet updated” with null |
| Enum values starting at 1 | Do not copy | Unnecessary representation change |
| Five projects with one test project | Do not copy | Target's seven-project structure is already working |
| API only references Application | Do not copy | Target's composition root intentionally wires Infrastructure |
| Unrelated package-version changes | Do not introduce | Preserve the target's actual dependency graph and deliberate compatibility pins |

If UUIDv7 is later approved, preserve existing IDs and external identifier types, update only new ID generation, and test the version/time encoding. Do not claim perfectly monotonic ordering or guaranteed uniqueness. No such migration is part of the required packages above.

## 19. Architecture principles and implementation boundaries

Keep the system small, explainable, tested, and aligned with business rules. Basic layering and API separation are already implemented in the target; strengthen them rather than rebuilding them.

| Area | Preserve or strengthen | Application to this target |
|---|---|---|
| Layers, domain, lifecycle, validation | Domain owns invariants; request validation stays separate | Covered by IMP-01 through IMP-04; preserve target DTOs, rounding, and duplicate-line decisions |
| Background separation/configuration/testing | Thin scheduler, configurable cadence, direct service tests | Keep Hangfire; add configuration/recovery/backlog evidence in IMP-13 |
| Repositories and patterns | Small abstractions with a reason; no forced State/Strategy framework | Keep the useful existing repository/unit-of-work boundary; explain EF's overlapping mechanics |
| Concurrency | Prevent cancel-versus-processing lost updates | Keep version checks; add actual service/API race evidence, not a claim of exactly-once job execution |
| Data/query design | Preserve captured prices and use query-appropriate indexes | Keep existing schema; verify cursor queries and new idempotency uniqueness, not speculative indexes |
| Tests and errors | Domain/API/database tests; meaningful centralized failures | Existing rich error shape stays; add field-level, transaction, correlation and multi-batch tests |
| Structured logging | Searchable OrderId, states, CorrelationId; avoid sensitive data | IMP-11 and IMP-12 turn this into a browser-debugging workflow |
| Swagger, README, AI explanation | Fast reviewer onboarding and concrete AI-correction examples | Extend current OpenAPI/README/AI log with actual implemented additions |
| Scope and walkthrough | Prioritize correctness over technology count; explain tradeoffs | Do P1 first, then focused idempotency/observability; keep production ambitions optional |
| Production-minded extensions | Idempotency and telemetry support retry safety and operations | Explicitly requested in this brief, not original assignment requirements |

Avoid unrelated or incompatible changes:

- Downgrading from the existing .NET 10 target without a justified runtime decision.
- Replacing working Hangfire with `BackgroundService`.
- Using one-second recurring cron when the scheduler does not support it.
- Replacing the target's approved shared error shape with a simpler incompatible response.
- Exposing a mutable backing list through a read-only interface.
- Removing `IUnitOfWork` without assessing its existing transaction/conflict responsibilities.
- Adding User/Product/Customer features or CustomerId to logs when they are not modeled or justified.
- Claiming EF currently emits a status predicate merely because conceptual SQL includes one. EF's original-version predicate and the domain's state guard are separate mechanisms.

Recommended architecture remains:

```text
Client -> API -> Application -> Domain
                       |
                       v
                Infrastructure -> PostgreSQL

Hangfire -> Application processing -> same domain/persistence rules

Serilog / ILogger -> console + configurable Seq sink
```

Idempotency orchestration belongs beside creation in Application, backed by PostgreSQL through Infrastructure. Correlation/request handling and Seq configuration belong at the API/host boundary. No broker, Redis lock, or new service boundary is needed.

## 20. Work that remains outside this brief

This brief does not authorize microservices, a broker, sharding, multi-region deployment, payment/inventory features, production authentication, CI-platform replacement, event sourcing, a generic retry/pattern framework, or an unrelated security overhaul.

Tracing-service replacement, mandatory Seq-dependent startup, lossless audit logging, and new downstream HTTP integration infrastructure are also outside scope.

Section 23 records conditional future scaling options. It is design guidance, not authorization to implement those options as part of IMP-01 through IMP-14.

## 21. Suggested execution order and dependencies

Use the repository's existing work tracking. Do not introduce a new process or task database.

1. **Baseline and decisions:** inspect the current checkout, run relevant baseline commands, map governing specifications, and record which input-limit decisions require approval.
2. **Invariant and assignment hardening:** implement IMP-01 through IMP-04, the approved parts of IMP-02, IMP-13, and IMP-14. Business caps and listing/drain contract extensions require their own approved decisions.
3. **Time and build governance:** implement IMP-05 through IMP-07, including before/after restore evidence and container SDK compatibility.
4. **Durable retry safety:** implement IMP-10 after creation normalization and transaction ownership are settled.
5. **Observability:** implement IMP-11 and IMP-12; keep the API usable without Seq and verify actual searchable ingestion.
6. **Boundary enforcement:** complete IMP-08 against the final domain/application/persistence surfaces.
7. **Evidence and documentation:** complete IMP-09 throughout the work and reconcile the final delivered scope.

Actual prerequisites:

- HTTP creation validation must agree with the finalized domain money and input rules.
- Fake-time service integration depends on the explicit-time domain path.
- Evaluated compiler-policy checks depend on centralized settings.
- Restore-version checks depend on central package management.
- Idempotency fingerprints depend on approved creation normalization; atomic replay records depend on explicit transaction ownership.
- Idempotency expiration tests reuse deterministic time.
- Searchable Seq correlation examples depend on the identifier model and structured properties, not merely the sink installation.
- New idempotency/cleanup/drain options depend on configuration validation.
- Full regression evidence depends on all approved implementation changes.

Collection hardening and package centralization do not inherently depend on each other. Do not invent dependencies simply from the order of this list.

## 22. Validation and definition of done

Run the smallest relevant unit/architecture/integration selectors during each change, then the full suite after integration. Use the target's documented environment and database setup.

Where appropriate:

```bash
dotnet restore OrderProcessing.slnx
dotnet build OrderProcessing.slnx --no-restore
dotnet build OrderProcessing.slnx -c Release --no-restore
dotnet test OrderProcessing.slnx --no-restore
```

Use a dedicated validation database/container rather than a developer's existing database. Verify the Docker build/run path when SDK/build/package configuration changes.

### Required regression evidence

| Category | Minimum evidence |
|---|---|
| Collection | Input copying, mutation refusal, and fresh-context EF round-trip |
| Creation | Null/empty inputs, null element, empty ProductId, quantity/price guards, duplicate-line compatibility |
| Money | Existing rounding example, zero/trailing zeros, storage ceiling, line/sum overflow guards, sanitized HTTP 400, no partial persistence |
| Lifecycle | 25 pair matrix, 20 method matrix, terminal states, unchanged failed transitions, unchanged API no-op |
| Time | Explicit timestamps, UTC normalization, nullable initial UpdatedAt, controlled service time |
| Concurrency | Existing stale-writer and competing-processor tests still pass with Version increments unchanged |
| API | Existing methods/DTOs/errors preserved; invalid status and creation inputs rejected correctly |
| Build | Effective shared compiler settings, zero-warning Debug/Release builds, compatible SDK/container |
| Dependencies | Central pins match prior effective versions; project metadata and transitive constraints preserved |
| Idempotency | Atomic order/result commit, concurrent uniqueness, equivalent/different payloads, rollback, restart/status-change replay, retention |
| Correlation | Header/error/log mapping, invalid input, concurrent scope isolation, background run context, no sensitive payload logging |
| Seq | Actual searchable ingestion, persisted events, login/port overrides, console fallback, explicit bounded outage behavior |
| Background reliability | Options validation, multi-batch budget, mid-run conflict continuation, bounded retry/exhaustion, partial-progress recovery, scheduler-aware readiness |
| Pagination | Traverse more than 100 orders, filter consistency, timestamp ties, malformed cursor rejection, existing array compatibility |
| Documentation | Acceptance criteria, implementation, contracts, evidence, and AI-use record agree |

A work package is done only when its approved criteria are implemented and verified. A recommended limit or optional feature awaiting a human decision is not done and must be reported as pending/deferred, not silently skipped.

### Final agent handoff

Report:

- Which IMP packages were completed and which are deferred.
- Any approved behavior changes, including exact limits.
- Relevant changed files and contract/specification updates.
- Commands actually run and their results.
- Newly reproduced problems and their resolution.
- Remaining current-request blockers.

Do not describe the system as production-ready or claim unrelated issues were fixed.

## 23. Scale-focused architecture evolution: conditional future work

**Do not implement this section by default.** It captures applicable ideas from the proposed pattern checklist, with concrete triggers and proof requirements. Current work remains IMP-01 through IMP-14; future implementation requires its own approved scope.

The target already has layers, dependency injection, an order-specific repository, a rich domain model, guarded state transitions, optimistic concurrency, a thin scheduler adapter, and a unit-of-work boundary. Preserve those. They do not need to be reintroduced under new pattern names.

### 23.1 Measure before adding architecture

Define an approved workload and measurable budgets before claiming scale:

- Order creation/query/transition rates and representative item counts.
- HTTP p95/p99 latency and failure rate.
- Pending backlog count, oldest pending age, and processing drain rate.
- Database query duration, connection-pool pressure, lock waits, and concurrency-conflict rate.
- Worker throughput, retry/failure rates, and telemetry buffering/drop behavior.

Test with representative stored data and concurrent cancellation/processing, not only isolated domain calls. Compare results before and after an architectural change using the same environment and workload. Do not present unmeasured performance targets as achieved guarantees.

Correct pagination, query shapes/indexes, bounded processing, and excessive database round trips before adding a broker, more workers, or service boundaries. More API replicas cannot fix a saturated database or duplicated worker selection.

### 23.2 Applicable options and adoption triggers

| Option | Concrete trigger | Intended benefit | Boundary |
|---|---|---|---|
| Independent API/worker scaling | Processing consumes resources needed by requests, or oldest-pending age repeatedly exceeds the agreed budget | Tune request capacity and background capacity separately | Same domain/application implementation and PostgreSQL consistency boundary; no mandatory service split |
| Producer-consumer integration with a transactional outbox | Real downstream work such as notifications/fulfilment must survive a commit-to-publish crash, or needs independent throughput | Decouple follow-up work while preserving durable event publication | Existing Hangfire may be sufficient initially; introduce a broker only for a demonstrated requirement |
| Modular-monolith boundaries | Distinct order, fulfilment, or inventory capabilities acquire real ownership, rules, or change-pressure boundaries | Scale development and contain coupling before considering services | Business modules are not the same as the current technical layers; start inside one deployment |
| Strategy for distinct processing policies | At least two real policies, such as standard and priority processing, require materially different behavior | Isolate evolving policies without duplicating the lifecycle | Not a throughput optimization by itself; selection must not bypass domain/concurrency rules |
| Circuit breaker for external calls | A real remote provider repeatedly fails or becomes slow, consuming request/worker resources | Fail fast and limit cascading failure | Apply at an actual integration adapter, with timeouts and bounded concurrency; not to hide core-database failure |

These are contingent choices, not a mandate to implement every pattern.

### 23.3 Multi-instance processing must prove both safety and useful throughput

When scaling workers is approved:

- Test separate hosts/processes against the same database; two DI scopes alone are not a deployment test.
- Preserve original-version checks and version advancement across every allowed write path.
- Measure duplicate selection and conflict rate. Workers racing over the same first batch may remain correct while doing mostly redundant work.
- If contention requires claiming/partitioning work, choose an explicit database-backed approach with bounded transactions, crash recovery, and ownership/lease semantics where applicable. Do not add a process-local mutex or Redis lock by reflex.
- Any conditional/bulk update must enforce the intended state/version predicate, advance the surviving row's version, inspect affected rows, and preserve aggregate rules. EF bulk APIs do not supply tracked concurrency behavior automatically.
- Verify worker termination/restart, partial progress, cancellation races, retry exhaustion, and sustained backlog reduction.
- Keep request-correlation/run logging and metrics bounded; scale must not create unbounded identifier labels or sink buffers.

Acceptance requires both legal final states/no lost writes and a demonstrated capacity improvement under the agreed workload. Passing a single-race correctness test is not enough to claim scalable processing.

### 23.4 Producer-consumer and outbox: only for actual downstream effects

If approved, commit the order change and the outgoing event in one database transaction. Publish through a separate dispatcher with finite retries and visible terminal failures.

Assume at-least-once delivery:

- Give events stable IDs and relevant order/version context.
- Consumers deduplicate events durably; for local database effects, coordinate deduplication with the effect in the same transaction.
- External effects require the provider's idempotency support or a deliberately designed recovery/reconciliation mechanism. An inbox record alone cannot make an unrelated remote side effect atomic.
- Define any required per-order ordering and out-of-order handling; do not assume a global event order.
- Provide bounded queues/backpressure, oldest-unpublished age, consumer-lag signals, retry/exhaustion visibility, and replay procedures.

This is distinct from HTTP create idempotency. `Idempotency-Key` prevents duplicate keyed creates; event IDs prevent duplicate consumer effects. Neither guarantees exactly-once execution.

### 23.5 Retry and circuit-breaker boundaries

**Current improvement:** IMP-13 documents and verifies Hangfire's bounded retry behavior. Do not build a general retry framework.

**Future external integrations:** apply retries only to appropriate transient failures, with bounded attempts, backoff, cancellation and an overall time budget. Use jitter where the selected mechanism supports it. Avoid layered retries that multiply attempts across HTTP handlers, workers, and clients.

Protect non-idempotent external calls with an explicit replay/reconciliation policy. Circuit-breaker open/half-open states must be visible and tested; any fallback must be an honest pending/failed outcome, not a fabricated successful order operation.

Do not use a database circuit breaker to keep readiness green while the core order store is unavailable.

### 23.6 Patterns not added merely for scale

| Checklist item | Decision |
|---|---|
| Full multi-project Clean Architecture | Core separation already exists; retain it. More projects do not increase runtime capacity |
| Full DDD approach | Revisit when domain complexity/bounded contexts justify it, not simply because traffic grows |
| Specification | Use only if substantial shared query rules emerge; wrapping a query does not improve its execution plan |
| Factory framework | Current creation methods are sufficient; additional factories do not improve throughput |
| Class-per-state State pattern | Adopt only for substantial state-specific behavior; keep the current guarded relation otherwise |
| Additional Unit of Work abstraction | Existing save/transaction boundary is sufficient; avoid overlapping transaction owners |
| AAA | Keep as test organization; it is not a scaling mechanism |
| Adapter | Existing thin scheduler/persistence boundaries already serve this purpose; introduce a new adapter only for a real dependency |

### 23.7 Future-work decision record

Before implementing a future option, record:

1. The measured bottleneck or concrete domain/integration need.
2. The simpler alternatives considered.
3. Transaction, concurrency, ordering, failure/recovery and idempotency consequences.
4. A before/after workload and acceptance threshold.
5. Operational cost, observability, and rollback plan.

Prefer the smallest change that meets the measured need. An interview walkthrough can explain these triggers without implementing the entire future architecture.

## 24. Target implementation map

All paths below are relative to `order-processing`. They identify existing implementation surfaces or proposed additions; inspect the current tree before editing.

| Improvement | Target location |
|---|---|
| Collection, null guards, explicit time, lifecycle | `src/OrderProcessing.Domain/Order.cs` |
| Product identity, quantity guards, captured price | `src/OrderProcessing.Domain/OrderItem.cs` |
| Shared money rules | Small new helper under `src/OrderProcessing.Domain/` if needed |
| Shared legal transition relation | Small new helper under `src/OrderProcessing.Domain/` if needed |
| Creation/list/status validation | `src/OrderProcessing.Application/Validation/` |
| Creation, idempotency orchestration, manual operations | `src/OrderProcessing.Application/Services/OrderService.cs` and appropriate application contracts |
| Background processing | `src/OrderProcessing.Application/Services/OrderProcessingService.cs` |
| Persistence, pagination and idempotency records | `src/OrderProcessing.Infrastructure/Persistence/`, including new migrations |
| Scheduling, retry policy and run context | `src/OrderProcessing.Infrastructure/Jobs/`, `src/OrderProcessing.Infrastructure/DependencyInjection.cs` |
| Request correlation, Seq configuration and readiness | `src/OrderProcessing.Api/Program.cs` and focused API components |
| Central compiler/SDK policy | New root `Directory.Build.props` and `global.json` |
| Central dependency policy | New root `Directory.Packages.props` and existing `.csproj` files |
| Domain/lifecycle/public-surface tests | `tests/OrderProcessing.UnitTests/` |
| Dependency/build/package policy tests | `tests/OrderProcessing.ArchitectureTests/` |
| API/database/concurrency/idempotency tests | `tests/OrderProcessing.IntegrationTests/` |
| Reviewer run path and Seq browser setup | `docker-compose.yml`, README and configuration guidance |
| API contract and AI-use evidence | `openapi/order-processing.yaml`, `docs/AI_USAGE.md`, relevant approved artifacts |

Architecture principles are summarized in section 19. Seq's external implementation references are listed in IMP-12. No absolute local path, external assignment document, or access to any other project is required.

**Desired outcome:** keep `order-processing`'s working end-to-end backend, add stronger defenses and complete assignment traversal/processing behavior, prevent duplicate keyed creates, and let a reviewer follow requests and jobs in Seq without changing unrelated business policies or importing unfinished infrastructure.
