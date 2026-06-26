import { AfterViewChecked, Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { marked } from 'marked';
import hljs from 'highlight.js/lib/common';

import { ChatUiMessage } from '../../../../core/models/chat.model';
import { CitationComponent } from '../citation/citation.component';
import { TypingIndicatorComponent } from '../typing-indicator/typing-indicator.component';

@Component({
  selector: 'app-chat-message',
  imports: [DatePipe, MatButtonModule, MatIconModule, CitationComponent, TypingIndicatorComponent],
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss'
})
export class ChatMessageComponent implements AfterViewChecked {
  @Input({ required: true }) message!: ChatUiMessage;
  @Output() copyMessage = new EventEmitter<string>();
  @Output() regenerate = new EventEmitter<void>();
  @Output() openDocument = new EventEmitter<string>();
  @ViewChild('contentEl') private readonly contentEl?: ElementRef<HTMLElement>;
  private copyButtonsAttached = false;

  constructor(private readonly sanitizer: DomSanitizer) {}

  ngAfterViewChecked(): void {
    this.contentEl?.nativeElement.querySelectorAll('pre code').forEach((block) => {
      hljs.highlightElement(block as HTMLElement);
    });
    this.attachCopyButtons();
  }

  protected markdown(): SafeHtml {
    this.copyButtonsAttached = false;
    const html = marked.parse(this.message.content || '', {
      async: false,
      breaks: true,
      gfm: true
    }) as string;

    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  private attachCopyButtons(): void {
    const host = this.contentEl?.nativeElement;
    if (!host || this.copyButtonsAttached) {
      return;
    }

    host.querySelectorAll('pre').forEach((pre) => {
      if (pre.querySelector('.copy-code-button')) {
        return;
      }

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'copy-code-button';
      button.textContent = 'Copy code';
      button.setAttribute('aria-label', 'Copy code block');
      button.addEventListener('click', () => {
        void navigator.clipboard.writeText(pre.textContent?.replace('Copy code', '').trim() ?? '');
      });
      pre.prepend(button);
    });

    this.copyButtonsAttached = true;
  }
}
