export interface GeoPoint {
  latitude: number;
  longitude: number;
}

export interface GeolocatedPoi {
  id: string;
  latitude: number | null;
  longitude: number | null;
  radiusMeters: number;
  priority: number;
}

export interface PoiDistance<TPoi extends GeolocatedPoi> {
  poi: TPoi;
  distanceMeters: number;
}

const EARTH_RADIUS_METERS = 6_371_008.8;

function isValidPoint(point: GeoPoint): boolean {
  return Number.isFinite(point.latitude) &&
    Number.isFinite(point.longitude) &&
    point.latitude >= -90 &&
    point.latitude <= 90 &&
    point.longitude >= -180 &&
    point.longitude <= 180;
}

export function haversineDistanceMeters(from: GeoPoint, to: GeoPoint): number {
  if (!isValidPoint(from) || !isValidPoint(to)) {
    throw new RangeError("Coordinates must be finite values within valid latitude and longitude ranges.");
  }

  const radians = (degrees: number) => degrees * Math.PI / 180;
  const latitudeDelta = radians(to.latitude - from.latitude);
  const longitudeDelta = radians(to.longitude - from.longitude);
  const fromLatitude = radians(from.latitude);
  const toLatitude = radians(to.latitude);
  const haversine = Math.sin(latitudeDelta / 2) ** 2 +
    Math.cos(fromLatitude) * Math.cos(toLatitude) *
    Math.sin(longitudeDelta / 2) ** 2;
  const centralAngle = 2 * Math.atan2(
    Math.sqrt(Math.min(1, haversine)),
    Math.sqrt(Math.max(0, 1 - haversine))
  );

  return EARTH_RADIUS_METERS * centralAngle;
}

export function findNearestPoiWithinRadius<TPoi extends GeolocatedPoi>(
  pois: readonly TPoi[],
  position: GeoPoint,
  playedPoiIds: ReadonlySet<string>
): PoiDistance<TPoi> | null {
  const eligible = pois.flatMap((poi) => {
    if (
      playedPoiIds.has(poi.id) ||
      poi.latitude === null ||
      poi.longitude === null ||
      !Number.isFinite(poi.latitude) ||
      poi.latitude < -90 ||
      poi.latitude > 90 ||
      !Number.isFinite(poi.longitude) ||
      poi.longitude < -180 ||
      poi.longitude > 180 ||
      !Number.isFinite(poi.radiusMeters) ||
      poi.radiusMeters <= 0
    ) {
      return [];
    }

    const distanceMeters = haversineDistanceMeters(position, {
      latitude: poi.latitude,
      longitude: poi.longitude
    });

    return distanceMeters <= poi.radiusMeters ? [{ poi, distanceMeters }] : [];
  });

  eligible.sort((left, right) =>
    left.distanceMeters - right.distanceMeters ||
    right.poi.priority - left.poi.priority ||
    left.poi.id.localeCompare(right.poi.id)
  );

  return eligible[0] ?? null;
}

export function isIosSafari(userAgent: string, platform: string, maxTouchPoints: number): boolean {
  const isAppleMobile = /iPhone|iPad|iPod/i.test(userAgent) ||
    (platform === "MacIntel" && maxTouchPoints > 1);
  return isAppleMobile && /Safari/i.test(userAgent) && !/CriOS|FxiOS|EdgiOS/i.test(userAgent);
}
