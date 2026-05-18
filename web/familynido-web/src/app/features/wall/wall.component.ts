import { DatePipe, NgClass, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { FamilyMembersService } from '../../core/api/family-members.service';
import { FilesService } from '../../core/api/files.service';
import { WallService } from '../../core/api/wall.service';
import { AuthService } from '../../core/auth/auth.service';
import { FamilyMember } from '../../core/models/family-member';
import { ToggleReactionResult, WallComment, WallMessage } from '../../core/models/wall';
import { refreshOnFocus } from '../../core/realtime/refresh-on-focus';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { MentionMember, MentionTextareaComponent } from '../../shared/ui/mention-textarea/mention-textarea.component';

/** Quick-access emoji palette shown when the reactions picker opens. */
const REACTION_PALETTE = ['❤️', '👍', '🎉', '🙏', '😂', '😢', '🌸', '🔥'];

/**
 * "El muro" — family pinboard. Owns everything needed to render the feed,
 * publish a new message with an optional image, pin/unpin, react with emojis,
 * and add 1-level comments. Reloads the feed when the tab becomes visible
 * again so changes from other devices show up without manual refresh.
 */
@Component({
  selector: 'fn-wall',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent, DatePipe, IconComponent, MentionTextareaComponent, NgClass, NgTemplateOutlet],
  templateUrl: './wall.component.html',
  styleUrl: './wall.component.css',
})
export class WallComponent implements OnInit {
  private readonly wall = inject(WallService);
  private readonly files = inject(FilesService);
  private readonly membersApi = inject(FamilyMembersService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly reactionPalette = REACTION_PALETTE;

  /** Aria-labels and placeholders surfaced via bracketed bindings. */
  protected readonly newMessageAriaLabel = $localize`:@@wall.new-message-aria:Nuevo mensaje`;
  protected readonly closeAriaLabel = $localize`:@@wall.close-aria:Cerrar`;
  protected readonly pinAriaLabel = $localize`:@@wall.pin-aria:Fijar`;
  protected readonly unpinAriaLabel = $localize`:@@wall.unpin-aria:Despinchar`;
  protected readonly shareAriaLabel = $localize`:@@wall.share-aria:Compartir enlace al mensaje`;
  protected readonly composerPlaceholder = $localize`:@@wall.composer.placeholder:Recuerda sacar al perro 🐕. Escribe @ para mencionar.`;
  protected readonly commentPlaceholder = $localize`:@@wall.comment.placeholder:Responder… (usa @ para mencionar)`;

  protected commentCountLabel(count: number): string {
    return count === 1
      ? $localize`:@@wall.comment-count.one:${count}:N: comentario`
      : $localize`:@@wall.comment-count.many:${count}:N: comentarios`;
  }

  protected readonly pinned = signal<WallMessage[]>([]);
  protected readonly messages = signal<WallMessage[]>([]);
  protected readonly hasMore = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly members = signal<FamilyMember[]>([]);

  /** Composer state. */
  protected readonly composerOpen = signal(false);
  protected readonly composerText = signal('');
  protected readonly composerImageId = signal<string | null>(null);
  protected readonly composerImagePreview = signal<string | null>(null);
  protected readonly composerUploading = signal(false);
  /**
   * When non-null, the composer is acting as an editor for that message id —
   * Publish becomes "Guardar" and dispatches to <c>wall.update</c>.
   */
  protected readonly editingMessageId = signal<string | null>(null);
  /** True while the user is dragging a file over the composer area. */
  protected readonly composerDragOver = signal(false);

  /** Server-rendered HTML preview of `composerText` — kept ~350 ms behind. */
  protected readonly previewHtml = signal('');
  protected readonly previewLoading = signal(false);
  private previewTimer: ReturnType<typeof setTimeout> | null = null;

  /** Debounced preview fetcher: rerun whenever the composer text changes. */
  private readonly _previewEffect = effect(() => {
    const text = this.composerText().trim();
    const open = this.composerOpen();
    if (this.previewTimer) clearTimeout(this.previewTimer);
    if (!open || text.length === 0) {
      this.previewHtml.set('');
      this.previewLoading.set(false);
      return;
    }
    this.previewLoading.set(true);
    this.previewTimer = setTimeout(async () => {
      try {
        const res = await firstValueFrom(this.wall.preview(text));
        this.previewHtml.set(res.html);
      } catch {
        // Best-effort: leave the previous preview if the call fails.
      } finally {
        this.previewLoading.set(false);
      }
    }, 350);
  });

  /** Per-message UI state: which message has comments expanded / picker open. */
  protected readonly commentsOpen = signal<Set<string>>(new Set());
  protected readonly pickerOpenFor = signal<string | null>(null);
  protected readonly commentDrafts = signal<Record<string, string>>({});

  /** When non-null, the URL of an image currently shown fullscreen in the lightbox. */
  protected readonly lightboxUrl = signal<string | null>(null);

  /** Transient toast shown after copying a share link (or failing to). Auto-clears. */
  protected readonly toast = signal<string | null>(null);
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  /**
   * Message id currently highlighted because the user landed on it via a
   * deep link (`/wall?messageId=…`). The card binds a transient background
   * class against it; cleared by a timer after ~3 s.
   */
  protected readonly highlightedId = signal<string | null>(null);
  private highlightTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly canPublish = computed(() => this.composerText().trim().length > 0);

  protected readonly myMemberId = computed(() => this.auth.me()?.memberId ?? null);
  protected readonly isAdmin = computed(() => this.auth.me()?.role === 'Admin');

  /** Active members projected to the lightweight shape consumed by the autocomplete. */
  protected readonly mentionCandidates = computed<MentionMember[]>(() =>
    this.members()
      .filter((m) => m.isActive)
      .map((m) => ({ id: m.id, displayName: m.displayName, colorHex: m.colorHex })),
  );

  ngOnInit(): void {
    // Read the deep-link target (if any) BEFORE kicking off the initial load
    // so we can resolve "is the requested message in the first page?" against
    // a known target. Without a target this is just the regular wall flow.
    const targetId = this.route.snapshot.queryParamMap.get('messageId');
    void this.bootstrap(targetId);
    void this.wall.markRead().subscribe({ error: () => { /* silent */ } });
    refreshOnFocus(() => void this.load(), this.destroyRef);

    // Clean up any pending timers when the component unmounts so navigating
    // away mid-highlight doesn't leak a setTimeout into the next route.
    this.destroyRef.onDestroy(() => {
      if (this.toastTimer) clearTimeout(this.toastTimer);
      if (this.highlightTimer) clearTimeout(this.highlightTimer);
    });
  }

  /**
   * Coordinated initial load. When the URL carries a `?messageId=…` deep
   * link, fetch that specific message first (so we can fail fast on
   * 404/403 with a clear toast) and then merge it into the feed for
   * scroll + highlight. Without a deep link this just runs `load()`.
   */
  private async bootstrap(targetId: string | null): Promise<void> {
    if (!targetId) {
      await this.load();
      return;
    }

    // Fire both calls in parallel: the single GET is cheap and lets us
    // surface a clean error if the link is dead before the user sees an
    // empty highlight.
    const single$ = firstValueFrom(this.wall.get(targetId)).catch(() => null);
    const feed$ = this.load();
    const [single] = await Promise.all([single$, feed$]);

    if (!single) {
      this.showToast($localize`:@@wall.share.not-found:Este post ya no existe o no tienes acceso.`);
      this.clearMessageIdFromUrl();
      return;
    }

    // If the deep-link target is not in the first page (older message,
    // off-cursor), prepend it so the user actually sees what they were
    // sent. Otherwise the existing entry stays in place.
    const alreadyVisible =
      this.pinned().some((m) => m.id === single.id) ||
      this.messages().some((m) => m.id === single.id);
    if (!alreadyVisible) {
      this.messages.update((list) => [single, ...list]);
    }

    this.highlight(single.id);
    this.clearMessageIdFromUrl();
  }

  /**
   * Copy a public-format link to the message to the clipboard. The link
   * itself is NOT public: opening it requires a session cookie and the
   * GET endpoint still enforces family-scoping. Failure (e.g. denied
   * clipboard permission on insecure context) surfaces as a toast.
   */
  protected async share(message: WallMessage): Promise<void> {
    const url = `${window.location.origin}/wall?messageId=${message.id}`;
    try {
      await navigator.clipboard.writeText(url);
      this.showToast($localize`:@@wall.share.copied:Enlace copiado al portapapeles`);
    } catch {
      this.showToast($localize`:@@wall.share.error:No se pudo copiar el enlace`);
    }
  }

  /** Highlight a message card briefly, then clear the marker on a timer. */
  private highlight(messageId: string): void {
    this.highlightedId.set(messageId);
    if (this.highlightTimer) clearTimeout(this.highlightTimer);
    this.highlightTimer = setTimeout(() => this.highlightedId.set(null), 3000);

    // Scroll twice on purpose:
    //  - First soon after change detection flushes so the user gets
    //    immediate feedback that something is happening.
    //  - Again after a longer delay to correct for any layout shift that
    //    happens while images in messages ABOVE the target finish loading.
    //    Without the second pass, the post often ends up scrolled past
    //    (above the viewport) because the document grew under it.
    const scrollToCard = (): void => {
      document
        .getElementById(`wall-message-${messageId}`)
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };
    setTimeout(scrollToCard, 50);
    setTimeout(scrollToCard, 600);
  }

  /** Show a transient toast at the bottom of the screen for ~2 s. */
  private showToast(text: string): void {
    this.toast.set(text);
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(null), 2000);
  }

