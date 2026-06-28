import { DocumentDto, DocumentStatus } from '../../core/models/document.model';
import { resolveDocumentAvailability } from './chat.component';

describe('Chat document readiness', () => {
  it('keeps chat disabled when no indexed documents exist', () => {
    expect(resolveDocumentAvailability([])).toBe('none');
    expect(resolveDocumentAvailability([
      createDocument(DocumentStatus.Pending),
      createDocument(DocumentStatus.Processing)
    ])).toBe('processing');
  });

  it('enables chat when at least one document is indexed', () => {
    expect(resolveDocumentAvailability([
      createDocument(DocumentStatus.Processing),
      createDocument(DocumentStatus.Indexed)
    ])).toBe('ready');
  });
});

function createDocument(status: DocumentStatus): DocumentDto {
  return {
    id: crypto.randomUUID(),
    fileName: 'stored.pdf',
    originalFileName: 'document.pdf',
    contentType: 'application/pdf',
    filePath: 'uploads/stored.pdf',
    uploadedAt: new Date().toISOString(),
    status,
    versionNumber: 1,
    isDeleted: false
  };
}
