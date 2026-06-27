# Render API Verification

Use this checklist to verify the deployed ASP.NET Core API on Render without placing credentials or tokens in source control.

## Deployment URL

```text
https://ai-knowledge-assistant-api-h4bx.onrender.com
```

The root route returning `404 Not Found` is expected because this service exposes API routes rather than a homepage.

## Temporarily enable Swagger

Swagger remains disabled by default in Production. In the Render service's environment settings, add:

```text
Swagger__Enabled=true
```

Save and deploy the environment change. Remove the variable or set it to `false` after verification.

## 1. Verify liveness

Open:

```text
https://ai-knowledge-assistant-api-h4bx.onrender.com/health/live
```

Expected result: HTTP `200 OK` with a healthy response.

For dependency readiness, also check:

```text
https://ai-knowledge-assistant-api-h4bx.onrender.com/health/ready
```

## 2. Open Swagger

After enabling the Render environment variable, open:

```text
https://ai-knowledge-assistant-api-h4bx.onrender.com/swagger
```

The page should display Authentication, Documents, Search, Chat, Conversations, Admin, and health endpoints. Swagger includes JWT Bearer authorization.

## 3. Register a user

In Swagger, open `POST /api/auth/register`, select **Try it out**, and submit:

```json
{
  "email": "render.tester@example.com",
  "password": "StrongPass123!"
}
```

Expected result: HTTP `201 Created`. Use a different email if the account already exists.

## 4. Login

Open `POST /api/auth/login` and submit:

```json
{
  "email": "render.tester@example.com",
  "password": "StrongPass123!"
}
```

Expected result: HTTP `200 OK`. Copy the returned `accessToken` without saving it in documentation, logs, screenshots, or source control.

## 5. Authorize Swagger

1. Select **Authorize** near the top of Swagger UI.
2. Paste the access token into the Bearer field.
3. Select **Authorize** and close the dialog.

Swagger's HTTP Bearer scheme normally adds the `Bearer` prefix automatically, so paste only the token unless the dialog explicitly requests `Bearer <token>`.

## 6. Upload a document

Open `POST /api/documents/upload` and select a non-empty PDF or DOCX file in the multipart `file` field.

Expected result: HTTP `200 OK` with document metadata and a status such as `Pending`. Save the returned document ID, then poll:

```text
GET /api/documents/{id}
```

Wait for `Indexed`, then verify extracted chunks:

```text
GET /api/documents/{id}/chunks?page=1&pageSize=50
```

Render's free filesystem is ephemeral. This upload is suitable for verification, but durable production files require external object storage.

## 7. Ask a question

After the document is `Indexed`, open `POST /api/chat/ask` and submit a question answerable from its content:

```json
{
  "question": "What are the main points in the uploaded document?",
  "conversationId": null
}
```

Expected result: HTTP `200 OK` containing:

- `answer`
- `conversationId`
- `userMessageId`
- `assistantMessageId`
- `citations`

If no relevant indexed chunks exist, the endpoint returns a validation response instead of asking Groq without document context.

## Disable Swagger after verification

In Render, remove `Swagger__Enabled` or set it to:

```text
false
```

Redeploy the environment change and confirm `/swagger` no longer opens in Production.
