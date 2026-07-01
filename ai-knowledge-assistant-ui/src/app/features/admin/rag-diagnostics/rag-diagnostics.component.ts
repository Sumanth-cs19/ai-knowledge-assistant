import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ToastrService } from 'ngx-toastr';

import {
  RagDocumentDetailDto,
  RagDocumentDiagnosticDto,
  RagTestResponseDto
} from '../../../core/models/admin.model';
import { AdminService } from '../../../core/services/admin.service';

@Component({
  selector: 'app-rag-diagnostics',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule
  ],
  templateUrl: './rag-diagnostics.component.html',
  styleUrl: './rag-diagnostics.component.scss'
})
export class RagDiagnosticsComponent {
  protected readonly documents = signal<RagDocumentDiagnosticDto[]>([]);
  protected readonly details = signal<Record<string, RagDocumentDetailDto>>({});
  protected readonly testResult = signal<RagTestResponseDto | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly isTesting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly documentControl = new FormControl('', { nonNullable: true });
  protected readonly questionControl = new FormControl('What is this document about?', { nonNullable: true });

  private readonly adminService = inject(AdminService);
  private readonly toastr = inject(ToastrService);

  constructor() {
    this.loadDocuments();
  }

  protected loadDetail(documentId: string): void {
    if (this.details()[documentId]) {
      return;
    }

    this.adminService.getRagDocument(documentId).subscribe({
      next: (detail) => this.details.update((items) => ({ ...items, [documentId]: detail })),
      error: () => this.toastr.error('Could not load RAG document details.', 'Diagnostics failed')
    });
  }

  protected runTest(): void {
    const documentId = this.documentControl.value;
    const question = this.questionControl.value.trim();
    if (!documentId || !question || this.isTesting()) {
      this.toastr.warning('Select a document and enter a question.', 'Test query incomplete');
      return;
    }

    this.isTesting.set(true);
    this.testResult.set(null);
    this.adminService.testRag(documentId, question).subscribe({
      next: (result) => {
        this.testResult.set(result);
        this.isTesting.set(false);
      },
      error: (error: unknown) => {
        this.isTesting.set(false);
        this.toastr.error(this.errorText(error), 'RAG test failed');
      }
    });
  }

  protected statusLabel(status: number | string): string {
    const labels: Record<number, string> = { 1: 'Pending', 2: 'Processing', 3: 'Indexed', 4: 'Failed' };
    return typeof status === 'number' ? (labels[status] ?? 'Unknown') : status;
  }

  private loadDocuments(): void {
    this.adminService.getRagDocuments().subscribe({
      next: (documents) => {
        this.documents.set(documents);
        if (documents.length > 0) {
          this.documentControl.setValue(documents[0].documentId);
        }
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(this.errorText(error));
        this.isLoading.set(false);
      }
    });
  }

  private errorText(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 403) {
      return 'Admin access is required.';
    }
    return 'The diagnostics API could not be reached.';
  }
}
