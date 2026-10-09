---
# Provenance frontmatter. Every field stays; approver is always human.
artifact_id: DOC-0009
artifact_type: evidence
title: "Deployment manifest for the order processing backend"
status: approved
stage: S6
work_item: TKT-0001
author: { kind: agent, name: "sdlc-writer", model: "deepseek/deepseek-flash" }
reviewers:
  - { kind: agent, name: "sdlc-reviewer", model: "deepseek/deepseek-v4-pro", rounds: 1, verdict: approved }
approver: { kind: human, name: "Komal Bahetwar", role: "EM", date: 2026-10-09 }
gate: G6
sources: [DOC-0004, DOC-0008]
jurisdiction: []
created: 2026-10-09
updated: 2026-10-09
superseded_by: null
---

# Deployment manifest for the order processing backend

This is the S6 deployment manifest, the A6.2 artifact for the G6 packet. It
lists the components and their versions, the start order, how migrations apply,
the configuration keys and their defaults, the ports, the feature-flag position,
and the health endpoints used after deploy. The Engineering Manager approved it
at G6, recorded in the frontmatter. The deployment scope and the run evidence
are in DOC-0008; the deployment view and its deferred evolution are in DOC-0004.

## Components and versions

| Component | Image or artifact | Version |
|---|---|---|
| Build stage | `mcr.microsoft.com/dotnet/sdk:10.0` | .NET 10 SDK |
| API runtime | Built from the `Dockerfile`, runtime base `mcr.microsoft.com/dotnet/aspnet:10.0` | .NET 10 |
| API application | `OrderProcessing.Api.dll`, published in Release with `UseAppHost=false` | 1.0.0 in the OpenAPI contract |
| Database | `postgres:18-alpine` | PostgreSQL 18 (alpine) |

The runtime image installs `libgssapi-krb5-2`. That dependency was missing in
the first build and broke startup; the Dockerfile now installs it, and the
startup log is clean. There is no message broker, no cache, and no other
component in this deployment.

## Start order

1. The database starts. The compose healthcheck runs `pg_isready` against the
   `orderprocessing` database every 5 seconds, with a 3-second timeout and 10
   retries. The service must report healthy before the API is allowed to start.
2. The API starts after the database is healthy, declared as `depends_on` with
   `condition: service_healthy`.
3. The API applies its pending migrations, then starts the Hangfire scheduler,
   then reports ready. Nothing else starts.

## Migrations

The API applies EF Core migrations at startup, before readiness, so the schema
is current with no manual step. The migrations take a PostgreSQL advisory lock,
so if more than one instance starts at once, one applies the schema and the
others wait and then continue. The migration rule is forward-only with
compensation: a shipped migration is never edited, and a defect is corrected by
a new forward migration. The Hangfire schema is created by Hangfire at startup
in its own tables, separate from the application migrations.

## Configuration keys and defaults

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings__Default` | `Host=postgres;Port=5432;Database=orderprocessing;Username=postgres;Password=postgres` in compose; `Host=localhost;...` in `appsettings.json` | The data store connection string. The compose value points at the `postgres` service. A real deployment must supply it from the environment; the committed value is a local development default. |
| `OrderProcessing:CronExpression` | `*/5 * * * *` | The Hangfire recurring schedule for the automatic move. The default runs every 5 minutes. |
| `OrderProcessing:BatchSize` | `200` | The greatest number of pending orders the automatic move processes per run. |
| `ASPNETCORE_ENVIRONMENT` | `Development` in compose | Selects the Development behavior, which serves Swagger and the Hangfire dashboard. A real deployment sets this explicitly and does not expose either. |
| `API_PORT` | `8080` | The host port mapped to the API container port. |
| `POSTGRES_PORT` | `5432` | The host port mapped to the database container port. |

## Ports

| Component | Container port | Host port | Notes |
|---|---|---|---|
| API | 8080 | `${API_PORT:-8080}` | `ASPNETCORE_URLS` is `http://+:8080`. |
| Database | 5432 | `${POSTGRES_PORT:-5432}` | Internal connection from the API uses the service name and port 5432. |

## Feature flags

None. No feature flag is defined or read at deploy time, so every caller sees
the same behavior and a deploy does not change behavior behind a flag. If a
future increment needs one, it is a new configuration key and a new manifest
entry.

## Health endpoints after deploy

| Endpoint | Purpose | Expected result |
|---|---|---|
| `GET /health/live` | Liveness. The process is running. | 200 |
| `GET /health/ready` | Readiness. The process is up and its data store is reachable. | 200 before traffic is treated as healthy |

The 2026-10-09 deployment check used `GET /health/ready` and observed 200, as
recorded in DOC-0008.

## Operational endpoints (Development only)

In Development two non-contract surfaces are served alongside the API, which is
why the compose service runs as Development:

| Route | Purpose |
|---|---|
| `/swagger` | The generated API explorer, from the controllers. |
| `/hangfire` | The Hangfire dashboard: the `process-pending-orders` recurring job, its schedule, and its run history. Unauthenticated, so Development only. |

Neither is part of the public contract in `openapi/order-processing.yaml`; both
are operational aids for the local environment. The recurring job is registered
through the dependency-injected `IRecurringJobManager` at startup, so the
dashboard lists it and the automatic move runs on schedule.
