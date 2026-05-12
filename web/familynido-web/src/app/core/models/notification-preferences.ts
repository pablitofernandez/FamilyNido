/** Wire shape returned by GET/PUT `/api/notifications/preferences`. */
export interface NotificationPreferences {
  emailEnabled: boolean;
  digestEnabled: boolean;
  taskAssignedEnabled: boolean;
  wallMentionEnabled: boolean;
}
