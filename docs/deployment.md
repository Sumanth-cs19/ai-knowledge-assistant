# Deployment

The reference deployment uses Vercel for Angular, Render for ASP.NET Core, Neon for PostgreSQL/pgvector, and Groq for chat completion.

## Production Topology

| Component | Platform | Configuration |
| --- | --- | --- |
| Angular UI | Vercel | Root: `ai-knowledge-assistant-ui`; output: `dist/ai-knowledge-assistant-ui/browser` |
| ASP.NET Core API | Render | Root Docker context; `ai-knowledge-assistant.Api/Dockerfile` |
| PostgreSQL + pgvector | Neon | Pooled runtime connection; direct migration connection |
| AI provider | Groq | Secret API key in Render environment variables |

## 1. Neon

1. Create a Neon PostgreSQL project.
2. Enable pgvector with `CREATE EXTENSION IF NOT EXISTS vector;`.
3. Save the direct and pooled connection strings securely.
4. Apply EF migrations using the direct connection.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "YOUR_DIRECT_NEON_CONNECTION"
dotnet ef database update --project .\ai-knowledge-assistant.Infrastructure --startup-project .\ai-knowledge-assistant.Api
Remove-Item Env:ConnectionStrings__DefaultConnection
Remove-Item Env:ASPNETCORE_ENVIRONMENT
```

## 2. Render API

The root `render.yaml` defines the service and health path. Create a Render Blueprint from the repository or configure a Docker web service manually.

Required secrets:

- `ConnectionStrings__DefaultConnection`: pooled Neon connection
- `Jwt__SigningKey`: long random secret
- `AI__ApiKey`: Groq API key

Required non-secret configuration is documented in `.env.example` and `render.yaml`. Confirm `/health/live` and `/health/ready` after deployment.

## 3. Vercel Frontend

1. Import the GitHub repository.
2. Set Root Directory to `ai-knowledge-assistant-ui`.
3. Use the Angular preset.
4. Build with `npm run build`.
5. Set Output Directory to `dist/ai-knowledge-assistant-ui/browser`.

The committed `vercel.json` provides SPA routing fallback. The production Angular environment contains the public Render API URL and no secret values.

## 4. CORS

Render must allow the Vercel origin:

```text
Cors__AllowedOrigins__0=https://YOUR-FRONTEND.vercel.app
```

Do not combine credentials with `AllowAnyOrigin`. See [vercel-render-cors.md](vercel-render-cors.md).

## 5. Deployment Validation

1. Open the Vercel application.
2. Register and log in.
3. Upload a text-based PDF or DOCX.
4. Wait for `Indexed` status.
5. Ask a question answerable from the document.
6. Confirm the streamed answer and Sources section.
7. Check `/health/live` and `/health/ready`.

## Security Checklist

- No real keys or database credentials in Git
- Production Swagger disabled after temporary verification
- HTTPS-only public endpoints
- Strong JWT signing key
- Neon SSL required
- Vercel origin explicitly allowed by CORS
- Logs contain metadata, never API keys or tokens

For provider-specific detail, see [deployment-render-neon.md](deployment-render-neon.md).
