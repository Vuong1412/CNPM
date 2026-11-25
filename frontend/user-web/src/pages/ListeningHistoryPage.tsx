import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { ListeningHistoryItem } from "@qol/shared";
import { useAuth } from "../context/AuthContext";
import { apiClient, getMediaUrl } from "../services/api";

export function ListeningHistoryPage() {
  const { t } = useTranslation();
  const { user, isLoading: isAuthLoading } = useAuth();
  const [history, setHistory] = useState<ListeningHistoryItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadHistory = useCallback(async (signal?: AbortSignal) => {
    if (!user) {
      setIsLoading(false);
      return;
    }

    try {
      const items = await apiClient.get<ListeningHistoryItem[]>(
        "me/listening-history",
        signal
      );
      setHistory(items);
      setError(null);
    } catch (cause) {
      if (!signal?.aborted) {
        setError(cause instanceof Error ? cause.message : t("history.loadError"));
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [t, user]);

  useEffect(() => {
    if (isAuthLoading) {
      return;
    }
    const controller = new AbortController();
    void loadHistory(controller.signal);
    return () => controller.abort();
  }, [isAuthLoading, loadHistory]);

  if (isAuthLoading || (user && isLoading)) {
    return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
  }

  return (
    <section className="content-page activity-page">
      <p className="eyebrow">{t("history.eyebrow")}</p>
      <h1>{t("history.title")}</h1>
      {!user ? (
        <>
          <p>{t("history.signIn")}</p>
          <Link className="button-primary" to="/login">{t("nav.login")}</Link>
        </>
      ) : (
        <>
          <p>{t("history.description")}</p>
          {error && <p className="form-error" role="alert">{error}</p>}
          {!error && history.length === 0 && <p className="admin-empty">{t("history.empty")}</p>}
          <div className="activity-list">
            {history.map((item) => {
              const progress = item.audioDurationSeconds > 0
                ? Math.min(100, Math.round(item.durationSeconds / item.audioDurationSeconds * 100))
                : 0;
              return (
                <article className="activity-card history-card" key={item.id}>
                  <div className="activity-card-heading">
                    <div>
                      <p className="eyebrow">{item.languageName}</p>
                      <h2>{item.audioTitle}</h2>
                      <Link to={`/locations/${item.locationId}`}>{item.locationName}</Link>
                    </div>
                    <span className={item.isCompleted ? "status-badge active" : "status-badge inactive"}>
                      {t(item.isCompleted ? "history.completed" : "history.inProgress")}
                    </span>
                  </div>
                  <audio controls preload="none" src={getMediaUrl(item.audioUrl)}>
                    {t("audio.unsupported")}
                  </audio>
                  <p>
                    {t("history.progress", {
                      percent: progress,
                      date: new Date(item.listenedAt).toLocaleString()
                    })}
                  </p>
                </article>
              );
            })}
          </div>
        </>
      )}
    </section>
  );
}
