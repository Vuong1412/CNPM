import { createApiClient } from "@qol/shared";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:49820/api/";

export const apiClient = createApiClient({
  baseUrl: apiBaseUrl
});

export function getMediaUrl(path: string) {
  return new URL(path, apiBaseUrl).toString();
}
