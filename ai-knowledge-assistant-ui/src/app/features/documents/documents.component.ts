import { DatePipe, SlicePipe } from '@angular/common';
import { HttpErrorResponse, HttpEvent, HttpEventType } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

import {
  DocumentChunkDto,
  DocumentDto,
  DocumentStatus,
  DocumentVersionDto,
  PagedResponse,
  UploadDocumentResponse
} from '../../core/models/document.model';
import { DocumentService } from '../../core/services/document.service';
import {
  ConfirmationDialogComponent,
  ConfirmationDialogData
} from '../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { SkeletonComponent } from '../../shared/components/skeleton/skeleton.component';

@Component({
  selector: 'app-documents',
  imports: [
    DatePipe,
    SlicePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    SkeletonComponent
  ],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss'
})
export class DocumentsComponent {
  protected readonly DocumentStatus = DocumentStatus;
  protected readonly statusOptions = [
    { label: 'All statuses', value: 'all' },
    { label: 'Pending', value: DocumentStatus.Pending },
    { label: 'Processing', value: DocumentStatus.Processing },
    { label: 'Indexed', value: DocumentStatus.Indexed },
    { label: 'Failed', value: DocumentStatus.Failed }
  ];

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly statusControl = new FormControl<DocumentStatus | 'all'>('all', { nonNullable: true });

  protected readonly documents = signal<DocumentDto[]>([]);
  protected readonly selectedDocument = signal<DocumentDto | null>(null);
  protected readonly versions = signal<DocumentVersionDto[]>([]);
  protected readonly chunks = signal<PagedResponse<DocumentChunkDto> | null>(null);
  protected readonly activePanel = signal<'details' | 'versions' | 'chunks' | null>(null);
  protected readonly isDragOver = signal(false);
  protected readonly isUploading = signal(false);
  protected readonly uploadProgress = signal(0);
  protected readonly isLoadingDocuments = signal(true);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(5);

  protected readonly filteredDocuments = computed(() => {
    const query = this.searchControl.value.trim().toLowerCase();
    const status = this.statusControl.value;

    return this.documents().filter((document) => {
      const matchesSearch = !query
        || document.originalFileName.toLowerCase().includes(query)
        || document.fileName.toLowerCase().includes(query);
      const matchesStatus = status === 'all' || this.normalizeStatus(document.status) === status;

      return matchesSearch && matchesStatus;
    });
  });

