import { useEffect, useState } from "react";
import type { Language } from "@qol/shared";
import { apiClient } from "../services/api";

export function useLanguages() {
  const [languages, setLanguages] = useState<Language[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    apiClient
      .get<Language[]>("languages", controller.signal)
      .then(setLanguages)
      .catch((cause: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        setError(cause instanceof Error ? cause.message : "Unable to load languages.");
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, []);

  return { languages, isLoading, error };
}
