import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AddWallCommentRequest,
  CreateWallMessageRequest,
  ToggleReactionRequest,
  ToggleReactionResult,
  UpdateWallMessageRequest,
  WallComment,
  WallFeedPage,
  WallMessage,
} from '../models/wall';

/** HTTP client for `/api/wall`. */
@Injectable({ providedIn: 'root' })
export class WallService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/wall';

  /** GET /api/wall/messages — cursor-based. */
  list(options?: { before?: string; limit?: number }): Observable<WallFeedPage> {
    let params = new HttpParams();
    if (options?.before) {
      params = params.set('before', options.before);
    }
    if (options?.limit) {
      params = params.set('limit', String(options.limit));
    }
    return this.http.get<WallFeedPage>(`${this.baseUrl}/messages`, { params });
  }

  /** GET /api/wall/messages/{id}. */
  get(id: string): Observable<WallMessage> {
    return this.http.get<WallMessage>(`${this.baseUrl}/messages/${id}`);
  }

  /** POST /api/wall/messages. */
  create(request: CreateWallMessageRequest): Observable<WallMessage> {
    return this.http.post<WallMessage>(`${this.baseUrl}/messages`, request);
  }

  /** PATCH /api/wall/messages/{id}. */
  update(id: string, request: UpdateWallMessageRequest): Observable<WallMessage> {
    return this.http.patch<WallMessage>(`${this.baseUrl}/messages/${id}`, request);
  }

  /** DELETE /api/wall/messages/{id}. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/messages/${id}`);
  }

  /** POST /api/wall/messages/{id}/pin. */
  pin(id: string): Observable<WallMessage> {
    return this.http.post<WallMessage>(`${this.baseUrl}/messages/${id}/pin`, {});
  }

  /** POST /api/wall/messages/{id}/unpin. */
  unpin(id: string): Observable<WallMessage> {
    return this.http.post<WallMessage>(`${this.baseUrl}/messages/${id}/unpin`, {});
  }

  /** POST /api/wall/messages/{id}/comments. */
  addComment(messageId: string, request: AddWallCommentRequest): Observable<WallComment> {
    return this.http.post<WallComment>(`${this.baseUrl}/messages/${messageId}/comments`, request);
  }

  /** DELETE /api/wall/comments/{id}. */
  deleteComment(commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/comments/${commentId}`);
  }

  /** POST /api/wall/messages/{id}/reactions (toggle). */
  toggleReaction(messageId: string, request: ToggleReactionRequest): Observable<ToggleReactionResult> {
    return this.http.post<ToggleReactionResult>(
      `${this.baseUrl}/messages/${messageId}/reactions`,
      request,
    );
  }

  /** GET /api/wall/unread-count. */
  unreadCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.baseUrl}/unread-count`);
  }

  /** PATCH /api/wall/last-read. */
  markRead(): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/last-read`, {});
  }

  /** POST /api/wall/preview — render raw markdown the same way the server will on save. */
  preview(text: string): Observable<{ html: string }> {
    return this.http.post<{ html: string }>(`${this.baseUrl}/preview`, { text });
  }
}