  protected readonly pagedDocuments = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filteredDocuments().slice(start, start + this.pageSize());
  });

  private readonly documentService = inject(DocumentService);
  private readonly dialog = inject(MatDialog);
  private readonly toastr = inject(ToastrService);
  private readonly maxFileSizeBytes = 25 * 1024 * 1024;

  constructor() {
    this.loadDocuments();
    this.searchControl.valueChanges.subscribe(() => this.pageIndex.set(0));
    this.statusControl.valueChanges.subscribe(() => this.pageIndex.set(0));
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);

    const file = event.dataTransfer?.files.item(0);
    if (file) {
      this.uploadFile(file);
    }
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0);

    if (file) {
      this.uploadFile(file);
    }

    input.value = '';
  }

  protected viewDetails(document: DocumentDto): void {
    this.activePanel.set('details');
    this.versions.set([]);
    this.chunks.set(null);

    this.documentService.getDocumentById(document.id).subscribe({
      next: (response) => this.selectedDocument.set(response)
    });
  }

  protected viewVersions(document: DocumentDto): void {
    this.selectedDocument.set(document);
    this.activePanel.set('versions');
    this.chunks.set(null);

    this.documentService.getDocumentVersions(document.id).subscribe({
      next: (response) => this.versions.set(response)
    });
  }

  protected viewChunks(document: DocumentDto, page = 1): void {
    this.selectedDocument.set(document);
    this.activePanel.set('chunks');
    this.versions.set([]);

    this.documentService.getDocumentChunks(document.id, page, 10).subscribe({
      next: (response) => this.chunks.set(response)
    });
  }

  protected reindex(document: DocumentDto): void {
    this.documentService.reindexDocument(document.id).subscribe({
      next: () => {
        this.toastr.success('Re-indexing has been queued.', 'Document re-index');
        this.loadDocuments();
      }
    });
  }

  protected deleteDocument(document: DocumentDto): void {
    const dialogRef = this.dialog.open<ConfirmationDialogComponent, ConfirmationDialogData, boolean>(
      ConfirmationDialogComponent,
      {
        data: {
          title: 'Delete document',
          message: `Delete "${document.originalFileName}"? This removes it from your document list.`,
          confirmText: 'Delete',
          cancelText: 'Cancel'
        }
      }
    );

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.documentService.deleteDocument(document.id).subscribe({
        next: () => {
          this.toastr.success('Document deleted.', 'Delete complete');
          this.clearPanelIfSelected(document.id);
          this.loadDocuments();
        }
      });
    });
  }

  protected pageChanged(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected chunkPageChanged(event: PageEvent): void {
    const document = this.selectedDocument();
    if (!document) {
      return;
    }

    this.viewChunks(document, event.pageIndex + 1);
  }

  protected closePanel(): void {
    this.activePanel.set(null);
    this.selectedDocument.set(null);
    this.versions.set([]);
    this.chunks.set(null);
  }

  protected getStatusLabel(status: DocumentDto['status']): string {
    const normalizedStatus = this.normalizeStatus(status);
    return DocumentStatus[normalizedStatus] ?? String(status);
  }

  protected getStatusClass(status: DocumentDto['status']): string {
    return this.getStatusLabel(status).toLowerCase();
  }

  protected hasNoSearchResults(): boolean {
    return this.documents().length > 0 && this.filteredDocuments().length === 0;
  }

  private loadDocuments(): void {
    this.isLoadingDocuments.set(true);
    this.documentService.getMyDocuments().subscribe({
      next: (response) => {
        this.documents.set(response);
        this.isLoadingDocuments.set(false);
      },
      error: () => this.isLoadingDocuments.set(false)
    });
  }

  private uploadFile(file: File): void {
    const validationError = this.validateFile(file);
    if (validationError) {
      this.toastr.error(validationError, 'Upload blocked');
      return;
    }

    this.isUploading.set(true);
    this.uploadProgress.set(0);

    this.documentService.uploadDocument(file).pipe(
      finalize(() => this.isUploading.set(false))
    ).subscribe({
      next: (event) => this.handleUploadEvent(event),
      error: (error: unknown) => {
        this.uploadProgress.set(0);
        this.toastr.error(this.getUploadErrorMessage(error), 'Upload failed');
      }
    });
  }

  private handleUploadEvent(event: HttpEvent<UploadDocumentResponse>): void {
    if (event.type === HttpEventType.UploadProgress) {
      const total = event.total ?? 1;
      this.uploadProgress.set(Math.round((event.loaded / total) * 100));
      return;
    }

    if (event.type === HttpEventType.Response) {
      this.uploadProgress.set(100);
      this.toastr.success('Document uploaded and queued for indexing.', 'Upload complete');
      this.documents.update((items) => [event.body, ...items].filter((item): item is DocumentDto => item !== null));
    }
  }

  private validateFile(file: File): string | null {
    const lowerName = file.name.toLowerCase();
    const hasAllowedExtension = lowerName.endsWith('.pdf') || lowerName.endsWith('.docx');

    if (!hasAllowedExtension) {
      return 'Only PDF and DOCX files are supported.';
    }

    if (file.size <= 0) {
      return 'The selected file is empty.';
    }

    if (file.size > this.maxFileSizeBytes) {
      return 'Files must be 25 MB or smaller.';
    }

    return null;
  }

  private normalizeStatus(status: DocumentDto['status']): DocumentStatus {
    if (typeof status === 'number') {
      return status;
    }

    return DocumentStatus[status] ?? DocumentStatus.Pending;
  }

  private clearPanelIfSelected(documentId: string): void {
    if (this.selectedDocument()?.id === documentId) {
      this.closePanel();
    }
  }

  private getUploadErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && typeof error.error?.detail === 'string') {
      return error.error.detail;
    }

    if (error instanceof HttpErrorResponse && error.error?.errors) {
      return Object.values(error.error.errors as Record<string, string[]>).flat().join(' ');
    }

    return 'The document could not be uploaded. Please try again.';
  }
}
