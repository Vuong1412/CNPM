import { createApiClient } from "@qol/shared";

const configuredApiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:49820/api/";
const apiBaseUrl =
  configuredApiBaseUrl.startsWith("/")
    ? new URL(configuredApiBaseUrl, window.location.origin).toString()
    : configuredApiBaseUrl;

export const apiClient = createApiClient({
  baseUrl: apiBaseUrl
});

export function getMediaUrl(path: string) {
  return new URL(path, apiBaseUrl).toString();
}
