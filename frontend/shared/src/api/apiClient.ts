export interface ApiClientOptions {
  baseUrl: string;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly details?: unknown
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function createApiClient({ baseUrl }: ApiClientOptions) {
  const withTrailingSlash = baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`;
  const normalizedBaseUrl =
    typeof window !== "undefined" && withTrailingSlash.startsWith("/")
      ? new URL(withTrailingSlash, window.location.origin).toString()
      : withTrailingSlash;
  let antiforgeryToken: string | null = null;

  async function ensureAntiforgeryToken(signal?: AbortSignal): Promise<string> {
    if (antiforgeryToken) {
      return antiforgeryToken;
    }

    const response = await request<{ requestToken: string }>("auth/antiforgery", {
      method: "GET",
      signal
    });
    antiforgeryToken = response.requestToken;
    return antiforgeryToken;
  }

  async function mutate<T>(
    path: string,
    init: RequestInit,
    signal?: AbortSignal
  ): Promise<T> {
    const requestToken = await ensureAntiforgeryToken(signal);
    const headers = new Headers(init.headers);
    headers.set("X-CSRF-TOKEN", requestToken);

    try {
      const result = await request<T>(path, { ...init, headers, signal });
      antiforgeryToken = null;
      return result;
    } catch (cause) {
      antiforgeryToken = null;
      throw cause;
    }
  }

  async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const url = new URL(path.replace(/^\/+/, ""), normalizedBaseUrl);
    const headers = new Headers(init.headers);
    headers.set("Accept", "application/json");

    const response = await fetch(url, { ...init, headers, credentials: "include" });
    if (response.status === 204) {
      return undefined as T;
    }

    const contentType = response.headers.get("content-type") ?? "";
    const responseBody = contentType.includes("json")
      ? await response.json()
      : await response.text();

    if (!response.ok) {
      const problem =
        typeof responseBody === "object" && responseBody !== null
          ? (responseBody as { detail?: string; title?: string })
          : undefined;
      const message =
        problem?.detail ??
        problem?.title ??
        (typeof responseBody === "string" && responseBody.length > 0
          ? responseBody
          : `API request failed with status ${response.status}`);
      throw new ApiError(message, response.status, responseBody);
    }

    return responseBody as T;
  }

  return {
    async get<T>(path: string, signal?: AbortSignal): Promise<T> {
      return request<T>(path, { method: "GET", signal });
    },
    async post<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
      return mutate<T>(path, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
      }, signal);
    },
    async postForm<T>(path: string, body: FormData, signal?: AbortSignal): Promise<T> {
      return mutate<T>(path, {
        method: "POST",
        body
      }, signal);
    },
    async putForm<T>(path: string, body: FormData, signal?: AbortSignal): Promise<T> {
      return mutate<T>(path, {
        method: "PUT",
        body
      }, signal);
    },
    async put<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
      return mutate<T>(path, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
      }, signal);
    },
    async patch<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
      return mutate<T>(path, {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
      }, signal);
    },
    async delete<T>(path: string, signal?: AbortSignal): Promise<T> {
      return mutate<T>(path, { method: "DELETE" }, signal);
    }
  };
}
