/** Shape returned by `POST /api/files` and embedded in wall messages. */
export interface FileAsset {
  id: string;
  contentType: string;
  sizeBytes: number;
  width: number | null;
  height: number | null;
  ownerMemberId: string;
  /** Relative URL to stream the bytes back (handled by the interceptor + cookie). */
  url: string;
}
