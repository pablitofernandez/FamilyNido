import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Me, TemperatureUnitPreference, TimeFormatPreference } from '../models/me';

/**
 * Holds the authenticated session as a signal-driven state so components and
 * route guards can react to it without subscriptions. The session is hydrated
 * once at application startup via {@link loadInitialSession}; OIDC login goes
 * through a full-page navigation, while local credentials login is a regular
 * fetch that returns the same cookie shape as the OIDC callback.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _me = signal<Me | null>(null);
  private readonly _status = signal<'idle' | 'loading' | 'authenticated' | 'anonymous' | 'not-linked'>('idle');

  /** Currently authenticated user profile (null when not logged in). */
  readonly me = this._me.asReadonly();

  /** Rough state machine used by the shell and route guards. */
  readonly status = this._status.asReadonly();

  /** Convenience: true when there is a valid authenticated session. */
  readonly isAuthenticated = computed(() => this._status() === 'authenticated');

  /** True when the user is logged in but the admin has not linked them to a member yet. */
  readonly isNotLinked = computed(() => this._status() === 'not-linked');

  /**
   * Fetch the current user's profile. Called at bootstrap; safe to call again.
   * Maps 401 to `anonymous` and 403/`auth.not_linked` to `not-linked`.
   */
  async loadInitialSession(): Promise<void> {
    this._status.set('loading');
    try {
      const me = await firstValueFrom(this.http.get<Me>(environment.authMeUrl));
      this._me.set(me);
      this._status.set('authenticated');
    } catch (error: unknown) {
      this._me.set(null);
      const status = (error as { status?: number }).status;
      this._status.set(status === 403 ? 'not-linked' : 'anonymous');
    }
  }

  /**
   * Persist the caller's preferred UI language (BCP-47 tag). On success the
   * browser is reloaded to land on the new locale subpath — the bundle is
   * fixed at compile time, so we can't simply swap strings in place.
   */
  async setPreferredLanguage(language: string): Promise<void> {
    await firstValueFrom(this.http.put<{ language: string }>(
      '/api/auth/me/preferred-language',
      { language },
    ));
    // Patch the in-memory signal first so consumers reading it during the
    // small window before the reload see the new value.
    const current = this._me();
    if (current) {
      this._me.set({ ...current, preferredLanguage: language });
    }
    // Build the new URL by swapping the locale prefix. The user lands on the
    // same screen, just with the other bundle.
    const newPrefix = language.toLowerCase().startsWith('en') ? '/en' : '/es';
    const path = window.location.pathname.replace(/^\/(es|en)(?=\/|$)/, newPrefix);
    window.location.assign(path + window.location.search + window.location.hash);
  }

  /**
   * Persist the caller's time-format override (or clear it). Patches the
   * in-memory signal so any signal-driven view re-evaluates immediately;
   * components that cached an `Intl.DateTimeFormat` instance at construction
   * (e.g. dashboard / tablet) only pick the change up on the next mount.
   */
  async setTimeFormat(format: TimeFormatPreference | null): Promise<void> {
    await firstValueFrom(this.http.put<{ timeFormat: TimeFormatPreference | null }>(
      '/api/auth/me/time-format',
      { timeFormat: format },
    ));
    const current = this._me();
    if (current) {
      this._me.set({ ...current, timeFormat: format });
    }
  }

  /** Same shape as {@link setTimeFormat} but for the temperature unit. */
  async setTemperatureUnit(unit: TemperatureUnitPreference | null): Promise<void> {
    await firstValueFrom(this.http.put<{ temperatureUnit: TemperatureUnitPreference | null }>(
      '/api/auth/me/temperature-unit',
      { temperatureUnit: unit },
    ));
    const current = this._me();
    if (current) {
      this._me.set({ ...current, temperatureUnit: unit });
    }
  }

  /**
   * Fetch which login providers the server has wired up. Used by the login
   * screen to hide the OIDC button when the deployment doesn't have a
   * provider configured. Falls back to "OIDC enabled" if the call fails so
   * that a prod deployment with a transient network blip doesn't accidentally
   * hide the only login path its users know.
   */
  async getProviders(): Promise<{ oidcEnabled: boolean }> {
    try {
      return await firstValueFrom(
        this.http.get<{ oidcEnabled: boolean }>('/api/auth/providers'),
      );
    } catch {
      return { oidcEnabled: true };
    }
  }

  /** Redirect the browser to the OIDC challenge endpoint (full-page navigation). */
  loginOidc(returnUrl = '/'): void {
    const target = new URL(environment.authLoginUrl, window.location.origin);
    target.searchParams.set('returnUrl', returnUrl);
    window.location.assign(target.toString());
  }

  /**
   * Sign in with local credentials. Returns a structured outcome so the form
   * can render a precise error without leaking timing or distinguishing
   * "no such user" from "wrong password" — the API answers 401 for both.
   */
  async loginLocal(email: string, password: string): Promise<LocalLoginOutcome> {
    try {
      await firstValueFrom(this.http.post('/api/auth/local/login', { email, password }));
      await this.loadInitialSession();
      return { ok: true };
    } catch (error: unknown) {
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      if (status === 403 || status === 401) {
        return { ok: false, reason: 'invalid_credentials' };
      }
      if (status === 429) {
        return { ok: false, reason: 'rate_limited' };
      }
      return { ok: false, reason: 'unknown' };
    }
  }

  /** Backwards-compatible alias for callers that still use `login`. */
  login(returnUrl = '/'): void {
    this.loginOidc(returnUrl);
  }

  /**
   * Drop the cookie on the server and unconditionally bounce the user to
   * /login. Wrapped in try/finally so that even when the POST fails (network
   * blip, server already 401'd this session, etc.) the local state still
   * resets and the user lands on a clean login screen — never stuck on a
   * page that thinks they're authenticated.
   */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post(environment.authLogoutUrl, {}));
    } catch {
      // Swallow — the redirect below is still the right thing to do.
    } finally {
      this._me.set(null);
      this._status.set('anonymous');
      // Full-page navigation so any in-memory caches (signal subscriptions,
      // lazy-loaded chunks holding old user data) get a fresh start instead
      // of trying to reconcile with no session.
      window.location.assign('/login');
    }
  }
}

/** Discriminated outcome returned by {@link AuthService.loginLocal}. */
export type LocalLoginOutcome =
  | { ok: true }
  | { ok: false; reason: 'invalid_credentials' | 'rate_limited' | 'unknown' };
