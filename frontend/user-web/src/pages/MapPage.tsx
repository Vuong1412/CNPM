import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
import * as L from "leaflet";
import {
  Circle,
  MapContainer,
  Marker,
  Popup,
  TileLayer,
  useMap
} from "react-leaflet";
import { Link } from "react-router-dom";
import { ApiError, type AudioGuide, type LocationSummary } from "@qol/shared";
import { useTranslation } from "react-i18next";
import "leaflet/dist/leaflet.css";
import { useAuth } from "../context/AuthContext";
import { useGeolocation } from "../hooks/useGeolocation";
import { useLocations } from "../hooks/useLocations";
import { apiClient, getMediaUrl } from "../services/api";
import {
  findNearestPoiWithinRadius,
  haversineDistanceMeters,
  isIosSafari,
  type GeoPoint
} from "../utils/geo";

type TriggerMethod = "TAP" | "AUTO_GPS";

const poiIcon = L.divIcon({
  className: "qol-poi-marker",
  html: "<span></span>",
  iconSize: [26, 26],
  iconAnchor: [13, 13]
});

const userIcon = L.divIcon({
  className: "qol-user-marker",
  html: "<span></span>",
  iconSize: [20, 20],
  iconAnchor: [10, 10]
});

function createSilentWavUrl(): string {
  const sampleCount = 800;
  const buffer = new ArrayBuffer(44 + sampleCount);
  const view = new DataView(buffer);
  const writeText = (offset: number, value: string) => {
    for (let index = 0; index < value.length; index += 1) {
      view.setUint8(offset + index, value.charCodeAt(index));
    }
  };

  writeText(0, "RIFF");
  view.setUint32(4, 36 + sampleCount, true);
  writeText(8, "WAVE");
  writeText(12, "fmt ");
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, 1, true);
  view.setUint32(24, 8_000, true);
  view.setUint32(28, 8_000, true);
  view.setUint16(32, 1, true);
  view.setUint16(34, 8, true);
  writeText(36, "data");
  view.setUint32(40, sampleCount, true);
  new Uint8Array(buffer, 44).fill(128);

  return URL.createObjectURL(new Blob([buffer], { type: "audio/wav" }));
}

function FollowUserPosition({ position }: { position: GeoPoint | null }) {
  const map = useMap();
  const hasCentered = useRef(false);

  useEffect(() => {
    if (position && !hasCentered.current) {
      map.flyTo([position.latitude, position.longitude], 15, { duration: 0.5 });
      hasCentered.current = true;
    }
  }, [map, position]);

  return null;
}

function FitLocations({
  locations,
  position
}: {
  locations: readonly (LocationSummary & { latitude: number; longitude: number })[];
  position: GeoPoint | null;
}) {
  const map = useMap();

  useEffect(() => {
    if (position || locations.length === 0) {
      return;
    }

    if (locations.length === 1) {
      map.setView([locations[0].latitude, locations[0].longitude], 13);
      return;
    }

    map.fitBounds(
      locations.map((location) => [location.latitude, location.longitude]),
      { padding: [32, 32], maxZoom: 13 }
    );
  }, [locations, map, position]);

  return null;
}

function isGeolocated(
  location: LocationSummary
): location is LocationSummary & { latitude: number; longitude: number } {
  return location.latitude !== null &&
    location.longitude !== null &&
    Number.isFinite(location.latitude) &&
    location.latitude >= -90 &&
    location.latitude <= 90 &&
    Number.isFinite(location.longitude) &&
    location.longitude >= -180 &&
    location.longitude <= 180;
}

