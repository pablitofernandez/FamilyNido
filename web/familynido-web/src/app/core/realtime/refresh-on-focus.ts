import { DestroyRef } from '@angular/core';

/**
 * Registers a callback that fires when the user returns to the app: the tab
 * becomes visible again or the window regains focus. Used in place of a
 * persistent SignalR connection — most "I came back and there's new content"
 * flows are well served by a single re-fetch on visibility change.
 *
 * `destroyRef` must be supplied by the caller (typically captured as
 * `private readonly destroyRef = inject(DestroyRef)` in the component class)
 * so this helper does not require an injection context at call time — that
 * lets it be invoked safely from `ngOnInit`. The callback is throttled to
 * fire at most once every 2 s to avoid spamming the API when iOS Safari
 * fires both `visibilitychange` and `focus` in quick succession on app
 * foreground.
 */
export function refreshOnFocus(callback: () => void, destroyRef: DestroyRef): void {
  let lastFiredAt = 0;
  const minIntervalMs = 2000;

  const handle = () => {
    if (typeof document !== 'undefined' && document.visibilityState === 'hidden') return;
    const now = Date.now();
    if (now - lastFiredAt < minIntervalMs) return;
    lastFiredAt = now;
    callback();
  };

  const onVisibility = () => {
    if (document.visibilityState === 'visible') handle();
  };
  const onFocus = () => handle();

  document.addEventListener('visibilitychange', onVisibility);
  window.addEventListener('focus', onFocus);

  destroyRef.onDestroy(() => {
    document.removeEventListener('visibilitychange', onVisibility);
    window.removeEventListener('focus', onFocus);
  });
}
