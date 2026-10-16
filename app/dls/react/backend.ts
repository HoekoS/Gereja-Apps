// Minimal fetch wrapper for this project's own API contract
// (docs/requirements/API-CONTRACT.md): base path /api, no /v1 segment, and
// the {"error":{"code":"...","message":"..."}} envelope api/internal/platform
// writes — not Matsu's generic {"error":{"message":"..."}} one. There is no
// account/session layer here yet: the Access context (the booth's PIN
// pairing cookie, FR-SEC-01) is a later slice, so this has no
// useBackend()/account the way Matsu's backend.ts does.
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
  ) {
    super(message);
  }
}

export async function api<T = any>(path: string, method = "GET", body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api${path}`, {
      method,
      credentials: "same-origin",
      signal: AbortSignal.timeout(10000),
      headers: { "Content-Type": "application/json", "X-Matsu-Client": "1" },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
  } catch {
    throw new ApiError(0, "NETWORK_ERROR", "Could not reach the server.");
  }
  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new ApiError(
      response.status,
      data?.error?.code ?? "UNKNOWN_ERROR",
      data?.error?.message || `Request failed (${response.status})`,
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
