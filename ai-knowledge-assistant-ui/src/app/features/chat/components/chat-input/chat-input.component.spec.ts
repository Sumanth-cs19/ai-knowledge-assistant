import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChatInputComponent } from './chat-input.component';

describe('ChatInputComponent', () => {
  let fixture: ComponentFixture<ChatInputComponent>;
  let component: ChatInputComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChatInputComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ChatInputComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('prevents native form submission and emits the question', () => {
    let emittedQuestion: string | undefined;
    component.sendMessage.subscribe((question) => emittedQuestion = question);

    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = '  What is in this document?  ';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    const submitEvent = new Event('submit', { bubbles: true, cancelable: true });
    form.dispatchEvent(submitEvent);

    expect(submitEvent.defaultPrevented).toBe(true);
    expect(emittedQuestion).toBe('What is in this document?');
  });

  it('does not submit while document-backed chat is unavailable', () => {
    let emittedQuestion: string | undefined;
    component.sendMessage.subscribe((question) => emittedQuestion = question);
    fixture.componentRef.setInput('isUnavailable', true);

    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = 'Can I ask without a document?';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    const sendButton = fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(sendButton.disabled).toBe(true);
    expect(emittedQuestion).toBeUndefined();
  });
});
