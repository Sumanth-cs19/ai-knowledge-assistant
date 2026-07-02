# Local Setup

This guide runs the Angular frontend, ASP.NET Core API, and PostgreSQL with pgvector on a development machine.

## Prerequisites

- .NET 8 SDK
- EF Core CLI 8.x: `dotnet tool install --global dotnet-ef --version 8.*`
- Node.js 20 or newer and npm
- Docker Desktop
- A Groq API key, or a configured local Ollama provider

## 1. Configure Local Secrets

```powershell
Copy-Item .env.example .env
```

Replace placeholder passwords and keys in `.env`. The file is ignored by Git.

For `dotnet run`, use environment variables, .NET user secrets, or the ignored `ai-knowledge-assistant.Api/appsettings.Development.json`. Do not add secrets to tracked appsettings files.

Example user-secret commands:

```powershell
dotnet user-secrets init --project .\ai-knowledge-assistant.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_LOCAL_CONNECTION" --project .\ai-knowledge-assistant.Api
dotnet user-secrets set "Jwt:SigningKey" "YOUR_LONG_RANDOM_SIGNING_KEY" --project .\ai-knowledge-assistant.Api
dotnet user-secrets set "AI:ApiKey" "YOUR_GROQ_KEY" --project .\ai-knowledge-assistant.Api
```

## 2. Start PostgreSQL and pgvector

Use the repository Compose service:

```powershell
docker compose up -d postgres
docker compose ps
```

Verify pgvector:

```powershell
docker compose exec postgres psql -U postgres -d AIKnowledgeAssistant -c "CREATE EXTENSION IF NOT EXISTS vector;"
docker compose exec postgres psql -U postgres -d AIKnowledgeAssistant -c "SELECT extname FROM pg_extension WHERE extname = 'vector';"
```

## 3. Apply Migrations

```powershell
dotnet restore
dotnet ef database update `
  --project .\ai-knowledge-assistant.Infrastructure `
  --startup-project .\ai-knowledge-assistant.Api
```

## 4. Run the API

```powershell
dotnet run --project .\ai-knowledge-assistant.Api --launch-profile http
```

- API: `http://localhost:5160`
- Swagger: `http://localhost:5160/swagger`
- Health: `http://localhost:5160/health/live`

## 5. Run Angular

```powershell
cd .\ai-knowledge-assistant-ui
npm install
npm start
```

- Frontend: `http://localhost:4200`
- Development API configuration: `http://localhost:5160/api`

## 6. Validate

```powershell
dotnet build
dotnet test
cd .\ai-knowledge-assistant-ui
npm run build
npm test -- --watch=false
```

Then follow [manual-test-flow.md](manual-test-flow.md).

## Full Docker Option

To run the UI, API, and database together:

```powershell
docker compose up --build
```

The local override exposes the frontend on port `4200` and the API on port `5000`.

## Common Problems

- **Port 5432 already used:** stop the conflicting local PostgreSQL service or change the Compose port.
- **pgvector unavailable:** verify the image is `pgvector/pgvector:pg16` and run the extension command above.
- **401 responses:** log in again; access tokens expire after 15 minutes by default.
- **Groq failure:** confirm the provider, endpoint, model, and API key without printing the key.
- **Document remains Failed:** use a text-based PDF/DOCX; scanned documents require OCR.
