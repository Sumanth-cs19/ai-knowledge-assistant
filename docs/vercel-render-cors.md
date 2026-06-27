# Vercel and Render CORS

The API reads allowed frontend origins from `Cors:AllowedOrigins`. Origins must include the scheme and host, with no trailing slash.

## Render configuration

Add this environment variable to the Render API service:

```text
Cors__AllowedOrigins__0=https://ai-knowledge-assistant-seven.vercel.app
```

Redeploy the service after saving the environment variable. The production app settings already contain this origin, while the environment variable makes the deployed origin explicit and allows it to be changed without rebuilding the image.

## Local development

Local Angular development allows:

```text
http://localhost:4200
https://localhost:4200
```

The API permits `GET`, `POST`, `PUT`, `DELETE`, and `OPTIONS` requests with the `Authorization`, `Content-Type`, and `Accept` headers. Authentication uses bearer tokens, so CORS credentials are not enabled.
