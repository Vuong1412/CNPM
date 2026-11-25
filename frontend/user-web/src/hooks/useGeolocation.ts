import { useEffect, useState } from "react";
import type { GeoPoint } from "../utils/geo";

export type GeolocationStatus =
  | "requesting"
  | "ready"
  | "denied"
  | "unsupported"
  | "error";

interface GeolocationResult {
  position: GeoPoint | null;
  status: GeolocationStatus;
}

export function useGeolocation(): GeolocationResult {
  const [position, setPosition] = useState<GeoPoint | null>(null);
  const [status, setStatus] = useState<GeolocationStatus>("requesting");

  useEffect(() => {
    if (!navigator.geolocation) {
      setStatus("unsupported");
      return;
    }

    setStatus("requesting");
    const watchId = navigator.geolocation.watchPosition(
      (result) => {
        setPosition({
          latitude: result.coords.latitude,
          longitude: result.coords.longitude
        });
        setStatus("ready");
      },
      (error) => {
        setStatus(error.code === error.PERMISSION_DENIED ? "denied" : "error");
      },
      {
        enableHighAccuracy: true,
        maximumAge: 5_000,
        timeout: 15_000
      }
    );

    return () => navigator.geolocation.clearWatch(watchId);
  }, []);

  return { position, status };
}
