export interface FavoriteSummary {
  locationId: string;
  name: string;
  address: string;
  createdAt: string;
}

export type ListeningTriggerMethod = "TAP" | "AUTO_GPS";

export interface ListeningHistoryItem {
  id: string;
  locationId: string;
  locationName: string;
  languageCode: string;
  languageName: string;
  audioTitle: string;
  audioUrl: string;
  audioDurationSeconds: number;
  durationSeconds: number;
  isCompleted: boolean;
  triggerMethod: ListeningTriggerMethod;
  sessionId?: string | null;
  listenedAt: string;
}
