# AI Knowledge Assistant

AI Knowledge Assistant is a .NET 8 backend for uploading documents, indexing their text with pgvector, and answering user questions through a retrieval-augmented generation (RAG) chat flow.

## Architecture

The solution follows Clean Architecture:

- `ai-knowledge-assistant.Domain`: core entities, enums, and shared domain constants.
- `ai-knowledge-assistant.Application`: DTOs, service interfaces, use-case services, and application exceptions.
- `ai-knowledge-assistant.Infrastructure`: Entity Framework Core, PostgreSQL, document storage, background processing, AI provider implementations, and repositories.
- `ai-knowledge-assistant.Api`: minimal API endpoints, authentication, authorization, middleware, Swagger, health checks, and observability.

## Tech Stack

- .NET 8 / ASP.NET Core Minimal APIs
- PostgreSQL with pgvector
- Entity Framework Core
- JWT authentication with refresh tokens
- Serilog structured logging
- OpenTelemetry tracing foundation
- Swagger / OpenAPI
- GitHub Actions CI

## Local Setup

1. Install .NET 8 SDK.
2. Start PostgreSQL with pgvector:

```powershell
docker run --name ai-knowledge-postgres `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=your-postgres-password `
  -e POSTGRES_DB=AIKnowledgeAssistant `
  -p 5432:5432 `
  -d pgvector/pgvector:pg16
```

3. Copy `.env.example` values into your local environment or user secrets. Do not commit real secrets.
4. Restore and build:

```powershell
dotnet restore
dotnet build
```

5. Run the API:

```powershell
dotnet run --project .\ai-knowledge-assistant.Api
```

Swagger is available in Development at `/swagger`.

## Docker Compose

Run the API with PostgreSQL and pgvector:

```powershell
copy .env.example .env
docker compose up --build
```

The API is exposed at `http://localhost:5000` when using the local override file, and PostgreSQL is exposed on `localhost:5432`.

## Environment Variables

Configuration supports `appsettings.json`, environment-specific appsettings files, and environment variable overrides.

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Jwt__AccessTokenExpirationMinutes`
- `Jwt__RefreshTokenExpirationDays`
- `AI__Provider`
- `AI__ApiKey`
- `AI__Endpoint`
- `AI__Model`
- `AI__EmbeddingModel`
- `Storage__UploadsPath`

## API Features

- Auth: register, login, refresh token rotation, logout
- RBAC: default Admin/User roles and admin-only user management
- Documents: upload, versioning, background indexing, chunks, reindex, soft delete
- Search: hybrid semantic and keyword search foundation
- Chat: RAG answers, streaming SSE responses, conversation history, citations, feedback
- Admin analytics: overview, user, document, chat, and feedback metrics
- Observability: Serilog logs, correlation IDs, OpenTelemetry tracing foundation
- Health checks: `/health`, `/health/ready`, `/health/live`
- Versioning foundation: current endpoints are available under `/api/v1/...` while legacy `/api/...` routes remain available

## CI/CD

GitHub Actions restores, builds, runs `dotnet test`, and validates the API Docker build when `ai-knowledge-assistant.Api/Dockerfile` exists.

## Tests

The solution includes:

- `ai-knowledge-assistant.UnitTests`
- `ai-knowledge-assistant.IntegrationTests`

Tests use a safe test configuration and fake AI providers, so `dotnet test` does not call real external AI APIs.

```powershell
dotnet test
```

Integration tests run against an isolated in-memory EF Core database for reliable local and CI execution. For full PostgreSQL verification, point `ConnectionStrings__DefaultConnection` at a dedicated test database such as `AIKnowledgeAssistant_Test`.

## EF Core Migrations

Add a migration from the repository root:

```powershell
dotnet ef migrations add InitialCreate `
  --project .\ai-knowledge-assistant.Infrastructure `
  --startup-project .\ai-knowledge-assistant.Api
```

Update the database:

```powershell
dotnet ef database update `
  --project .\ai-knowledge-assistant.Infrastructure `
  --startup-project .\ai-knowledge-assistant.Api
```

For PostgreSQL with pgvector, ensure the database is running from `pgvector/pgvector:pg16` or has the `vector` extension available.

## Production Checklist

Required environment values:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `AI__Provider`
- `AI__ApiKey` when the selected provider requires one
- `AI__Endpoint` when the selected provider requires one
- `AI__Model`
- `AI__EmbeddingModel`
- `Storage__UploadsPath`

Database migration command:

```powershell
dotnet ef database update --project .\ai-knowledge-assistant.Infrastructure --startup-project .\ai-knowledge-assistant.Api
```

Health check URLs:

- `/health`
- `/health/ready`
- `/health/live`

Docker build and run:

```powershell
docker build -f .\ai-knowledge-assistant.Api\Dockerfile -t ai-knowledge-assistant-api .
docker run --rm -p 8080:8080 --env-file .env ai-knowledge-assistant-api
```

## Build

```powershell
dotnet restore
dotnet build
dotnet test
```
