import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { Language } from "@qol/shared";
import { useLanguages } from "../hooks/useLanguages";
import { apiClient } from "../services/api";

interface VisitorSessionResponse {
  sessionId: string;
  languageCode: string | null;
  expiresAt: string;
}

interface VisitorLanguageResponse {
  languageCode: string;
  languageName: string;
}

function getDeviceId(): string {
  const storageKey = "qol.anonymous-device-id";
  const storedId = window.localStorage.getItem(storageKey);
  if (storedId && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(storedId)) {
    return storedId;
  }

  const deviceId = window.crypto.randomUUID();
  window.localStorage.setItem(storageKey, deviceId);
  return deviceId;
}

export function AccessPage() {
  const { t, i18n } = useTranslation();
  const { languages, isLoading: isLoadingLanguages, error: languageError } = useLanguages();
  const [selectedLanguage, setSelectedLanguage] = useState("");
  const [isStarting, setIsStarting] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [sessionExpiresAt, setSessionExpiresAt] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let deviceId: string;
    try {
      deviceId = getDeviceId();
    } catch {
      setError(i18n.t("access.startError"));
      setIsStarting(false);
      return () => controller.abort();
    }

    void apiClient.post<VisitorSessionResponse>(
      "access/start",
      { deviceId },
      controller.signal
    ).then(async (session) => {
      if (controller.signal.aborted) {
        return;
      }

      setSessionExpiresAt(session.expiresAt);
      const languageCode = session.languageCode ?? i18n.resolvedLanguage ?? "vi";
      setSelectedLanguage(languageCode);
      await i18n.changeLanguage(languageCode);
      setError(null);
    }).catch((cause: unknown) => {
      if (!controller.signal.aborted) {
        setError(cause instanceof Error ? cause.message : i18n.t("access.startError"));
      }
    }).finally(() => {
      if (!controller.signal.aborted) {
        setIsStarting(false);
      }
    });

    return () => controller.abort();
  }, [i18n]);

  async function handleLanguageChange(language: Language) {
    setIsSaving(true);
    setError(null);
    setNotice(null);
    try {
      await apiClient.put<VisitorLanguageResponse>("session/language", {
        languageCode: language.code
      });
      await i18n.changeLanguage(language.code);
      setSelectedLanguage(language.code);
      setNotice(i18n.t("access.languageSaved", { language: language.nativeName }));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("access.languageError"));
    } finally {
      setIsSaving(false);
    }
  }

  const currentLanguage = languages.find((language) => language.code === selectedLanguage);

  return (
    <section className="content-page access-page">
      <p className="eyebrow">{t("access.eyebrow")}</p>
      <h1>{t("access.title")}</h1>
      <p>{t("access.description")}</p>

      <div className="access-panel">
        <h2>{t("access.languageTitle")}</h2>
        <p>{t("access.languageDescription")}</p>
        {error && <p className="form-error" role="alert">{error}</p>}
        {languageError && <p className="form-error" role="alert">{languageError}</p>}
        {(isStarting || isLoadingLanguages) && <p role="status">{t("common.loading")}</p>}
        {!isStarting && !isLoadingLanguages && languages.length > 0 && (
          <label>
            {t("access.languageLabel")}
            <select
              disabled={isSaving}
              onChange={(event) => {
                const language = languages.find((item) => item.code === event.target.value);
                if (language) {
                  void handleLanguageChange(language);
                }
              }}
              value={selectedLanguage}
            >
              {languages.map((language) => (
                <option key={language.code} value={language.code}>
                  {language.nativeName} · {language.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {isSaving && <p role="status">{t("common.loading")}</p>}
        {notice && <p role="status">{notice}</p>}
        {currentLanguage && sessionExpiresAt && (
          <p className="muted">
            {t("access.sessionUntil", {
              date: new Intl.DateTimeFormat(i18n.resolvedLanguage, {
                dateStyle: "medium",
                timeStyle: "short"
              }).format(new Date(sessionExpiresAt))
            })}
          </p>
        )}
      </div>

      <div className="access-panel">
        <h2>{t("access.audioTitle")}</h2>
        <p>{t("access.audioDescription")}</p>
        <div className="activity-actions">
          <Link className="button-primary" to="/explore">{t("access.explore")}</Link>
          <Link className="button-secondary" to="/login">{t("nav.login")}</Link>
          <Link className="button-secondary" to="/plans">{t("nav.plans")}</Link>
        </div>
      </div>
    </section>
  );
}
