# Deploy the API to Render with Neon PostgreSQL

This guide deploys the ASP.NET Core API on a free Render web service and stores relational/vector data in a free Neon PostgreSQL project. The repository's multi-stage Dockerfile builds and runs the API on .NET 8.

## Architecture

- **Render Web Service:** builds `ai-knowledge-assistant.Api/Dockerfile` and runs the API.
- **Neon PostgreSQL:** stores users, documents, chunks, vectors, conversations, messages, and feedback.
- **Groq:** generates chat completions.
- **Local hash embeddings:** generated inside the API without a paid embedding service.

Render free web services can spin down while idle and use an ephemeral filesystem. Neon data persists, but files written to `/tmp/uploads` do not survive every restart or deployment. This zero-cost setup is suitable for demos and testing; durable document storage requires an external object store.

## 1. Create the Neon database

1. Create or sign in to a Neon account and select the Free plan.
2. Create a project in a region near the Render service. This repository's `render.yaml` defaults Render to Singapore.
3. Open the project dashboard and select **Connect**.
4. Copy both connection strings:
   - **Direct connection:** host does not contain `-pooler`; use it for EF migrations.
   - **Pooled connection:** host contains `-pooler`; use it for the Render application.
5. Convert the selected connection into Npgsql key/value format if Neon displays a PostgreSQL URI:

```text
Host=ep-example-pooler.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=REPLACE_ME;SSL Mode=Require;Channel Binding=Require
```

Do not commit either connection string.

## 2. Enable pgvector

The existing `InitialCreate` migration contains the Npgsql `vector` extension annotation and generates `CREATE EXTENSION IF NOT EXISTS vector`. Neon supports pgvector.

You can enable it before migration from the Neon SQL Editor:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Verify it:

```sql
SELECT extname, extversion
FROM pg_extension
WHERE extname = 'vector';
```

## 3. Apply EF Core migrations

Use the **direct, non-pooled** Neon connection for migrations. From the repository root in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "Host=ep-example.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=REPLACE_ME;SSL Mode=Require;Channel Binding=Require"

dotnet tool install --global dotnet-ef --version 8.*
dotnet ef database update `
  --project ai-knowledge-assistant.Infrastructure `
  --startup-project ai-knowledge-assistant.Api

Remove-Item Env:ConnectionStrings__DefaultConnection
Remove-Item Env:ASPNETCORE_ENVIRONMENT
```

If `dotnet-ef` is already installed, use `dotnet tool update --global dotnet-ef --version 8.*` or skip the installation command.

Verify the migration in the Neon SQL Editor:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory";
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;
```

## 4. Deploy with the Render Blueprint

The root `render.yaml` defines a free Docker web service, Singapore region, `/health/live` health check, and all non-secret settings.

1. Push the repository to GitHub.
2. In Render, select **New > Blueprint**.
3. Connect the repository and select the branch containing `render.yaml`.
4. Enter values when Render prompts for variables marked `sync: false`:
   - `ConnectionStrings__DefaultConnection`: use the **pooled** Neon connection.
   - `AI__ApiKey`: use the Groq API key.
5. Review the generated `Jwt__SigningKey`; the Blueprint asks Render to generate a random 256-bit value.
6. Apply the Blueprint and wait for the Docker build and health check to pass.

Render builds with the repository root as Docker context and `ai-knowledge-assistant.Api/Dockerfile` as the Dockerfile. The API binds to Render's `PORT` on `0.0.0.0`; locally, the container defaults to port 8080.

## 5. Manual Render setup

If you do not use the Blueprint:

1. Select **New > Web Service** and connect the GitHub repository.
2. Choose **Docker** as the runtime.
3. Set the instance type to **Free**.
4. Set Dockerfile path to `./ai-knowledge-assistant.Api/Dockerfile`.
5. Set Docker build context to `.`.
6. Choose a region near the Neon project.
7. Set health check path to `/health/live`.
8. Add every environment variable in the next section.
9. Deploy.

## Required Render environment variables

| Variable | Example or guidance | Secret |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Pooled Neon Npgsql connection with `SSL Mode=Require` | Yes |
| `Jwt__SigningKey` | Random value at least 32 characters long | Yes |
| `Jwt__Issuer` | `ai-knowledge-assistant` | No |
| `Jwt__Audience` | `ai-knowledge-assistant-api` | No |
| `AI__Provider` | `Groq` | No |
| `AI__ApiKey` | Groq API key | Yes |
| `AI__Endpoint` | `https://api.groq.com/openai/v1/chat/completions` | No |
| `AI__Model` | `llama-3.1-8b-instant` | No |
| `AI__EmbeddingModel` | `local-hash-embedding` | No |

Recommended supporting variables:

| Variable | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Storage__UploadsPath` | `/tmp/uploads` |

ASP.NET Core maps double underscores to configuration separators, so `AI__ApiKey` overrides `AI:ApiKey` and `ConnectionStrings__DefaultConnection` overrides the placeholder in `appsettings.json`.

## 6. Validate the deployment

Render assigns an HTTPS URL such as `https://ai-knowledge-assistant-api.onrender.com`.

```text
GET https://YOUR-SERVICE.onrender.com/health/live
GET https://YOUR-SERVICE.onrender.com/health/ready
GET https://YOUR-SERVICE.onrender.com/health
```

- `/health/live` verifies the process and is used by Render during deploys.
- `/health/ready` verifies Neon connectivity, Groq configuration, and writable temporary storage.
- `/health` returns the combined health report.

Swagger is intentionally enabled only in Development. Test Production endpoints with the frontend, an HTTP client, or an API client collection.

## 7. Troubleshooting

### Render cannot detect an open port

- Confirm the latest code includes `PORT` handling in `Program.cs`.
- Confirm the service uses the repository Dockerfile and has not overridden its Docker command.

### Neon connection fails

- Use Npgsql key/value format and include `SSL Mode=Require`.
- Confirm the database, role, password, and endpoint host are from the same Neon branch.
- Use the pooled endpoint for runtime and the direct endpoint for migrations.

### Migration cannot create `vector`

- Run `CREATE EXTENSION IF NOT EXISTS vector;` in the Neon SQL Editor.
- Rerun `dotnet ef database update` with the direct connection.

### Readiness is unhealthy

- Check Render logs without printing secret values.
- Confirm all required variables are configured with the exact double-underscore names.
- Confirm `/tmp/uploads` is writable and Neon is awake/reachable.

### Uploaded files disappear

This is expected on a free Render web service because its filesystem is ephemeral. Database metadata remains in Neon, but the file itself can disappear. Add S3-compatible object storage before treating this as a production deployment.

## Local validation

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
docker build -f ai-knowledge-assistant.Api/Dockerfile -t ai-knowledge-assistant-api:render .
```

No real connection strings, JWT secrets, or Groq keys belong in tracked configuration files.

## Official references

- [Render Docker deployments](https://render.com/docs/docker)
- [Render web services and port binding](https://render.com/docs/web-services)
- [Render health checks](https://render.com/docs/health-checks)
- [Render free instance limitations](https://render.com/docs/free)
- [Neon connection pooling](https://neon.com/docs/connect/connection-pooling)
- [Neon pgvector setup](https://neon.com/docs/ai/ai-concepts)
