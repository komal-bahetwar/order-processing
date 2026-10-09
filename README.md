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

The API listens on `http://localhost:8080`; Swagger is served in development.
The service applies its database migrations on startup.

To run the API on the host instead and only use Docker for the database:

```bash
docker compose up -d postgres
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

## How AI was used

See `docs/AI_USAGE.md` for the prompts, the issues found in the AI output, and
how they were corrected. The work was run through the process in
`docs/AGENTIC_SDLC_RUNBOOK.md`.
