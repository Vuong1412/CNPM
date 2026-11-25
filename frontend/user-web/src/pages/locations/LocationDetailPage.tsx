import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ApiError, type AudioGuide, type LocationReview } from "@qol/shared";
import { useLocation } from "../../hooks/useLocations";
import { useAuth } from "../../context/AuthContext";
import { apiClient, getMediaUrl } from "../../services/api";

export function LocationDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const { location, isLoading, error, notFound } = useLocation(id);
  const { user } = useAuth();
  const [audioGuides, setAudioGuides] = useState<AudioGuide[]>([]);
  const [audioLoading, setAudioLoading] = useState(false);
  const [audioError, setAudioError] = useState<string | null>(null);
  const [isFavorite, setIsFavorite] = useState(false);
  const [isSavingFavorite, setIsSavingFavorite] = useState(false);
  const [favoriteError, setFavoriteError] = useState<string | null>(null);
  const [playbackError, setPlaybackError] = useState<string | null>(null);
  const [reviews, setReviews] = useState<LocationReview[]>([]);
  const [myReview, setMyReview] = useState<LocationReview | null>(null);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [isSavingReview, setIsSavingReview] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const listeningIds = useRef(new Map<string, string>());
  const listeningRequests = useRef(new Map<string, Promise<void>>());
  const completedSessions = useRef(new Set<string>());

  useEffect(() => {
    if (!id) {
      return;
    }

    const controller = new AbortController();
    setAudioLoading(true);
    setAudioError(null);
    apiClient
      .get<AudioGuide[]>(`locations/${encodeURIComponent(id)}/audio`, controller.signal)
      .then(setAudioGuides)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setAudioError(cause instanceof Error ? cause.message : t("audio.loadError"));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setAudioLoading(false);
        }
      });

    return () => controller.abort();
  }, [id, t]);

  useEffect(() => {
    if (!location || !user) {
      setIsFavorite(false);
      setFavoriteError(null);
      return;
    }

    const controller = new AbortController();
    apiClient
      .get<{ locationId: string }[]>("me/favorites", controller.signal)
      .then((items) => {
        setIsFavorite(items.some((item) => item.locationId === location.id));
        setFavoriteError(null);
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setFavoriteError(cause instanceof Error ? cause.message : t("favorites.loadError"));
        }
      });

    return () => controller.abort();
  }, [location, t, user]);

  useEffect(() => {
    if (!location) {
      return;
    }

    const controller = new AbortController();
    apiClient
      .get<LocationReview[]>(`locations/${encodeURIComponent(location.id)}/reviews`, controller.signal)
      .then(setReviews)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setReviewError(cause instanceof Error ? cause.message : t("reviews.loadError"));
        }
      });

    return () => controller.abort();
  }, [location, t]);

  useEffect(() => {
    if (!location || !user) {
      setMyReview(null);
      setRating(5);
      setComment("");
      return;
    }

    const controller = new AbortController();
    apiClient
      .get<LocationReview>(
        `me/locations/${encodeURIComponent(location.id)}/review`,
        controller.signal
      )
      .then((review) => {
        setMyReview(review);
        setRating(review.rating);
        setComment(review.comment ?? "");
      })
      .catch((cause: unknown) => {
        if (cause instanceof ApiError && cause.status === 404) {
          setMyReview(null);
          setRating(5);
          setComment("");
        } else if (!controller.signal.aborted) {
          setReviewError(cause instanceof Error ? cause.message : t("reviews.loadError"));
        }
      });

    return () => controller.abort();
  }, [location, t, user]);

  async function submitReview(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!location || !user) {
      return;
    }

    setIsSavingReview(true);
    setReviewError(null);
    try {
      const savedReview = await apiClient.put<LocationReview>(
        `me/locations/${encodeURIComponent(location.id)}/review`,
        { rating, comment }
      );
      setMyReview(savedReview);
      setReviews(await apiClient.get<LocationReview[]>(
        `locations/${encodeURIComponent(location.id)}/reviews`
      ));
    } catch (cause) {
      setReviewError(cause instanceof Error ? cause.message : t("reviews.saveError"));
    } finally {
      setIsSavingReview(false);
    }
  }

  async function deleteReview() {
    if (!location) {
      return;
    }
    try {
      await apiClient.delete<void>(
        `me/locations/${encodeURIComponent(location.id)}/review`
      );
      setMyReview(null);
      setRating(5);
      setComment("");
      setReviews(await apiClient.get<LocationReview[]>(
        `locations/${encodeURIComponent(location.id)}/reviews`
      ));
      setReviewError(null);
    } catch (cause) {
      setReviewError(cause instanceof Error ? cause.message : t("reviews.saveError"));
    }
  }

  async function toggleFavorite() {
    if (!location || !user) {
      return;
    }
    setIsSavingFavorite(true);
    setFavoriteError(null);
    try {
      if (isFavorite) {
        await apiClient.delete<void>(`me/favorites/${encodeURIComponent(location.id)}`);
        setIsFavorite(false);
      } else {
        await apiClient.put<void>(`me/favorites/${encodeURIComponent(location.id)}`, {});
        setIsFavorite(true);
      }
    } catch (cause) {
      setFavoriteError(cause instanceof Error ? cause.message : t("favorites.saveError"));
    } finally {
      setIsSavingFavorite(false);
    }
  }

  async function startListening(guide: AudioGuide) {
    if (!user || listeningIds.current.has(guide.id)) {
      return;
    }
    const pending = listeningRequests.current.get(guide.id);
    if (pending) {
      await pending;
      return;
    }

    const request = apiClient
      .post<{ id: string }>("me/listening-history", { audioId: guide.id })
      .then((session) => {
        listeningIds.current.set(guide.id, session.id);
        setPlaybackError(null);
      })
      .catch((cause: unknown) => {
        setPlaybackError(cause instanceof Error ? cause.message : t("audio.historyError"));
      })
      .finally(() => {
        listeningRequests.current.delete(guide.id);
      });
    listeningRequests.current.set(guide.id, request);
    await request;
  }

  async function saveListeningProgress(
    guide: AudioGuide,
    currentTime: number,
    isCompleted: boolean
  ) {
    if (!user || (!listeningIds.current.has(guide.id) && !listeningRequests.current.has(guide.id))) {
      return;
    }

    await listeningRequests.current.get(guide.id);
    const historyId = listeningIds.current.get(guide.id);
    if (!historyId) {
      return;
    }

    try {
      await apiClient.patch<void>(
        `me/listening-history/${encodeURIComponent(historyId)}`,
        {
          durationSeconds: Math.min(Math.floor(currentTime), guide.durationSeconds),
          isCompleted
        }
      );
      setPlaybackError(null);
    } catch (cause) {
      setPlaybackError(cause instanceof Error ? cause.message : t("audio.historyError"));
    }
  }

  return (
    <section className="content-page">
      <Link to="/explore">{t("location.back")}</Link>
      {isLoading && <p role="status">{t("common.loading")}</p>}
      {error && (
        <p role="alert">
          {notFound
            ? t("location.notFound")
            : t("location.error", { message: error })}
        </p>
      )}
      {location && (
        <>
          <p className="eyebrow">{t("explore.eyebrow")}</p>
          <h1>{location.name}</h1>
          <p>{location.address}</p>
          <div className="activity-actions">
            {user ? (
              <button
                className="button-secondary"
                disabled={isSavingFavorite}
                onClick={() => void toggleFavorite()}
                type="button"
              >
                {isSavingFavorite
                  ? t("auth.working")
                  : t(isFavorite ? "favorites.remove" : "favorites.save")}
              </button>
            ) : (
              <Link className="button-secondary" to="/login">
                {t("favorites.signInToSave")}
              </Link>
            )}
          </div>
          {favoriteError && <p className="form-error" role="alert">{favoriteError}</p>}
          {playbackError && (
            <p className="form-error" role="alert">
              {t("audio.historyError", { message: playbackError })}
            </p>
          )}
          <section className="audio-guides" aria-labelledby="audio-guides-title">
            <h2 id="audio-guides-title">{t("audio.title")}</h2>
            {audioLoading && <p role="status">{t("common.loading")}</p>}
            {audioError && <p role="alert">{t("audio.loadError")}: {audioError}</p>}
            {!audioLoading && !audioError && audioGuides.length === 0 && (
              <p>{t("audio.empty")}</p>
            )}
            {audioGuides.map((guide) => (
              <article className="audio-guide-card" key={guide.id}>
                <p className="eyebrow">
                  {guide.languageNativeName} · {guide.languageName}
                </p>
                <h3>{guide.title}</h3>
                <audio
                  controls
                  preload="none"
                  src={getMediaUrl(guide.audioUrl)}
                  onEnded={(event) => {
                    completedSessions.current.add(guide.id);
                    void saveListeningProgress(guide, event.currentTarget.currentTime, true);
                  }}
                  onPause={(event) => {
                    if (!completedSessions.current.has(guide.id)) {
                      void saveListeningProgress(guide, event.currentTarget.currentTime, false);
                    }
                  }}
                  onPlay={() => {
                    if (completedSessions.current.has(guide.id)) {
                      completedSessions.current.delete(guide.id);
                      listeningIds.current.delete(guide.id);
                    }
                    void startListening(guide);
                  }}
                >
                  {t("audio.unsupported")}
                </audio>
                <details>
                  <summary>{t("audio.transcript")}</summary>
                  <p>{guide.transcript}</p>
                </details>
              </article>
            ))}
          </section>
          <section className="location-reviews" aria-labelledby="reviews-title">
            <h2 id="reviews-title">{t("reviews.title")}</h2>
            {reviewError && <p className="form-error" role="alert">{reviewError}</p>}
            {user ? (
              <form className="review-form" onSubmit={(event) => void submitReview(event)}>
                <label>
                  {t("reviews.rating")}
                  <select
                    onChange={(event) => setRating(Number(event.target.value))}
                    value={rating}
                  >
                    {[5, 4, 3, 2, 1].map((value) => (
                      <option key={value} value={value}>
                        {value} / 5
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  {t("reviews.comment")}
                  <textarea
                    maxLength={2000}
                    onChange={(event) => setComment(event.target.value)}
                    rows={4}
                    value={comment}
                  />
                </label>
                <div className="activity-actions">
                  <button className="button-primary" disabled={isSavingReview} type="submit">
                    {isSavingReview
                      ? t("auth.working")
                      : t(myReview ? "reviews.update" : "reviews.submit")}
                  </button>
                  {myReview && (
                    <button
                      className="button-danger"
                      onClick={() => void deleteReview()}
                      type="button"
                    >
                      {t("reviews.delete")}
                    </button>
                  )}
                </div>
              </form>
            ) : (
              <p>
                <Link to="/login">{t("reviews.signIn")}</Link>
              </p>
            )}
            {!reviewError && reviews.length === 0 && <p>{t("reviews.empty")}</p>}
            <div className="review-list">
              {reviews.map((review) => (
                <article className="review-card" key={review.id}>
                  <div className="activity-card-heading">
                    <strong>{review.displayName}</strong>
                    <span aria-label={t("reviews.ratingValue", { rating: review.rating })}>
                      {"★".repeat(review.rating)}{"☆".repeat(5 - review.rating)}
                    </span>
                  </div>
                  {review.comment && <p>{review.comment}</p>}
                  <small>{new Date(review.createdAt).toLocaleDateString()}</small>
                </article>
              ))}
            </div>
          </section>
        </>
      )}
    </section>
  );
}
