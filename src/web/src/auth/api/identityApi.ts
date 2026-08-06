import type { StoredTokens } from './tokenStore.ts'

const CLIENT_ID = 'web-client'
const SCOPE = 'openid profile chat offline_access'
const TOKEN_ENDPOINT = '/connect/token'
const REVOCATION_ENDPOINT = '/connect/revocation'

export class LoginError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'LoginError'
  }
}

interface TokenResponse {
  access_token: string
  refresh_token: string
  expires_in: number
}

async function requestTokens(body: URLSearchParams): Promise<StoredTokens> {
  let response: Response
  try {
    response = await fetch(TOKEN_ENDPOINT, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body,
    })
  } catch (error) {
    console.error('Token request failed', error)
    throw new LoginError('Could not reach the sign-in service. Try again.')
  }

  if (response.status === 429) {
    throw new LoginError('Too many attempts. Try again in a minute.')
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    if (problem?.error === 'invalid_grant') {
      throw new LoginError('Invalid email or password.')
    }
    console.error('Token request rejected', response.status, problem)
    throw new LoginError('Sign-in failed. Try again.')
  }

  const tokens = (await response.json()) as TokenResponse
  return {
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token,
    expiresAt: Date.now() + tokens.expires_in * 1000,
  }
}

export function login(email: string, password: string): Promise<StoredTokens> {
  return requestTokens(
    new URLSearchParams({
      grant_type: 'password',
      client_id: CLIENT_ID,
      scope: SCOPE,
      username: email,
      password,
    }),
  )
}

export function refresh(refreshToken: string): Promise<StoredTokens> {
  return requestTokens(
    new URLSearchParams({
      grant_type: 'refresh_token',
      client_id: CLIENT_ID,
      refresh_token: refreshToken,
    }),
  )
}

/** Best-effort revocation on logout; failures only get logged. */
export async function revoke(refreshToken: string): Promise<void> {
  try {
    await fetch(REVOCATION_ENDPOINT, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        client_id: CLIENT_ID,
        token: refreshToken,
        token_type_hint: 'refresh_token',
      }),
    })
  } catch (error) {
    console.warn('Refresh token revocation failed', error)
  }
}
