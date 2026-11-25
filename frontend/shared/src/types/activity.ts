export interface FavoriteSummary {
  locationId: string;
  name: string;
  address: string;
  createdAt: string;
}

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
  listenedAt: string;
}
