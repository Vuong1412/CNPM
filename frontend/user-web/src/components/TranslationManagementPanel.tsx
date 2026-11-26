import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import type { AudioGuide, Language, LocationSummary } from "@qol/shared";
import { apiClient, getMediaUrl } from "../services/api";

interface LocationTranslation {
  id: string;
  locationId: string;
  languageId: string;
  languageCode: string;
  languageName: string;
  text: string;
  updatedAt: string;
}

interface TranslationManagementPanelProps {
  locations: LocationSummary[];
  languages: Language[];
}

export function TranslationManagementPanel({
  locations,
  languages
}: TranslationManagementPanelProps) {
  const { t } = useTranslation();
  const [selectedLocationId, setSelectedLocationId] = useState("");
  const [selectedLanguageId, setSelectedLanguageId] = useState("");
  const [text, setText] = useState("");
  const [translations, setTranslations] = useState<LocationTranslation[]>([]);
  const [generatedAudio, setGeneratedAudio] = useState<AudioGuide | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const selectedLocation = useMemo(
    () => locations.find((location) => location.id === selectedLocationId),
    [locations, selectedLocationId]
  );
  const selectedTranslation = translations.find(
    (translation) => translation.languageId === selectedLanguageId
  );

  const loadTranslations = useCallback(async (locationId: string, signal?: AbortSignal) => {
    if (!locationId) {
      setTranslations([]);
      return;
    }

    setIsLoading(true);
    try {
      const result = await apiClient.get<LocationTranslation[]>(
        `admin/locations/${encodeURIComponent(locationId)}/translations`,
        signal
      );
      setTranslations(result);
      setError(null);
    } catch (cause) {
      if (!signal?.aborted) {
        setError(cause instanceof Error ? cause.message : t("admin.translationLoadError"));
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [t]);

  useEffect(() => {
    if (!selectedLocationId && locations.length > 0) {
      setSelectedLocationId(locations[0].id);
    }
  }, [locations, selectedLocationId]);

  useEffect(() => {
    if (!selectedLanguageId && languages.length > 0) {
      setSelectedLanguageId(languages[0].id);
    }
  }, [languages, selectedLanguageId]);

  useEffect(() => {
    const controller = new AbortController();
    void loadTranslations(selectedLocationId, controller.signal);
    setGeneratedAudio(null);
    return () => controller.abort();
  }, [loadTranslations, selectedLocationId]);

  useEffect(() => {
    setText(selectedTranslation?.text ?? "");
  }, [selectedLanguageId, selectedTranslation?.text]);

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedLocation || !selectedLanguageId) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setNotice(null);
    try {
      const saved = await apiClient.put<LocationTranslation>(
        `admin/locations/${encodeURIComponent(selectedLocation.id)}/translations/${encodeURIComponent(selectedLanguageId)}`,
        { text }
      );
      setTranslations((current) => [
        ...current.filter((item) => item.id !== saved.id),
        saved
      ]);
      setNotice(t("admin.translationSaved"));
      setGeneratedAudio(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.translationSaveError"));
    } finally {
      setIsSaving(false);
    }
  }

  async function generateAudio(translation: LocationTranslation) {
    if (!selectedLocation) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setNotice(null);
    try {
      const audio = await apiClient.post<AudioGuide>(
        `admin/locations/${encodeURIComponent(selectedLocation.id)}/translations/${encodeURIComponent(translation.id)}/audio/tts`,
        {}
      );
      setGeneratedAudio(audio);
      setNotice(t("admin.ttsCreated"));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("admin.ttsError"));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="admin-panel translation-panel">
      <div className="admin-panel-heading">
        <div>
          <h2>{t("admin.translationsTitle")}</h2>
          <p>{t("admin.translationsDescription")}</p>
        </div>
      </div>
      <label>
        {t("admin.translationLocation")}
        <select
          onChange={(event) => setSelectedLocationId(event.target.value)}
          value={selectedLocationId}
        >
          {locations.map((location) => (
            <option key={location.id} value={location.id}>{location.name}</option>
          ))}
        </select>
      </label>
      <label>
        {t("admin.translationLanguage")}
        <select
          onChange={(event) => setSelectedLanguageId(event.target.value)}
          value={selectedLanguageId}
        >
          {languages.map((language) => (
            <option key={language.id} value={language.id}>
              {language.nativeName} · {language.name}
            </option>
          ))}
        </select>
      </label>
      {error && <p className="form-error" role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {isLoading && <p role="status">{t("common.loading")}</p>}
      {selectedLocation && selectedLanguageId && (
        <form className="translation-form" onSubmit={(event) => void handleSave(event)}>
          <label>
            {t("admin.translationText")}
            <textarea
              maxLength={50000}
              onChange={(event) => setText(event.target.value)}
              required
              rows={8}
              value={text}
            />
          </label>
          <button className="button-primary" disabled={isSaving} type="submit">
            {isSaving ? t("auth.working") : t("admin.translationSave")}
          </button>
        </form>
      )}
      <div className="translation-list">
        {translations.map((translation) => (
          <article className="translation-row" key={translation.id}>
            <div>
              <strong>{translation.languageName}</strong>
              <p>{translation.text}</p>
            </div>
            <button
              className="button-secondary"
              disabled={isSaving}
              onClick={() => void generateAudio(translation)}
              type="button"
            >
              {t("admin.generateTts")}
            </button>
          </article>
        ))}
        {!isLoading && translations.length === 0 && (
          <p className="muted">{t("admin.noTranslations")}</p>
        )}
      </div>
      {generatedAudio && (
        <div className="generated-audio">
          <p>{generatedAudio.title} · {t("admin.audioSourceTts")}</p>
          <audio controls src={getMediaUrl(generatedAudio.audioUrl)} />
        </div>
      )}
    </section>
  );
}
