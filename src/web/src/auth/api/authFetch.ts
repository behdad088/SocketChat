import { refresh } from './identityApi.ts'
import { clearTokens, loadTokens, saveTokens } from './tokenStore.ts'

/** Refresh this long before the access token actually expires. */
const EXPIRY_MARGIN_MS = 30_000

type SessionExpiredListener = () => void

const sessionExpiredListeners = new Set<SessionExpiredListener>()

export function onSessionExpired(listener: SessionExpiredListener): () => void {
  sessionExpiredListeners.add(listener)
  return () => sessionExpiredListeners.delete(listener)
}

function expireSession(): void {
  clearTokens()
  for (const listener of sessionExpiredListeners) listener()
}

let refreshInFlight: Promise<string> | null = null

async function refreshTokens(refreshToken: string): Promise<string> {
  try {
    const tokens = await refresh(refreshToken)
    saveTokens(tokens)
    return tokens.accessToken
  } catch (error) {
    expireSession()
    throw error
  }
}

/**
 * Returns a usable access token, refreshing it first when it is missing,
 * expired, or about to expire. Concurrent callers share a single refresh
 * request, rotating refresh tokens are one-time-use, so a second parallel
 * refresh would be rejected.
 */
export function getValidAccessToken(): Promise<string> {
  if (refreshInFlight) return refreshInFlight

  const tokens = loadTokens()
  if (!tokens) {
    expireSession()
    return Promise.reject(new Error('Not signed in'))
  }

  if (Date.now() < tokens.expiresAt - EXPIRY_MARGIN_MS) {
    return Promise.resolve(tokens.accessToken)
  }

  refreshInFlight = refreshTokens(tokens.refreshToken).finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

/**
 * fetch() with a Bearer token attached. If the server still answers 401
 * (e.g. the token was revoked server-side), refreshes once and retries.
 */
export async function authFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const send = (token: string) => {
    const headers = new Headers(init.headers)
    headers.set('Authorization', `Bearer ${token}`)
    return fetch(input, { ...init, headers })
  }

  const response = await send(await getValidAccessToken())
  if (response.status !== 401) return response

  const tokens = loadTokens()
  if (!tokens) return response

  refreshInFlight ??= refreshTokens(tokens.refreshToken).finally(() => {
    refreshInFlight = null
  })
  return send(await refreshInFlight)
}
