import { PercentPipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';

import { ChatCitationDto } from '../../../../core/models/chat.model';

@Component({
  selector: 'app-citation',
  imports: [PercentPipe, MatExpansionModule, MatIconModule],
  templateUrl: './citation.component.html',
  styleUrl: './citation.component.scss'
})
export class CitationComponent {
  @Input({ required: true }) citation!: ChatCitationDto;
  @Output() openDocument = new EventEmitter<string>();
}