export function MapPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { locations, isLoading, error: locationsError } = useLocations();
  const { position, status: geolocationStatus } = useGeolocation();
  const [selectedLocationId, setSelectedLocationId] = useState<string | null>(null);
  const [audioGuides, setAudioGuides] = useState<AudioGuide[]>([]);
  const [currentGuide, setCurrentGuide] = useState<AudioGuide | null>(null);
  const [audioLoading, setAudioLoading] = useState(false);
  const [audioError, setAudioError] = useState<string | null>(null);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [subscriptionRequired, setSubscriptionRequired] = useState(false);
  const [tourStarted, setTourStarted] = useState(false);
  const [audioUnlocked, setAudioUnlocked] = useState(false);
  const [playedRevision, setPlayedRevision] = useState(0);
  const audioRef = useRef<HTMLAudioElement>(null);
  const guideRequests = useRef(new Map<string, Promise<AudioGuide[]>>());
  const autoAttemptedPoiIds = useRef(new Set<string>());
  const playedPoiIds = useRef(new Set<string>());
  const subscriptionBlocked = useRef(false);
  const audioRequestNumber = useRef(0);
  const historyIds = useRef(new Map<string, string>());
  const historyRequests = useRef(new Map<string, Promise<string | null>>());
  const currentGuideRef = useRef<AudioGuide | null>(null);
  const currentLocationIdRef = useRef<string | null>(null);
  const triggerMethodRef = useRef<TriggerMethod>("TAP");
  const unlockingAudio = useRef(false);
  const completedAudioIds = useRef(new Set<string>());
  const iosSafari = useMemo(
    () => isIosSafari(navigator.userAgent, navigator.platform, navigator.maxTouchPoints),
    []
  );

  const mappedLocations = useMemo(
    () => locations.filter(isGeolocated),
    [locations]
  );
  const nearestPoi = useMemo(
    () => position
      ? findNearestPoiWithinRadius(mappedLocations, position, playedPoiIds.current)
      : null,
    [mappedLocations, playedRevision, position]
  );
  const mapCenter: [number, number] = position
    ? [position.latitude, position.longitude]
    : mappedLocations[0]
      ? [mappedLocations[0].latitude, mappedLocations[0].longitude]
      : [16, 107.5];
  const gpsMessageKey = `map.gps.${geolocationStatus}`;

  const loadLocationAudio = useCallback((locationId: string) => {
    const pending = guideRequests.current.get(locationId);
    if (pending) {
      return pending;
    }

    const request = apiClient.get<AudioGuide[]>(
      `locations/${encodeURIComponent(locationId)}/audio`
    );
    guideRequests.current.set(locationId, request);
    request.catch(() => {
      if (guideRequests.current.get(locationId) === request) {
        guideRequests.current.delete(locationId);
      }
    });
    return request;
  }, []);

  const ensureListeningHistory = useCallback((guide: AudioGuide) => {
    const existingId = historyIds.current.get(guide.id);
    if (existingId) {
      return Promise.resolve(existingId);
    }
    const pending = historyRequests.current.get(guide.id);
    if (pending) {
      return pending;
    }
    if (!user) {
      return Promise.resolve(null);
    }

    const request = apiClient
      .post<{ id: string }>("me/listening-history", {
        audioId: guide.id,
        triggerMethod: triggerMethodRef.current
      })
      .then(({ id }) => {
        historyIds.current.set(guide.id, id);
        setHistoryError(null);
        return id;
      })
      .catch(() => {
        setHistoryError(t("map.historyError"));
        return null;
      })
      .finally(() => {
        historyRequests.current.delete(guide.id);
      });
    historyRequests.current.set(guide.id, request);
    return request;
  }, [t, user]);

  const saveListeningProgress = useCallback(async (isCompleted: boolean) => {
    const guide = currentGuideRef.current;
    const player = audioRef.current;
    if (!guide || !player || !user) {
      return;
    }

    const historyId = historyIds.current.get(guide.id) ??
      await historyRequests.current.get(guide.id);
    if (!historyId) {
      return;
    }

    try {
      await apiClient.patch<void>(
        `me/listening-history/${encodeURIComponent(historyId)}`,
        {
          durationSeconds: Math.min(Math.floor(player.currentTime), guide.durationSeconds),
          isCompleted
        }
      );
      setHistoryError(null);
    } catch {
      setHistoryError(t("map.historyError"));
    }
  }, [t, user]);

  const playGuide = useCallback(async (
    location: LocationSummary,
    guide: AudioGuide,
    triggerMethod: TriggerMethod,
    shouldAutoplay: boolean
  ) => {
    const player = audioRef.current;
    if (!player) {
      return;
    }

    player.pause();
    setAudioError(null);
    currentGuideRef.current = guide;
    currentLocationIdRef.current = location.id;
    triggerMethodRef.current = triggerMethod;
    setCurrentGuide(guide);
    setSelectedLocationId(location.id);
    player.src = getMediaUrl(guide.audioUrl);
    player.load();

    if (shouldAutoplay) {
      try {
        await player.play();
        setAudioError(null);
      } catch {
        setAudioError(t(iosSafari ? "map.iosTapTip" : "map.audioError"));
      }
    }
  }, [iosSafari, t]);

  const startLocationAudio = useCallback(async (
    location: LocationSummary,
    triggerMethod: TriggerMethod,
    shouldAutoplay: boolean
  ) => {
    const requestNumber = audioRequestNumber.current + 1;
    audioRequestNumber.current = requestNumber;
    setSelectedLocationId(location.id);
    if (currentGuideRef.current) {
      void saveListeningProgress(false);
    }
    audioRef.current?.pause();
    currentGuideRef.current = null;
    currentLocationIdRef.current = null;
    setCurrentGuide(null);
    setAudioGuides([]);
    setAudioError(null);
    setSubscriptionRequired(false);
    setAudioLoading(true);
    try {
      const guides = await loadLocationAudio(location.id);
      if (requestNumber !== audioRequestNumber.current) {
        return;
      }
      subscriptionBlocked.current = false;
      setAudioGuides(guides);
      if (guides.length === 0) {
        setCurrentGuide(null);
        currentGuideRef.current = null;
        setAudioError(t("map.noAudio"));
        return;
      }
      await playGuide(location, guides[0], triggerMethod, shouldAutoplay);
    } catch (cause) {
      if (requestNumber === audioRequestNumber.current) {
        if (cause instanceof ApiError && cause.status === 402) {
          subscriptionBlocked.current = true;
          setSubscriptionRequired(true);
          setAudioError(t("map.subscriptionRequired"));
        } else {
          setAudioError(t("map.audioError"));
        }
      }
    } finally {
      if (requestNumber === audioRequestNumber.current) {
        setAudioLoading(false);
      }
    }
  }, [loadLocationAudio, playGuide, saveListeningProgress, t]);

  useEffect(() => {
    if (
      !tourStarted ||
      !nearestPoi ||
      subscriptionBlocked.current ||
      autoAttemptedPoiIds.current.has(nearestPoi.poi.id)
    ) {
      return;
    }

    autoAttemptedPoiIds.current.add(nearestPoi.poi.id);
    void startLocationAudio(nearestPoi.poi, "AUTO_GPS", true);
  }, [nearestPoi, startLocationAudio, tourStarted]);

  async function handleStartTour() {
    const player = audioRef.current;
    if (!player) {
      setTourStarted(true);
      return;
    }

    if (currentGuideRef.current) {
      void saveListeningProgress(false);
    }
    player.pause();
    currentGuideRef.current = null;
    currentLocationIdRef.current = null;
    setCurrentGuide(null);
    setAudioGuides([]);
    unlockingAudio.current = true;
    const silentUrl = createSilentWavUrl();
    player.muted = true;
    player.src = silentUrl;
    player.load();
    try {
      await player.play();
      player.pause();
      player.currentTime = 0;
      setAudioUnlocked(true);
      setAudioError(null);
    } catch {
      setAudioError(t(iosSafari ? "map.iosTapTip" : "map.audioError"));
    } finally {
      player.muted = false;
      player.removeAttribute("src");
      player.load();
      URL.revokeObjectURL(silentUrl);
      unlockingAudio.current = false;
      setTourStarted(true);
    }
  }

  function handlePlayerPlay() {
    if (unlockingAudio.current) {
      return;
    }
    const guide = currentGuideRef.current;
    const locationId = currentLocationIdRef.current;
    if (!guide || !locationId) {
      return;
    }

    playedPoiIds.current.add(locationId);
    completedAudioIds.current.delete(guide.id);
    setPlayedRevision((revision) => revision + 1);
    void ensureListeningHistory(guide);
  }

  const selectedLocation = locations.find((location) => location.id === selectedLocationId);

  return (
    <section className="content-page map-page">
      <p className="eyebrow">{t("nav.map")}</p>
      <h1>{t("map.title")}</h1>
      <p>{t("map.description")}</p>

      <div className="map-toolbar">
        <p role="status">{t(gpsMessageKey)}</p>
        <button
          className="button-primary"
          disabled={audioUnlocked}
          onClick={() => void handleStartTour()}
          type="button"
        >
          {audioUnlocked ? t("map.tourEnabled") : t("map.startTour")}
        </button>
      </div>

      {locationsError && <p className="form-error" role="alert">{locationsError}</p>}
      {isLoading && <p role="status">{t("common.loading")}</p>}
      {!isLoading && mappedLocations.length === 0 && (
        <p className="map-empty">{t("map.empty")}</p>
      )}

      <div className="map-frame">
        <MapContainer center={mapCenter} zoom={position ? 15 : 6} scrollWheelZoom>
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <FollowUserPosition position={position} />
          <FitLocations locations={mappedLocations} position={position} />
          {mappedLocations.map((location) => (
            <Fragment key={location.id}>
              <Circle
                center={[location.latitude, location.longitude]}
                pathOptions={{ color: "#23845f", fillOpacity: 0.08 }}
                radius={location.radiusMeters}
              />
              <Marker
                eventHandlers={{
                  click: () => setSelectedLocationId(location.id)
                }}
                icon={poiIcon}
                position={[location.latitude, location.longitude]}
              >
                <Popup>
                  <div className="map-popup">
                    <strong>{location.name}</strong>
                    <span>{location.address}</span>
                    {position && (
                      <span>
                        {t("map.distance", {
                          distance: Math.round(haversineDistanceMeters(position, location))
                        })}
                      </span>
                    )}
                    <button
                      className="button-primary"
                      disabled={audioLoading}
                      onClick={() => void startLocationAudio(location, "TAP", true)}
                      type="button"
                    >
                      {t("map.tapToListen")}
                    </button>
                    {location.directionLink && (
                      <a href={location.directionLink} rel="noopener noreferrer" target="_blank">
                        {t("map.directions")}
                      </a>
                    )}
                  </div>
                </Popup>
              </Marker>
            </Fragment>
          ))}
          {position && (
            <Marker icon={userIcon} position={[position.latitude, position.longitude]}>
              <Popup>{t("map.gps.ready")}</Popup>
            </Marker>
          )}
        </MapContainer>
      </div>

      {!isLoading && locations.length > mappedLocations.length && (
        <p className="muted">{t("map.noCoordinates")}</p>
      )}

      <section className="map-audio-panel" aria-live="polite">
        {selectedLocation && <h2>{selectedLocation.name}</h2>}
        {audioLoading && <p role="status">{t("common.loading")}</p>}
        {audioError && <p className={subscriptionRequired ? "subscription-notice" : "form-error"} role="alert">{audioError}</p>}
        {subscriptionRequired && (
          <Link className="button-primary" to={user ? "/plans" : "/login"}>
            {t(user ? "nav.plans" : "nav.login")}
          </Link>
        )}
        {currentGuide && (
          <div className="map-current-guide">
            <p>{t("map.listeningTo", { name: currentGuide.title })}</p>
            <p className="muted">
              {currentGuide.languageNativeName} · {currentGuide.languageName}
            </p>
            {audioGuides.length > 1 && (
              <div className="map-guide-list" aria-label={t("map.tapToListen")}>
                {audioGuides.map((guide) => (
                  <button
                    className={guide.id === currentGuide.id ? "button-primary" : "button-secondary"}
                    key={guide.id}
                    onClick={() => selectedLocation && void playGuide(selectedLocation, guide, "TAP", true)}
                    type="button"
                  >
                    {guide.languageNativeName}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {!selectedLocation && !audioError && <p>{t("map.audioPrompt")}</p>}
        <audio
          className={currentGuide ? undefined : "map-audio-unlock-player"}
          controls={Boolean(currentGuide)}
          onEnded={() => {
            if (currentGuideRef.current) {
              completedAudioIds.current.add(currentGuideRef.current.id);
            }
            void saveListeningProgress(true);
          }}
          onPause={() => {
            const guideId = currentGuideRef.current?.id;
            if (guideId && !completedAudioIds.current.has(guideId)) {
              void saveListeningProgress(false);
            }
          }}
          onPlay={handlePlayerPlay}
          preload="none"
          ref={audioRef}
        >
          {t("audio.unsupported")}
        </audio>
        {historyError && <p className="form-error" role="alert">{historyError}</p>}
      </section>
    </section>
  );
}
