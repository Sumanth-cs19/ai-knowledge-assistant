# Manual Backend Test Flow

This guide validates the complete local workflow through Swagger: authentication, document indexing, grounded Groq chat, feedback, and conversation history.

For the deployed portfolio flow, open `https://ai-knowledge-assistant-seven.vercel.app` and follow the same sequence through the Angular UI. The live API is `https://ai-knowledge-assistant-api-h4bx.onrender.com`.

## Prerequisites

- Docker Desktop is running.
- .NET 8 SDK is installed.
- `ai-knowledge-assistant.Api/appsettings.Development.json` contains local values for PostgreSQL, JWT, and Groq. This file is intentionally excluded from Git.
- The Groq model is `llama-3.1-8b-instant` and the embedding model is `local-hash-embedding`.

Run commands from the repository root unless noted otherwise.

## 1. Start PostgreSQL with pgvector

Start Docker Desktop, then verify the existing database container:

```powershell
docker ps --filter "name=pgvector-db"
docker exec pgvector-db psql -U postgres -d AIKnowledgeAssistant -c "SELECT extname FROM pg_extension WHERE extname = 'vector';"
docker exec pgvector-db psql -U postgres -d AIKnowledgeAssistant -c "\dt"
```

The extension query should return `vector`. The application tables should include:

- `Users`
- `Roles`
- `RefreshTokens`
- `Documents`
- `DocumentChunks`
- `Conversations`
- `ChatMessages`
- `ChatFeedback`
- `__EFMigrationsHistory`

If the migration is pending, apply it before starting the API:

```powershell
dotnet ef database update --project ai-knowledge-assistant.Infrastructure --startup-project ai-knowledge-assistant.Api
```

### Port 5432 conflict on Windows

Only one PostgreSQL server can own `localhost:5432`. If `dotnet ef database update` reports that `vector` is unavailable while `pgvector-db` has it, a local Windows PostgreSQL service is probably intercepting the connection. In an elevated PowerShell window, run:

```powershell
Stop-Service postgresql-x64-16
docker restart pgvector-db
```

Then rerun the migration command. This repository uses the existing `pgvector-db`; do not create a second database container.

## 2. Run the API and open Swagger

```powershell
dotnet run --project ai-knowledge-assistant.Api --launch-profile http
```

Open:

```text
http://localhost:5160/swagger
```

Swagger is available only when the API environment is `Development`.

## 3. Register a user

Open `POST /api/auth/register`, select **Try it out**, and submit:

```json
{
  "email": "manual.tester@example.com",
  "password": "StrongPass123!"
}
```

Expected result: `201 Created`. If the email is already registered, use a different email or continue with login.

## 4. Login and copy the access token

Open `POST /api/auth/login` and submit:

```json
{
  "email": "manual.tester@example.com",
  "password": "StrongPass123!"
}
```

Expected result: `200 OK`. Copy the `accessToken` value from the response. The response also contains a refresh token and token expiration timestamps.

## 5. Authorize Swagger

1. Select **Authorize** at the top of Swagger UI.
2. Paste the access token into the Bearer authorization field.
3. Select **Authorize**, then close the dialog.

Swagger adds the `Bearer` prefix automatically. Paste only the JWT unless the dialog explicitly requests the full `Bearer <token>` value.

Protected endpoints should now succeed. Without a valid token, they should return `401 Unauthorized`.

## 6. Upload a PDF or DOCX

Open `POST /api/documents/upload`, select **Try it out**, and choose a non-empty `.pdf` or `.docx` file in the `file` field.

Expected result: `200 OK`. Save the returned document `id`. New documents are returned with `Pending` status because extraction, chunking, and local embedding generation run asynchronously.

Only these combinations are accepted:

