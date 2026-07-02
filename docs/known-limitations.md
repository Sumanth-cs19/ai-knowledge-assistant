# Known Limitations and Roadmap

## Current Limitations

### Local Hash Embeddings

The zero-cost MVP uses deterministic local hash embeddings. They are useful for development but less semantically accurate than a trained embedding model. The API applies provider-aware thresholds and labels these citation scores as `local-fallback`.

### No OCR

PDF extraction expects selectable text. Scanned, image-only, or handwritten PDFs may fail extraction-quality validation. Paid OCR is intentionally not included. See [ocr-roadmap.md](ocr-roadmap.md) for a future Tesseract-based option.

### Free Hosting Cold Starts

Render free services may sleep while idle. The first login, upload, or chat request after inactivity can be noticeably slower. Neon and Groq free tiers may also introduce latency.

### AI Provider Limits

Groq rate limits and temporary provider availability can affect responses. The API returns consistent errors and does not fall back to ungrounded general answers.

### Ephemeral File Storage

The reference Render deployment writes uploads to an ephemeral filesystem. PostgreSQL metadata survives, but original files may disappear after a restart or deployment. Durable production use requires object storage.

### Retrieval Scope

Hybrid retrieval and broad-context summarization are an MVP foundation. Very large documents, tables, complex formatting, and cross-document reasoning can require better chunking, reranking, and context selection.

## Future Improvements

- Real multilingual embedding models with batch generation
- OCR support with Tesseract and confidence reporting
- Document-level summarization and summary caching
- Advanced retrieval evaluation, reranking, and analytics
- Multi-tenant organizations, workspaces, and scoped roles
- S3-compatible file storage with signed URLs
- Email invitations, verification, and password reset
- Provider failover, budgets, and usage dashboards
- End-to-end browser tests for production workflows
