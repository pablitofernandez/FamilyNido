import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { FileAsset } from '../models/file-asset';

/** HTTP client for the shared `/api/files` module. */
@Injectable({ providedIn: 'root' })
export class FilesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/files';

  /**
   * Upload a binary asset. Passes the file as `multipart/form-data` with the
   * optional `module` hint so the backend can place it under the right folder.
   */
  upload(file: File, module = 'wall'): Observable<FileAsset> {
    const body = new FormData();
    body.set('file', file);
    body.set('module', module);
    return this.http.post<FileAsset>(this.baseUrl, body);
  }

  /** Convenience: build the URL for an existing asset id (served with auth). */
  urlFor(fileId: string): string {
    return `${this.baseUrl}/${fileId}`;
  }
}
