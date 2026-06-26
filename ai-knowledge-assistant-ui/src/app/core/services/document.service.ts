import { HttpClient, HttpEvent } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import {
  DocumentChunkDto,
  DocumentDto,
  DocumentVersionDto,
  PagedResponse,
  UploadDocumentResponse
} from '../models/document.model';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly documentsUrl = `${APP_CONFIG.apiBaseUrl}/documents`;

  uploadDocument(file: File): Observable<HttpEvent<UploadDocumentResponse>> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.http.post<UploadDocumentResponse>(`${this.documentsUrl}/upload`, formData, {
      observe: 'events',
      reportProgress: true
    });
  }

  getMyDocuments(): Observable<DocumentDto[]> {
    return this.http.get<DocumentDto[]>(`${this.documentsUrl}/my-documents`);
  }

  getDocumentById(id: string): Observable<DocumentDto> {
    return this.http.get<DocumentDto>(`${this.documentsUrl}/${id}`);
  }

  deleteDocument(id: string): Observable<void> {
    return this.http.delete<void>(`${this.documentsUrl}/${id}`);
  }

  reindexDocument(id: string): Observable<void> {
    return this.http.post<void>(`${this.documentsUrl}/${id}/reindex`, {});
  }

  getDocumentVersions(id: string): Observable<DocumentVersionDto[]> {
    return this.http.get<DocumentVersionDto[]>(`${this.documentsUrl}/${id}/versions`);
  }

  getDocumentChunks(id: string, page = 1, pageSize = 20): Observable<PagedResponse<DocumentChunkDto>> {
    return this.http.get<PagedResponse<DocumentChunkDto>>(`${this.documentsUrl}/${id}/chunks`, {
      params: {
        page,
        pageSize
      }
    });
  }
}