| Extension | Content type |
| --- | --- |
| `.pdf` | `application/pdf` |
| `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |

## 7. List uploaded documents

Open `GET /api/documents/my-documents`.

Expected result: `200 OK`, with the uploaded document present. Confirm its original file name, version number, upload timestamp, and status.

## 8. Verify indexing status and chunks

Poll `GET /api/documents/{id}` using the document ID until the status becomes `Indexed` or `Failed`:

- `Pending`: waiting for the background worker.
- `Processing`: extraction, chunking, or embedding is running.
- `Indexed`: chunks and embeddings are ready for search/chat.
- `Failed`: inspect `errorMessage` and the API logs.

After the status is `Indexed`, call:

```text
GET /api/documents/{id}/chunks?page=1&pageSize=50
```

Expected result: one or more chunks containing extracted text. Do not test chat until chunks exist.

## 9. Ask a grounded question

Open `POST /api/chat/ask` and submit a question answerable from the uploaded document:

```json
{
  "question": "What are the main points in the uploaded document?",
  "conversationId": null
}
```

Expected result: `200 OK`. Omitting or setting `conversationId` to `null` creates a conversation automatically. To continue a conversation, send its returned ID in a later request.

## 10. Verify the chat response

Confirm the response contains:

- `answer`
- `conversationId`
- `userMessageId`
- `assistantMessageId`
- `citations`

Each citation can include the document ID/name, chunk ID/index, similarity score, and source snippet. Keep the `conversationId` and `assistantMessageId` for the next steps.

The answer is generated by Groq from retrieved document context. Embeddings remain local and do not call an external embedding API.

## 11. Submit feedback

Open `POST /api/chat/messages/{messageId}/feedback`. Set `messageId` to the `assistantMessageId` from the chat response, then submit:

```json
{
  "rating": 5,
  "comment": "The answer was accurate and the citations were useful."
}
```

Expected result: `200 OK`. Ratings must be between 1 and 5, and feedback is accepted only for the authenticated user's assistant messages.

## 12. Verify conversation history

First call:

```text
GET /api/conversations?page=1&pageSize=20
```

Confirm the returned list contains the chat's `conversationId`. Then call:

```text
GET /api/conversations/{conversationId}/messages?page=1&pageSize=50
```

Expected result: both the user question and assistant answer, ordered as stored in the conversation. This endpoint is the current equivalent of `GET /api/chat/history`.

## Troubleshooting

### Docker is not running

- Start Docker Desktop and wait until the engine is ready.
- Run `docker ps`.
- If `pgvector-db` exists but is stopped, run `docker start pgvector-db`.

### pgvector is missing

- Confirm the container image with `docker inspect pgvector-db --format '{{.Config.Image}}'`; it should be `pgvector/pgvector:pg16`.
- Run the extension check from Step 1 inside the container.
- If EF says pgvector is missing but the container query succeeds, resolve the Windows port 5432 conflict described in Step 1.

### 401 Unauthorized

- Login again and copy the new `accessToken`, not the refresh token.
- Reopen Swagger **Authorize** and replace the expired token.
- Ensure the lock icon shows the Bearer scheme is authorized.
- Tokens expire after 15 minutes by default.

### Groq API key is missing or rejected

- Set `AI:Provider` to `Groq` and provide `AI:ApiKey` in the ignored Development settings or use `AI__ApiKey` as an environment variable.
- Verify `AI:Endpoint` is `https://api.groq.com/openai/v1/chat/completions`.
- Never place the real key in tracked `appsettings.json` or logs.
- A provider failure is returned as `502 Bad Gateway`; inspect the API logs for status/model information.

### Upload fails

- Use the multipart field named `file`.
- Upload only non-empty PDF or DOCX files with matching content types.
- Confirm the access token is valid.
- Check that the configured `Storage:UploadsPath` is writable.

### Document has no chunks

- Wait until status is `Indexed`; processing is asynchronous.
- Check `errorMessage` when status is `Failed`.
- Confirm the document contains selectable text. Image-only/scanned PDFs require OCR, which is not part of the current extraction flow.
- Re-run processing with `POST /api/documents/{id}/reindex` after correcting the source file or configuration.

### Chat fails or returns no answer

- Confirm at least one owned document is `Indexed` and has chunks.
- Ask a question that is answerable from those chunks.
- Check Groq configuration and internet access.
- `400 Bad Request` with no relevant chunks means retrieval found no usable context.
- `429 Too Many Requests` means the chat rate limit was reached; wait for the configured window.
- `502 Bad Gateway` indicates a Groq provider failure.

## Final verification commands

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```
