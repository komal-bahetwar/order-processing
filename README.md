# Order Processing

The backend for an e-commerce order processing system: create an order with
items, retrieve it, list and filter orders, advance the status, cancel a
pending order, and move pending orders to processing automatically.

The stack is .NET 10, ASP.NET Core, EF Core 10 with Npgsql, PostgreSQL 18, and
Hangfire for the scheduled work. The design and the reasoning behind it live
under `docs/`.

## Requirements

- .NET 10 SDK
- Docker (for PostgreSQL, and for the integration tests which start a container)

## Run locally

One command starts the database and the service:

```bash
docker compose up --build
```

The API listens on `http://localhost:8080`; Swagger is served in development at
`http://localhost:8080/swagger`. The service applies its database migrations on
startup.

If the default ports are already taken, override them:

```bash
API_PORT=8081 POSTGRES_PORT=5433 docker compose up --build
```

To run the API on the host instead and only use Docker for the database:

```bash
docker compose up -d postgres
dotnet run --project src/OrderProcessing.Api
```

When overriding the database port for a host run, set the connection string too:

```bash
POSTGRES_PORT=5433 docker compose up -d postgres
ConnectionStrings__Default="Host=localhost;Port=5433;Database=orderprocessing;Username=postgres;Password=postgres" \
  dotnet run --project src/OrderProcessing.Api
```

## Tests

```bash
dotnet test
```

The unit tests cover the domain rules and need no database. The integration
tests start a throwaway PostgreSQL container and exercise the endpoints, the
automatic processing, and the concurrency conflict.

## API

Base path `/api/orders`.

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/orders` | Create an order |
| GET | `/api/orders/{id}` | Get an order by id |
| GET | `/api/orders?status=PENDING&limit=20` | List and filter orders (limit default 20, capped at 100) |
| PATCH | `/api/orders/{id}/status` | Advance the status (PROCESSING to SHIPPED, SHIPPED to DELIVERED) |
| POST | `/api/orders/{id}/cancel` | Cancel a pending order |

Errors use one shared shape with a stable code and a correlation identifier.
The contract is in `openapi/order-processing.yaml`.

## Health

- `GET /health/live` liveness
- `GET /health/ready` readiness, including the data store

## Project layout

```text
src/
  OrderProcessing.Domain/          entities, the status lifecycle, invariants
  OrderProcessing.Application/      services, DTOs, validators, interfaces
  OrderProcessing.Infrastructure/   EF Core, repositories, migrations, the job
  OrderProcessing.Api/              controllers, error handling, composition root
tests/
  OrderProcessing.UnitTests/        domain rules
  OrderProcessing.ArchitectureTests/ the layering and ORM confinement
  OrderProcessing.IntegrationTests/  the API against a real PostgreSQL container
openapi/order-processing.yaml       the API contract
docker-compose.yml, Dockerfile      the local deployment
docs/                                the lifecycle artifacts and the process
```

## Documentation

Start here to understand the product and the decisions behind it.

- Product and requirements: [brief](docs/briefs/BRIEF-0001-order-processing-backend.md),
  [PRD](docs/prd/PRD-0001-order-processing-backend.md),
  [NFR spec](docs/prd/NFR-0001-order-processing-backend-nfr-delta.md),
  [open questions](docs/prd/OQ-0001-open-questions-for-prd-0001.md).
- Design: [C4 and architecture](docs/design/DOC-0001-c4-views.md),
  [data model](docs/design/DOC-0002-data-model-and-migration.md),
  [sequences and state](docs/design/DOC-0003-sequence-diagrams-and-state.md),
  [deployment and evolution](docs/design/DOC-0004-deployment-and-evolution.md),
  the [ADRs](docs/decisions/) (`ADR-0001` to `ADR-0008`), and the
  [API contract](openapi/order-processing.yaml).
- Planning: [release plan](docs/plans/PLAN-0001-release-and-sprint-plan-for-order-processing.md),
  [task breakdown](docs/plans/PLAN-0002-task-breakdown-for-order-processing.md),
  [estimates](docs/plans/PLAN-0003-estimation-record-for-order-processing.md),
  [risks](docs/plans/RISK-0001-risk-register-for-order-processing.md).
- Verification: [test plan](docs/testing/TP-0002-test-plan.md),
  [test report](docs/testing/DOC-0005-test-report.md),
  [security evidence](docs/testing/DOC-0006-security-evidence.md),
  [UAT sign-off](docs/testing/DOC-0007-uat-sign-off.md).
- Release: [change record](docs/releases/DOC-0008-change-record.md),
  [deployment manifest](docs/releases/DOC-0009-deployment-manifest.md),
  [rollback plan](docs/releases/DOC-0010-rollback-plan.md),
  [release notes](docs/releases/DOC-0011-release-notes.md).
- Operations: [SLOs](docs/runbooks/DOC-0012-slo-and-sli-definitions.md),
  [alerts](docs/runbooks/DOC-0013-alert-rules.md),
  [runbooks](docs/runbooks/DOC-0014-operational-runbooks.md).
- Review: [post-launch review](docs/reviews/DOC-0015-post-launch-review.md),
  [telemetry](docs/reviews/DOC-0016-telemetry-report.md),
  [debt register](docs/reviews/DOC-0017-debt-register-delta.md), and the
  [future phases](docs/FUTURE_PHASES.md).
- Process and AI: the [runbook](docs/AGENTIC_SDLC_RUNBOOK.md) and the
  [AI usage log](docs/AI_USAGE.md).
