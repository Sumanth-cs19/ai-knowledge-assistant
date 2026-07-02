import { PercentPipe } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';

import { ChatCitationDto } from '../../../../core/models/chat.model';

interface CitationGroup {
  documentId: string;
  documentName: string;
  citations: ChatCitationDto[];
}

@Component({
  selector: 'app-citation',
  imports: [PercentPipe, MatExpansionModule, MatIconModule],
  templateUrl: './citation.component.html',
  styleUrl: './citation.component.scss'
})
export class CitationComponent implements OnChanges {
  @Input({ required: true }) citations: ChatCitationDto[] = [];
  @Output() openDocument = new EventEmitter<string>();

  protected groups: CitationGroup[] = [];

  ngOnChanges(): void {
    const groups = new Map<string, CitationGroup>();

    for (const citation of this.citations) {
      const documentName = citation.originalFileName || citation.documentName || 'Uploaded document';
      const key = citation.documentId || documentName;
      const group = groups.get(key);

      if (group) {
        group.citations.push(citation);
      } else {
        groups.set(key, {
          documentId: citation.documentId,
          documentName,
          citations: [citation]
        });
      }
    }

    this.groups = [...groups.values()];
  }

  protected chunkLabel(count: number): string {
    return `${count} referenced ${count === 1 ? 'chunk' : 'chunks'}`;
  }
}
