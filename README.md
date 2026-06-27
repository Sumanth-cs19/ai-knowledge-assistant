# AI Knowledge Assistant

AI Knowledge Assistant is a .NET 8 backend for uploading documents, indexing their text with pgvector, and answering user questions through a retrieval-augmented generation (RAG) chat flow.

The repository also includes an Angular frontend in `ai-knowledge-assistant-ui` for authentication, document management, RAG chat, admin analytics, profile, and settings.

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
- Angular 21 frontend
- Angular Material, Bootstrap 5, ngx-toastr, marked, and highlight.js
- Nginx static hosting for the production frontend container

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

## Frontend Setup

From the Angular project folder:

```powershell
cd .\ai-knowledge-assistant-ui
npm install
npm run build
npm start
```

The development UI runs at `http://localhost:4200` and uses `https://localhost:5001/api` from `environment.development.ts`.

Frontend environments:

- `src/environments/environment.development.ts`: local development API URL
- `src/environments/environment.production.ts`: deployed Render API URL
- `src/environments/environment.ts`: default fallback configuration

Frontend architecture:

- `core`: guards, interceptors, services, models, constants
- `shared`: reusable loading, empty, error, dialog, table, pagination, and utility components
- `layouts`: auth and dashboard shells
- `features`: auth, dashboard, documents, chat, conversations, admin, profile, settings

Screenshot placeholders:

- `docs/screenshots/login.png`
- `docs/screenshots/documents.png`
- `docs/screenshots/chat.png`
- `docs/screenshots/admin.png`

Architecture diagram reference:

- `docs/architecture/frontend-backend-rag-flow.md`

## Docker Compose

Run the UI, API, PostgreSQL, and pgvector:

```powershell
copy .env.example .env
docker compose up --build
```

The UI is exposed at `http://localhost:4200`, the API is exposed at `http://localhost:5000` when using the local override file, and PostgreSQL is exposed on `localhost:5432`.

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

GitHub Actions restores, builds, runs `dotnet test`, installs the Angular frontend, runs the frontend lint/build validation, runs frontend tests, and validates API/UI Docker builds when Dockerfiles exist.

Frontend Docker build:

```powershell
docker build -f .\ai-knowledge-assistant-ui\Dockerfile -t ai-knowledge-assistant-ui .
```

The frontend container uses Nginx with SPA routing fallback and proxies `/api/*` to the API service in Docker Compose.

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

Frontend production deployment:

```powershell
cd .\ai-knowledge-assistant-ui
npm ci
npm run build
docker build -f .\Dockerfile -t ai-knowledge-assistant-ui ..
```

Vercel deployment:

1. Import the GitHub repository into Vercel.
2. Set the project Root Directory to `ai-knowledge-assistant-ui`.
3. Select the Angular framework preset.
4. Use `npm run build` as the Build Command.
5. Use `dist/ai-knowledge-assistant-ui/browser` as the Output Directory.
6. Deploy. The committed `vercel.json` provides the Angular routing fallback.

The production Angular environment calls `https://ai-knowledge-assistant-api-h4bx.onrender.com/api`. Local `ng serve` continues to use `https://localhost:5001/api` from `environment.development.ts`.

For full-stack deployment:

```powershell
docker compose up --build
```

## Build

```powershell
dotnet restore
dotnet build
dotnet test
```

Frontend:

```powershell
cd .\ai-knowledge-assistant-ui
npm install
npm run lint
npm test
npm run build
```
