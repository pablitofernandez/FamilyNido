import { ChangeDetectionStrategy, Component, ElementRef, computed, input, output, signal, viewChild } from '@angular/core';

/** Minimal shape needed to render a mention candidate in the popover. */
export interface MentionMember {
  id: string;
  displayName: string;
  colorHex: string;
}

/**
 * Textarea wrapper with `@DisplayName` autocomplete. Detects when the caret
 * sits inside an `@…` token, opens a popover above the textarea with the
 * filtered list of family members and lets the user pick with arrow keys
 * + Enter/Tab or with the mouse. Picking inserts `@<DisplayName> ` at the
 * caret. The component owns no markdown logic — it just composes plain text
 * and emits `valueChange` on every edit, exactly like a regular textarea.
 */
@Component({
  selector: 'fn-mention-textarea',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mention-textarea.component.html',
  styleUrl: './mention-textarea.component.css',
})
export class MentionTextareaComponent {
  readonly value = input.required<string>();
  readonly valueChange = output<string>();
  readonly members = input.required<MentionMember[]>();
  readonly placeholder = input<string>('');
  readonly rows = input<number>(4);
  readonly maxLength = input<number>(4000);
  /** Forwarded to the inner textarea so callers can keep their Tailwind styling. */
  readonly textareaClass = input<string>('');
  /** Fired when an image is pasted from the clipboard into the textarea. */
  readonly pasteImage = output<File>();

  private readonly textareaRef = viewChild.required<ElementRef<HTMLTextAreaElement>>('textarea');

  /** `null` when the popover is closed; the typed query (without `@`) when open. */
  protected readonly query = signal<string | null>(null);
  protected readonly highlight = signal(0);

  protected readonly active = computed(() => this.query() !== null);

  protected readonly suggestions = computed<MentionMember[]>(() => {
    const q = (this.query() ?? '').toLowerCase();
    const all = this.members();
    if (q.length === 0) return all.slice(0, 6);
    return all
      .filter((m) => m.displayName.toLowerCase().includes(q))
      .slice(0, 6);
  });

  /** Index in the textarea value where the active `@` token starts. */
  private mentionStart = -1;

  protected onInput(event: Event): void {
    const ta = event.target as HTMLTextAreaElement;
    this.valueChange.emit(ta.value);
    this.refreshState(ta);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (!this.active()) return;
    const list = this.suggestions();
    if (list.length === 0) {
      if (event.key === 'Escape') {
        event.preventDefault();
        this.close();
      }
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.highlight.set((this.highlight() + 1) % list.length);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.highlight.set((this.highlight() - 1 + list.length) % list.length);
        break;
      case 'Enter':
      case 'Tab':
        event.preventDefault();
        this.pick(list[this.highlight()]);
        break;
      case 'Escape':
        event.preventDefault();
        this.close();
        break;
      default:
        break;
    }
  }

  protected onClick(event: MouseEvent): void {
    // Caret may have moved without triggering `input` (mouse click), so refresh.
    this.refreshState(event.target as HTMLTextAreaElement);
  }

  protected onBlur(): void {
    // Defer so a click on a suggestion can still fire `mousedown` first.
    setTimeout(() => this.close(), 120);
  }

  /**
   * Intercept paste events to look for an image on the clipboard. When the
   * user copies a screenshot or an image from a webpage and pastes it into
   * the textarea, the native behaviour is to paste nothing useful (image
   * bytes can't be represented as text). We hijack that case and emit the
   * `File` to the parent, which can upload it as an attachment. Plain-text
   * pastes are left untouched.
   */
  protected onPaste(event: ClipboardEvent): void {
    const items = event.clipboardData?.items;
    if (!items) return;
    for (const item of items) {
      if (item.kind === 'file' && item.type.startsWith('image/')) {
        const file = item.getAsFile();
        if (file) {
          event.preventDefault();
          this.pasteImage.emit(file);
          return;
        }
      }
    }
  }

  protected pick(member: MentionMember): void {
    if (this.mentionStart < 0) return;
    const ta = this.textareaRef().nativeElement;
    const before = ta.value.slice(0, this.mentionStart);
    const after = ta.value.slice(ta.selectionStart);
    const inserted = `@${member.displayName} `;
    const next = `${before}${inserted}${after}`;
    this.valueChange.emit(next);
    this.close();

    // Restore caret right after the inserted token on the next tick — the
    // value() input updates first, then we reposition.
    queueMicrotask(() => {
      const pos = before.length + inserted.length;
      ta.setSelectionRange(pos, pos);
      ta.focus();
    });
  }

  private refreshState(ta: HTMLTextAreaElement): void {
    const text = ta.value;
    const caret = ta.selectionStart;

    // Walk backwards from the caret looking for `@`. Stop at whitespace —
    // mentions never span across a space.
    let i = caret - 1;
    while (i >= 0) {
      const c = text[i];
      if (c === '@') {
        const prev = i === 0 ? '' : text[i - 1];
        // Boundary check: `@` must follow start-of-text or whitespace, otherwise
        // it's part of an email address.
        if (i === 0 || /\s/.test(prev)) {
          const fragment = text.slice(i + 1, caret);
          if (!/\s/.test(fragment)) {
            this.mentionStart = i;
            this.query.set(fragment);
            this.highlight.set(0);
            return;
          }
        }
        break;
      }
      if (/\s/.test(c)) break;
      i--;
    }
    this.close();
  }

  private close(): void {
    if (this.query() !== null) {
      this.query.set(null);
    }
    this.highlight.set(0);
    this.mentionStart = -1;
  }
}
