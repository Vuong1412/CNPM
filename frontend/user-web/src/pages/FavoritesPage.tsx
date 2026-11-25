import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { FavoriteSummary } from "@qol/shared";
import { useAuth } from "../context/AuthContext";
import { apiClient } from "../services/api";

export function FavoritesPage() {
  const { t } = useTranslation();
  const { user, isLoading: isAuthLoading } = useAuth();
  const [favorites, setFavorites] = useState<FavoriteSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadFavorites = useCallback(async (signal?: AbortSignal) => {
    if (!user) {
      setIsLoading(false);
      return;
    }

    try {
      const items = await apiClient.get<FavoriteSummary[]>("me/favorites", signal);
      setFavorites(items);
      setError(null);
    } catch (cause) {
      if (!signal?.aborted) {
        setError(cause instanceof Error ? cause.message : t("favorites.loadError"));
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
    void loadFavorites(controller.signal);
    return () => controller.abort();
  }, [isAuthLoading, loadFavorites]);

  async function removeFavorite(locationId: string) {
    try {
      await apiClient.delete<void>(`me/favorites/${encodeURIComponent(locationId)}`);
      setFavorites((items) => items.filter((item) => item.locationId !== locationId));
      setError(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("favorites.removeError"));
    }
  }

  if (isAuthLoading || (user && isLoading)) {
    return <section className="content-page"><p role="status">{t("common.loading")}</p></section>;
  }

  return (
    <section className="content-page activity-page">
      <p className="eyebrow">{t("favorites.eyebrow")}</p>
      <h1>{t("favorites.title")}</h1>
      {!user ? (
        <>
          <p>{t("favorites.signIn")}</p>
          <Link className="button-primary" to="/login">{t("nav.login")}</Link>
        </>
      ) : (
        <>
          <p>{t("favorites.description")}</p>
          {error && <p className="form-error" role="alert">{error}</p>}
          {!error && favorites.length === 0 && <p className="admin-empty">{t("favorites.empty")}</p>}
          <div className="activity-list">
            {favorites.map((favorite) => (
              <article className="activity-card" key={favorite.locationId}>
                <div>
                  <h2>{favorite.name}</h2>
                  <p>{favorite.address}</p>
                  <small>
                    {t("favorites.savedOn", {
                      date: new Date(favorite.createdAt).toLocaleDateString()
                    })}
                  </small>
                </div>
                <div className="activity-actions">
                  <Link className="button-secondary" to={`/locations/${favorite.locationId}`}>
                    {t("favorites.open")}
                  </Link>
                  <button
                    className="button-danger"
                    onClick={() => void removeFavorite(favorite.locationId)}
                    type="button"
                  >
                    {t("favorites.remove")}
                  </button>
                </div>
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
