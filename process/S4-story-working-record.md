# S4 story working record

One record for the implementation increment. The stories are small and were
built together as one vertical slice, so this record carries a section per
story rather than a file per story.

Work item: TKT-0001. Branch: `feature/order-processing`. Gate: G4.

## Opening reading

The governing artifacts are the approved PRD (`PRD-0001`), the NFR delta
(`NFR-0001`), the stories `PRD-0001-S01` to `S11`, the plan and breakdown
(`PLAN-0001`, `PLAN-0002`), and the design set (`ADR-0001` to `ADR-0008`,
`DOC-0001` to `DOC-0004`, `TP-0001`, `openapi/order-processing.yaml`). The
design was frozen at G3.

## Plan and build report

| Story | What was built | Tests |
|---|---|---|
| S01 | Order and item domain model, creation invariants, derived total, create endpoint | Unit: create, empty, quantity, price, rounding. Integration: create, 400 paths. |
| S02 | Retrieve by id, not-found and malformed-id outcomes | Integration: get, 404, 400. |
| S03 | List with status filter, stable ordering, result bound and clamp | Integration: filter, clamp over 100. |
| S04 | Manual status advance PROCESSING to SHIPPED and SHIPPED to DELIVERED, reject the rest | Unit + integration: ship, reject manual PENDING to PROCESSING. |
| S05 | Cancel while pending, reject otherwise | Unit + integration: cancel, cancel after processing. |
| S06 | Automatic move of pending orders to processing | Integration: promote, idempotent, leaves cancelled. |
| S07 | Same-status no-op and concurrency conflict on the version token | Integration: stale-writer conflict. |
| S08 | Per-transition structured logging, counter metric, correlation identifier, health signals | Logging and health in place; see notes. |
| S09 | Multi-instance safety: version token plus scheduler lock | Concurrency test; design in ADR-0002 and ADR-0003. |
| S10 | One error shape with stable codes, contract conformance | Error handler; integration error-path tests. |
| S11 | One-command local run, migrations on startup | docker compose; startup migration under an advisory lock. |

## Boundary rulings

- The automatic move is the only path from PENDING to PROCESSING; a manual
  request for that transition is rejected, matching DOC-0003.
- The list bound clamps rather than rejecting, matching the story and the
  contract.
- Money is a fixed-scale decimal, rounded half-up at the line, matching
  ADR-0004.
- Concurrent writes are detected with the `version` column, not a
  database-specific token, matching ADR-0002 after the human correction.

## Artifacts changed

Source under `src/`, tests under `tests/`, `openapi/order-processing.yaml`,
`docker-compose.yml`, `Dockerfile`, `README.md`, and the EF migration under
`src/OrderProcessing.Infrastructure/Persistence/Migrations/`.

## Out-of-scope guards

No authentication, payments, catalogue, or notifications were added. The
future extensions remain in `docs/FUTURE_PHASES.md`.

## Evidence

`dotnet test` passes: 16 unit tests and 15 integration tests against a real
PostgreSQL container. The build is clean with no warnings.
