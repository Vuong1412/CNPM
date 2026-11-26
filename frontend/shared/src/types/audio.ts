export interface AudioGuide {
  id: string;
  locationId: string;
  languageId: string;
  languageCode: string;
  languageName: string;
  languageNativeName: string;
  title: string;
  transcript: string;
  audioUrl: string;
  durationSeconds: number;
  isActive: boolean;
  source: "UPLOAD" | "TTS";
  translationId: string | null;
}
