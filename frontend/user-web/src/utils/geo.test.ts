import { describe, expect, it } from "vitest";
import {
  findNearestPoiWithinRadius,
  haversineDistanceMeters,
  isIosSafari,
  type GeolocatedPoi
} from "./geo";

const pois: GeolocatedPoi[] = [
  { id: "far", latitude: 10.001, longitude: 106, radiusMeters: 500, priority: 10 },
  { id: "near", latitude: 10, longitude: 106, radiusMeters: 200, priority: 0 },
  { id: "same-point-low", latitude: 10, longitude: 106, radiusMeters: 200, priority: 1 },
  { id: "same-point-high", latitude: 10, longitude: 106, radiusMeters: 200, priority: 5 },
  { id: "unmapped", latitude: null, longitude: null, radiusMeters: 200, priority: 100 }
];

describe("haversineDistanceMeters", () => {
  it("returns zero at the same point and a known distance for one degree of latitude", () => {
    expect(haversineDistanceMeters(
      { latitude: 10, longitude: 106 },
      { latitude: 10, longitude: 106 }
    )).toBe(0);
    const oneDegreeLatitude = haversineDistanceMeters(
      { latitude: 0, longitude: 0 },
      { latitude: 1, longitude: 0 }
    );
    expect(Math.abs(oneDegreeLatitude - 111_195)).toBeLessThan(100);
  });

  it("measures across the antimeridian using the shorter arc", () => {
    expect(haversineDistanceMeters(
      { latitude: 0, longitude: 179.9 },
      { latitude: 0, longitude: -179.9 }
    )).toBeLessThan(23_000);
  });
});

describe("findNearestPoiWithinRadius", () => {
  it("chooses the nearest eligible POI, then higher priority for equal distances", () => {
    const nearest = findNearestPoiWithinRadius(
      pois,
      { latitude: 10, longitude: 106 },
      new Set()
    );

    expect(nearest?.poi.id).toBe("same-point-high");
    expect(nearest?.distanceMeters).toBe(0);
  });

  it("excludes played, unmapped, and out-of-radius POIs", () => {
    const nearest = findNearestPoiWithinRadius(
      pois,
      { latitude: 10, longitude: 106 },
      new Set(["same-point-high", "same-point-low", "near"])
    );

    expect(nearest?.poi.id).toBe("far");
    expect(findNearestPoiWithinRadius(
      [{ ...pois[0], radiusMeters: 1 }],
      { latitude: 10, longitude: 106 },
      new Set()
    )).toBeNull();
  });
});

describe("isIosSafari", () => {
  it("detects Safari on iOS but excludes other iOS browsers", () => {
    expect(isIosSafari(
      "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1",
      "iPhone",
      5
    )).toBe(true);
    expect(isIosSafari(
      "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 CriOS/120.0 Mobile/15E148 Safari/604.1",
      "iPhone",
      5
    )).toBe(false);
  });
});