  /**
   * Drop `messageId` from the URL once we've consumed it so a refresh
   * doesn't re-trigger the highlight every time.
   */
  private clearMessageIdFromUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { messageId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  // ─── composer ──────────────────────────────────────────────────────────────

  protected toggleComposer(): void {
    const next = !this.composerOpen();
    if (!next) {
      this.resetComposer();
    }
    this.composerOpen.set(next);
  }

  protected onComposerInput(value: string): void {
    this.composerText.set(value);
  }

  protected async onImagePicked(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    await this.uploadComposerImage(file);
  }

  /** Forwarded from <fn-mention-textarea> when an image is pasted into the textarea. */
  protected async onComposerPasteImage(file: File): Promise<void> {
    if (!this.composerOpen()) {
      this.composerOpen.set(true);
    }
    await this.uploadComposerImage(file);
  }

  /** Drag-and-drop on the composer wrapper: highlight the drop zone. */
  protected onComposerDragOver(event: DragEvent): void {
    if (!event.dataTransfer?.types.includes('Files')) return;
    event.preventDefault();
    this.composerDragOver.set(true);
  }

  /** Reset highlight when the user drags out without dropping. */
  protected onComposerDragLeave(event: DragEvent): void {
    // Only react when the drag actually leaves the composer (not just children).
    if (event.currentTarget === event.target) {
      this.composerDragOver.set(false);
    }
  }

  /** Handle file drop on the composer — upload the first image-typed file. */
  protected async onComposerDrop(event: DragEvent): Promise<void> {
    event.preventDefault();
    this.composerDragOver.set(false);
    const files = event.dataTransfer?.files;
    if (!files) return;
    for (const f of Array.from(files)) {
      if (f.type.startsWith('image/')) {
        await this.uploadComposerImage(f);
        return;
      }
    }
  }

  /** Single source of truth for "got a File, attach it as the composer image". */
  private async uploadComposerImage(file: File): Promise<void> {
    this.composerUploading.set(true);
    try {
      const asset = await firstValueFrom(this.files.upload(file, 'wall'));
      this.composerImageId.set(asset.id);
      this.composerImagePreview.set(asset.url);
    } catch {
      this.error.set('upload');
    } finally {
      this.composerUploading.set(false);
    }
  }

  protected clearImage(): void {
    this.composerImageId.set(null);
    this.composerImagePreview.set(null);
  }

  /** Open the composer pre-filled with an existing message and switch to edit mode. */
  protected editMessage(message: WallMessage): void {
    this.editingMessageId.set(message.id);
    this.composerText.set(message.text);
    this.composerImageId.set(message.image?.id ?? null);
    this.composerImagePreview.set(message.image?.url ?? null);
    this.composerOpen.set(true);
  }

  /** True when the caller has permission to edit a given message (author or admin). */
  protected canEdit(message: WallMessage): boolean {
    return this.isAdmin() || message.authorMemberId === this.myMemberId();
  }

  /** Open the fullscreen lightbox for the given image URL. */
  protected openLightbox(url: string): void {
    this.lightboxUrl.set(url);
  }

  /** Close the lightbox. Bound to overlay click, X button, and Escape. */
  protected closeLightbox(): void {
    this.lightboxUrl.set(null);
  }

  protected async publish(): Promise<void> {
    if (!this.canPublish()) return;
    const editingId = this.editingMessageId();
    const payload = {
      text: this.composerText().trim(),
      imageFileId: this.composerImageId(),
    };
    try {
      if (editingId) {
        const updated = await firstValueFrom(this.wall.update(editingId, payload));
        this.applyMessageUpdated(updated);
      } else {
        const created = await firstValueFrom(this.wall.create(payload));
        this.applyMessageCreated(created);
      }
      this.resetComposer();
      this.composerOpen.set(false);
    } catch {
      this.error.set('publish');
    }
  }

  private resetComposer(): void {
    this.composerText.set('');
    this.composerImageId.set(null);
    this.composerImagePreview.set(null);
    this.editingMessageId.set(null);
    this.composerDragOver.set(false);
  }

  /** Apply a server-returned updated message to the local pinned/messages lists. */
  private applyMessageUpdated(m: WallMessage): void {
    this.pinned.update((list) => list.map((x) => (x.id === m.id ? m : x)));
    this.messages.update((list) => list.map((x) => (x.id === m.id ? m : x)));
  }

  // ─── message actions ───────────────────────────────────────────────────────

  protected async togglePin(message: WallMessage): Promise<void> {
    try {
      const updated = message.isPinned
        ? await firstValueFrom(this.wall.unpin(message.id))
        : await firstValueFrom(this.wall.pin(message.id));
      this.applyPinChanged({
        messageId: updated.id,
        isPinned: updated.isPinned,
        pinnedAt: updated.pinnedAt,
      });
    } catch {
      this.error.set('pin');
    }
  }

  protected async deleteMessage(message: WallMessage): Promise<void> {
    if (!confirm($localize`:@@wall.delete-message-confirm:¿Borrar este mensaje?`)) return;
    try {
      await firstValueFrom(this.wall.delete(message.id));
      this.applyMessageDeleted(message.id);
    } catch {
      this.error.set('delete');
    }
  }

  protected canDelete(message: WallMessage): boolean {
    return this.isAdmin() || message.authorMemberId === this.myMemberId();
  }

  // ─── reactions ─────────────────────────────────────────────────────────────

  protected togglePicker(messageId: string): void {
    this.pickerOpenFor.set(this.pickerOpenFor() === messageId ? null : messageId);
  }

  protected async react(messageId: string, emoji: string): Promise<void> {
    this.pickerOpenFor.set(null);
    try {
      const result = await firstValueFrom(this.wall.toggleReaction(messageId, { emoji }));
      this.applyReactionToggled(result);
    } catch {
      this.error.set('react');
    }
  }

  protected haveReacted(summary: { memberIds: string[] }): boolean {
    const me = this.myMemberId();
    return me ? summary.memberIds.includes(me) : false;
  }

  // ─── comments ──────────────────────────────────────────────────────────────

  protected toggleComments(messageId: string): void {
    const open = new Set(this.commentsOpen());
    if (open.has(messageId)) {
      open.delete(messageId);
    } else {
      open.add(messageId);
    }
    this.commentsOpen.set(open);
  }

  protected commentsOpenFor(messageId: string): boolean {
    return this.commentsOpen().has(messageId);
  }

  protected commentDraft(messageId: string): string {
    return this.commentDrafts()[messageId] ?? '';
  }

  protected onCommentDraftInput(messageId: string, value: string): void {
    this.commentDrafts.update((current) => ({ ...current, [messageId]: value }));
  }

  protected async submitComment(messageId: string): Promise<void> {
    const text = this.commentDraft(messageId).trim();
    if (!text) return;
    try {
      const created = await firstValueFrom(this.wall.addComment(messageId, { text }));
      this.applyCommentAdded(created);
      this.commentDrafts.update((current) => ({ ...current, [messageId]: '' }));
    } catch {
      this.error.set('comment');
    }
  }

  protected async deleteComment(comment: WallComment): Promise<void> {
    if (!confirm($localize`:@@wall.delete-comment-confirm:¿Borrar este comentario?`)) return;
    try {
      await firstValueFrom(this.wall.deleteComment(comment.id));
      this.applyCommentDeleted(comment.messageId, comment.id);
    } catch {
      this.error.set('comment-delete');
    }
  }

  protected canDeleteComment(comment: WallComment): boolean {
    return this.isAdmin() || comment.authorMemberId === this.myMemberId();
  }

  // ─── pagination ────────────────────────────────────────────────────────────

  protected async loadMore(): Promise<void> {
    if (!this.hasMore() || this.messages().length === 0) return;
    const last = this.messages()[this.messages().length - 1];
    try {
      const page = await firstValueFrom(this.wall.list({ before: last.createdAt }));
      this.messages.update((list) => [...list, ...page.messages]);
      this.hasMore.set(page.hasMore);
    } catch {
      this.error.set('load-more');
    }
  }

  // ─── display helpers ───────────────────────────────────────────────────────

  protected memberName(memberId: string): string {
    return this.members().find((m) => m.id === memberId)?.displayName ?? '—';
  }

  protected memberColor(memberId: string): string {
    return this.members().find((m) => m.id === memberId)?.colorHex ?? '#999999';
  }

  /** Avatar URL for a given member, or null when they have no photo set. */
  protected memberPhotoUrl(memberId: string): string | null {
    const m = this.members().find((x) => x.id === memberId);
    return m?.photoPath ? `/api/family-members/${m.id}/photo` : null;
  }

  // ─── initial load ──────────────────────────────────────────────────────────

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const [members, page] = await Promise.all([
        firstValueFrom(this.membersApi.list()),
        firstValueFrom(this.wall.list()),
      ]);
      this.members.set(members);
      this.pinned.set(page.pinned);
      this.messages.set(page.messages);
      this.hasMore.set(page.hasMore);
    } catch {
      this.error.set('load');
    } finally {
      this.loading.set(false);
    }
  }

  // ─── local mutations ───────────────────────────────────────────────────────
  // Each protected action calls these to merge the API response back into the
  // local state so the UI reflects the change without a full reload.

  private applyMessageCreated(m: WallMessage): void {
    if (m.isPinned) {
      this.pinned.update((list) => this.upsert(list, m));
    } else {
      this.messages.update((list) => this.upsert(list, m));
    }
  }

  private applyMessageDeleted(messageId: string): void {
    this.pinned.update((list) => list.filter((m) => m.id !== messageId));
    this.messages.update((list) => list.filter((m) => m.id !== messageId));
  }

  private applyPinChanged(e: { messageId: string; isPinned: boolean; pinnedAt: string | null }): void {
    const inPinned = this.pinned().find((m) => m.id === e.messageId);
    const inFeed = this.messages().find((m) => m.id === e.messageId);
    const target = inPinned ?? inFeed;
    if (!target) return;
    const moved: WallMessage = { ...target, isPinned: e.isPinned, pinnedAt: e.pinnedAt };
    if (e.isPinned) {
      this.messages.update((list) => list.filter((m) => m.id !== e.messageId));
      this.pinned.update((list) => this.upsert(list, moved));
    } else {
      this.pinned.update((list) => list.filter((m) => m.id !== e.messageId));
      this.messages.update((list) => this.upsert(list, moved));
    }
  }

  private applyCommentAdded(c: WallComment): void {
    const patch = (m: WallMessage): WallMessage =>
      m.id === c.messageId ? { ...m, comments: [...m.comments, c] } : m;
    this.pinned.update((list) => list.map(patch));
    this.messages.update((list) => list.map(patch));
  }

  private applyCommentDeleted(messageId: string, commentId: string): void {
    const patch = (m: WallMessage): WallMessage =>
      m.id === messageId
        ? { ...m, comments: m.comments.filter((c) => c.id !== commentId) }
        : m;
    this.pinned.update((list) => list.map(patch));
    this.messages.update((list) => list.map(patch));
  }

  private applyReactionToggled(r: ToggleReactionResult): void {
    const patch = (m: WallMessage): WallMessage => {
      if (m.id !== r.messageId) return m;
      const without = m.reactions.filter((s) => s.emoji !== r.emoji);
      const next = r.summary.count > 0 ? [...without, r.summary] : without;
      next.sort((a, b) => b.count - a.count);
      return { ...m, reactions: next };
    };
    this.pinned.update((list) => list.map(patch));
    this.messages.update((list) => list.map(patch));
  }

  private upsert(list: WallMessage[], m: WallMessage): WallMessage[] {
    if (list.some((x) => x.id === m.id)) {
      return list.map((x) => (x.id === m.id ? m : x));
    }
    // Newest first — prepend.
    return [m, ...list];
  }
}
