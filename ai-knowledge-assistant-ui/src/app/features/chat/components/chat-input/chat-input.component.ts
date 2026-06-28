import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
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
  @ViewChild('messageInput') private readonly messageInput?: ElementRef<HTMLTextAreaElement>;

  @Input() isGenerating = false;
  @Input() isUnavailable = false;
  @Output() sendMessage = new EventEmitter<string>();
  @Output() stopGeneration = new EventEmitter<void>();

  protected readonly form = new FormGroup({
    message: new FormControl('', { nonNullable: true })
  });
  protected readonly messageControl = this.form.controls.message;
  protected readonly maxLength = 4000;

  protected submit(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();

    const value = this.messageControl.value.trim();
    if (!value || this.isGenerating || this.isUnavailable) {
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

  focus(): void {
    this.messageInput?.nativeElement.focus();
  }
}
