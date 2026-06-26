export enum DocumentStatus {
  Pending = 1,
  Processing = 2,
  Indexed = 3,
  Failed = 4
}

export interface DocumentDto {
  id: string;
  fileName: string;
  originalFileName: string;
  contentType: string;
  filePath: string;
  uploadedAt: string;
  status: DocumentStatus | keyof typeof DocumentStatus;
  errorMessage?: string | null;
  processedAt?: string | null;
  versionNumber: number;
  isDeleted: boolean;
}

export type DocumentVersionDto = DocumentDto;

export interface DocumentChunkDto {
  id: string;
  documentId: string;
  chunkIndex: number;
  content: string;
  createdAt: string;
}

export type UploadDocumentResponse = DocumentDto;

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
