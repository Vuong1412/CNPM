import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import type { AudioGuide, Language, LocationSummary } from "@qol/shared";
import { apiClient, getMediaUrl } from "../services/api";

interface ManagedLocation extends LocationSummary {
  createdAt: string;
  isActive: boolean;
}

interface AudioManagementPanelProps {
  locations: ManagedLocation[];
  languages: Language[];
  isLoadingLanguages: boolean;
  languageError: string | null;
}

export function AudioManagementPanel({
  locations,
  languages,
  isLoadingLanguages,
  languageError
}: AudioManagementPanelProps) {
  const { t } = useTranslation();
  const activeLocations = useMemo(
    () => locations.filter((location) => location.isActive),
    [locations]
  );
  const [locationId, setLocationId] = useState("");
  const [languageId, setLanguageId] = useState("");
  const [title, setTitle] = useState("");
  const [transcript, setTranscript] = useState("");
  const [durationSeconds, setDurationSeconds] = useState("60");
  const [file, setFile] = useState<File | null>(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [replacementInputKey, setReplacementInputKey] = useState(0);
  const [editingAudioId, setEditingAudioId] = useState<string | null>(null);
  const [editTitle, setEditTitle] = useState("");
  const [editTranscript, setEditTranscript] = useState("");
  const [replacementFiles, setReplacementFiles] = useState<Record<string, File>>({});
  const [replacementDurations, setReplacementDurations] = useState<Record<string, string>>({});
  const [audioGuides, setAudioGuides] = useState<AudioGuide[]>([]);
  const [isLoadingAudio, setIsLoadingAudio] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [cleanupResult, setCleanupResult] = useState<{
    dryRun: boolean;
    count: number;
    fileNames: string[];
  } | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!activeLocations.some((location) => location.id === locationId)) {
      setLocationId(activeLocations[0]?.id ?? "");
    }
  }, [activeLocations, locationId]);

  useEffect(() => {
    if (!languages.some((language) => language.id === languageId)) {
      setLanguageId(languages[0]?.id ?? "");
    }
  }, [languageId, languages]);

  const loadAudio = useCallback(async (signal?: AbortSignal) => {
    if (!locationId) {
      setAudioGuides([]);
      setIsLoadingAudio(false);
      return;
    }

    setIsLoadingAudio(true);
    try {
      const items = await apiClient.get<AudioGuide[]>(
        `admin/locations/${encodeURIComponent(locationId)}/audio`,
        signal
      );
      setAudioGuides(items);
      setError(null);
    } catch (cause) {
      if (!signal?.aborted) {
        setError(cause instanceof Error ? cause.message : t("audio.loadError"));
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoadingAudio(false);
      }
    }
  }, [locationId, t]);

  useEffect(() => {
    const controller = new AbortController();
    void loadAudio(controller.signal);
    return () => controller.abort();
  }, [loadAudio]);

  async function handleUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!locationId || !languageId || !file) {
      setError(t("audio.requiredFields"));
      return;
    }

    setIsSaving(true);
    setError(null);
    const formData = new FormData();
    formData.append("Title", title);
    formData.append("Transcript", transcript);
    formData.append("LanguageId", languageId);
    formData.append("DurationSeconds", durationSeconds);
    formData.append("File", file);

    try {
      await apiClient.postForm<AudioGuide>(
        `admin/locations/${encodeURIComponent(locationId)}/audio`,
        formData
      );
      setTitle("");
      setTranscript("");
      setDurationSeconds("60");
      setFile(null);
      setFileInputKey((key) => key + 1);
      await loadAudio();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("audio.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  function startEditing(guide: AudioGuide) {
    setEditingAudioId(guide.id);
    setEditTitle(guide.title);
    setEditTranscript(guide.transcript);
  }

  async function handleUpdate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!locationId || !editingAudioId) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      await apiClient.put<AudioGuide>(
        `admin/locations/${encodeURIComponent(locationId)}/audio/${encodeURIComponent(editingAudioId)}`,
        { title: editTitle, transcript: editTranscript }
      );
      setEditingAudioId(null);
      await loadAudio();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("audio.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  async function toggleVisibility(guide: AudioGuide) {
    if (!locationId) {
      return;
    }
    if (guide.isActive && !window.confirm(t("audio.confirmHide", { title: guide.title }))) {
      return;
    }

    setError(null);
    try {
      await apiClient.patch<void>(
        `admin/locations/${encodeURIComponent(locationId)}/audio/${encodeURIComponent(guide.id)}/visibility`,
        { isActive: !guide.isActive }
      );
      await loadAudio();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("audio.saveError"));
    }
  }

  async function replaceAudioFile(guide: AudioGuide) {
    const replacementFile = replacementFiles[guide.id];
    if (!locationId || !replacementFile) {
      setError(t("audio.fileRequired"));
      return;
    }

    if (!window.confirm(t("audio.confirmReplace", { title: guide.title }))) {
      return;
    }

    const formData = new FormData();
    formData.append("DurationSeconds", replacementDurations[guide.id] ?? String(guide.durationSeconds));
    formData.append("File", replacementFile);
    setIsSaving(true);
    setError(null);
    try {
      await apiClient.putForm<AudioGuide>(
        `admin/locations/${encodeURIComponent(locationId)}/audio/${encodeURIComponent(guide.id)}/file`,
        formData
      );
      setReplacementFiles({});
      setReplacementDurations({});
      setReplacementInputKey((key) => key + 1);
      await loadAudio();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("audio.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  async function deleteAudio(guide: AudioGuide) {
    if (!locationId || !window.confirm(t("audio.confirmDelete", { title: guide.title }))) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      await apiClient.delete<void>(
        `admin/locations/${encodeURIComponent(locationId)}/audio/${encodeURIComponent(guide.id)}`
      );
      await loadAudio();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("audio.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  async function cleanupOrphanAudio(dryRun: boolean) {
    if (!dryRun && !window.confirm(t("audio.confirmCleanup"))) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const result = await apiClient.post<{
        dryRun: boolean;
        count: number;
        fileNames: string[];
      }>(`admin/audio/cleanup?dryRun=${dryRun}`, {});
      setCleanupResult(result);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("audio.saveError"));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="admin-panel">
      <div className="admin-panel-heading">
        <div>
          <h2>{t("audio.adminTitle")}</h2>
          <p>{t("audio.adminDescription")}</p>
        </div>
      </div>

      {activeLocations.length === 0 ? (
        <p className="admin-empty">{t("audio.noActiveLocations")}</p>
      ) : (
        <>
          <label className="audio-location-select">
            {t("audio.location")}
            <select
              onChange={(event) => setLocationId(event.target.value)}
              value={locationId}
            >
              {activeLocations.map((location) => (
                <option key={location.id} value={location.id}>
                  {location.name}
                </option>
              ))}
            </select>
          </label>

          <form className="audio-upload-form" onSubmit={(event) => void handleUpload(event)}>
            <label>
              {t("audio.language")}
              <select
                disabled={isLoadingLanguages || languages.length === 0}
                onChange={(event) => setLanguageId(event.target.value)}
                required
                value={languageId}
              >
                {languages.map((language) => (
                  <option key={language.id} value={language.id}>
                    {language.nativeName} · {language.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("audio.trackTitle")}
              <input
                maxLength={200}
                onChange={(event) => setTitle(event.target.value)}
                required
                value={title}
              />
            </label>
            <label className="audio-transcript-field">
              {t("audio.transcript")}
              <textarea
                maxLength={50000}
                onChange={(event) => setTranscript(event.target.value)}
                required
                rows={5}
                value={transcript}
              />
            </label>
            <label>
              {t("audio.duration")}
              <input
                max={3600}
                min={1}
                onChange={(event) => setDurationSeconds(event.target.value)}
                required
                type="number"
                value={durationSeconds}
              />
            </label>
            <label>
              {t("audio.file")}
              <input
                key={fileInputKey}
                accept=".mp3,.wav,.ogg,.m4a,audio/mpeg,audio/wav,audio/ogg,audio/mp4"
                onChange={(event) => setFile(event.target.files?.[0] ?? null)}
                required
                type="file"
              />
            </label>
            {languageError && <p className="form-error" role="alert">{languageError}</p>}
            <p className="form-hint">{t("audio.uploadLimit")}</p>
            <button
              className="button-primary"
              disabled={isSaving || isLoadingLanguages || languages.length === 0}
              type="submit"
            >
              {isSaving ? t("auth.working") : t("audio.upload")}
            </button>
          </form>
        </>
      )}

      {error && <p className="form-error" role="alert">{error}</p>}
      <div className="activity-actions">
        <button
          className="button-secondary"
          disabled={isSaving}
          onClick={() => void cleanupOrphanAudio(true)}
          type="button"
        >
          {t("audio.cleanupDryRun")}
        </button>
        <button
          className="button-danger"
          disabled={isSaving}
          onClick={() => void cleanupOrphanAudio(false)}
          type="button"
        >
          {t("audio.cleanupExecute")}
        </button>
      </div>
      {cleanupResult && (
        <p role="status">
          {t(cleanupResult.dryRun ? "audio.cleanupPreview" : "audio.cleanupComplete", {
            count: cleanupResult.count
          })}
          {cleanupResult.fileNames.length > 0 && `: ${cleanupResult.fileNames.join(", ")}`}
        </p>
      )}
      <div className="audio-admin-list">
        <h3>{t("audio.savedTracks")}</h3>
        {isLoadingAudio && <p role="status">{t("common.loading")}</p>}
        {!isLoadingAudio && audioGuides.length === 0 && (
          <p className="admin-empty">{t("audio.empty")}</p>
        )}
        {audioGuides.map((guide) => (
          <article className="audio-guide-card" key={guide.id}>
            <p className="eyebrow">
              {guide.languageNativeName} · {guide.languageName}
            </p>
            <div className="audio-admin-title">
              <h4>{guide.title}</h4>
              <span className={guide.isActive ? "status-badge active" : "status-badge inactive"}>
                {t(guide.isActive ? "audio.active" : "audio.inactive")}
              </span>
            </div>
            <audio controls preload="none" src={getMediaUrl(guide.audioUrl)} />
            {editingAudioId === guide.id ? (
              <form className="audio-edit-form" onSubmit={(event) => void handleUpdate(event)}>
                <label>
                  {t("audio.trackTitle")}
                  <input
                    maxLength={200}
                    onChange={(event) => setEditTitle(event.target.value)}
                    required
                    value={editTitle}
                  />
                </label>
                <label>
                  {t("audio.transcript")}
                  <textarea
                    maxLength={50000}
                    onChange={(event) => setEditTranscript(event.target.value)}
                    required
                    rows={5}
                    value={editTranscript}
                  />
                </label>
                <div className="activity-actions">
                  <button className="button-primary" disabled={isSaving} type="submit">
                    {t("admin.save")}
                  </button>
                  <button
                    className="button-secondary"
                    onClick={() => setEditingAudioId(null)}
                    type="button"
                  >
                    {t("admin.cancel")}
                  </button>
                </div>
              </form>
            ) : (
              <>
                <details>
                  <summary>{t("audio.transcript")}</summary>
                  <p>{guide.transcript}</p>
                </details>
                <div className="activity-actions">
                  <button
                    className="button-secondary"
                    onClick={() => startEditing(guide)}
                    type="button"
                  >
                    {t("audio.edit")}
                  </button>
                  <button
                    className={guide.isActive ? "button-danger" : "button-secondary"}
                    disabled={isSaving}
                    onClick={() => void toggleVisibility(guide)}
                    type="button"
                  >
                    {t(guide.isActive ? "audio.hide" : "audio.publish")}
                  </button>
                  <button
                    className="button-danger"
                    disabled={isSaving}
                    onClick={() => void deleteAudio(guide)}
                    type="button"
                  >
                    {t("audio.delete")}
                  </button>
                </div>
                <div className="audio-replace-form">
                  <label>
                    {t("audio.replaceFile")}
                    <input
                      key={`${guide.id}-${replacementInputKey}`}
                      accept=".mp3,.wav,.ogg,.m4a,audio/mpeg,audio/wav,audio/ogg,audio/mp4"
                      onChange={(event) => {
                        const selected = event.target.files?.[0];
                        if (selected) {
                          setReplacementFiles((current) => ({ ...current, [guide.id]: selected }));
                        }
                      }}
                      type="file"
                    />
                  </label>
                  <label>
                    {t("audio.duration")}
                    <input
                      max={3600}
                      min={1}
                      onChange={(event) =>
                        setReplacementDurations((current) => ({
                          ...current,
                          [guide.id]: event.target.value
                        }))
                      }
                      type="number"
                      value={replacementDurations[guide.id] ?? String(guide.durationSeconds)}
                    />
                  </label>
                  <button
                    className="button-secondary"
                    disabled={isSaving || !replacementFiles[guide.id]}
                    onClick={() => void replaceAudioFile(guide)}
                    type="button"
                  >
                    {isSaving ? t("auth.working") : t("audio.replace")}
                  </button>
                </div>
              </>
            )}
          </article>
        ))}
      </div>
    </div>
  );
}
