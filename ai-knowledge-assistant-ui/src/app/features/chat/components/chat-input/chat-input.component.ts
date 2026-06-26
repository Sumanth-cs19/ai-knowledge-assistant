import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TextFieldModule } from '@angular/cdk/text-field';

@Component({
  selector: 'app-chat-input',
  imports: [ReactiveFormsModule, TextFieldModule, MatButtonModule, MatIconModule, MatInputModule],
  templateUrl: './chat-input.component.html',
  styleUrl: './chat-input.component.scss'
})
export class ChatInputComponent {
  @Input() isGenerating = false;
  @Output() sendMessage = new EventEmitter<string>();
  @Output() stopGeneration = new EventEmitter<void>();

  protected readonly messageControl = new FormControl('', { nonNullable: true });
  protected readonly maxLength = 4000;

  protected submit(): void {
    const value = this.messageControl.value.trim();
    if (!value || this.isGenerating) {
      return;
    }

    this.sendMessage.emit(value);
    this.messageControl.setValue('');
  }

  protected handleKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.submit();
    }
  }
}
