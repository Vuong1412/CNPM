import { useEffect, useState } from "react";
import { ApiError } from "@qol/shared";
import type { LocationDetails, LocationSummary } from "@qol/shared";
import { apiClient } from "../services/api";

export function useLocations() {
  const [locations, setLocations] = useState<LocationSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    apiClient
      .get<LocationSummary[]>("locations", controller.signal)
      .then(setLocations)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : "Unable to load locations.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, []);

  return { locations, isLoading, error };
}

export function useLocation(id: string | undefined) {
  const [location, setLocation] = useState<LocationDetails | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    setLocation(null);
    setError(null);
    setNotFound(false);
    setIsLoading(true);

    if (!id) {
      setError("A location ID is required.");
      setIsLoading(false);
      return;
    }

    const controller = new AbortController();

    apiClient
      .get<LocationDetails>(`locations/${encodeURIComponent(id)}`, controller.signal)
      .then(setLocation)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setNotFound(cause instanceof ApiError && cause.status === 404);
          setError(cause instanceof Error ? cause.message : "Unable to load location.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [id]);

  return { location, isLoading, error, notFound };
}
