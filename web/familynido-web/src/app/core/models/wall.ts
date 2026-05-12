import { FileAsset } from './file-asset';

/** Mirror of the backend `WallMessageDto`. */
export interface WallMessage {
  id: string;
  authorMemberId: string;
  text: string;
  textHtml: string;
  image: FileAsset | null;
  isPinned: boolean;
  pinnedAt: string | null;
  createdAt: string;
  comments: WallComment[];
  reactions: WallReactionSummary[];
}

/** Mirror of `WallCommentDto`. */
export interface WallComment {
  id: string;
  messageId: string;
  authorMemberId: string;
  text: string;
  textHtml: string;
  createdAt: string;
}

/** Aggregated reaction bucket for a specific emoji. */
export interface WallReactionSummary {
  emoji: string;
  count: number;
  memberIds: string[];
}

/** Mirror of `WallFeedPageDto`. */
export interface WallFeedPage {
  pinned: WallMessage[];
  messages: WallMessage[];
  hasMore: boolean;
}

/** Payload for `POST /api/wall/messages`. */
export interface CreateWallMessageRequest {
  text: string;
  imageFileId?: string | null;
}

/** Payload for `PATCH /api/wall/messages/{id}`. */
export type UpdateWallMessageRequest = CreateWallMessageRequest;

/** Payload for `POST /api/wall/messages/{id}/comments`. */
export interface AddWallCommentRequest {
  text: string;
}

/** Payload for `POST /api/wall/messages/{id}/reactions`. */
export interface ToggleReactionRequest {
  emoji: string;
}

/** Response of `POST /api/wall/messages/{id}/reactions`. */
export interface ToggleReactionResult {
  messageId: string;
  emoji: string;
  isReacted: boolean;
  summary: WallReactionSummary;
}
