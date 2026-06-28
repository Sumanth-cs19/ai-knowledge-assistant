# OCR roadmap

The current zero-cost document pipeline extracts embedded text from PDF and DOCX files. It does not run OCR.

Documents whose extracted text is mostly symbols, control characters, or unreadable fragments are marked `Failed` with this message:

```text
Text extraction quality is low. This PDF may be scanned or handwritten and may require OCR.
```

A future local-only extension can add Tesseract OCR behind `ITextExtractionService`. It should remain opt-in because OCR increases processing time and container memory usage. No paid OCR service is required or configured.
