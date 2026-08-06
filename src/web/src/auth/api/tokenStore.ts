const ACCESS_TOKEN_KEY = 'access_token'
const REFRESH_TOKEN_KEY = 'refresh_token'
const EXPIRES_AT_KEY = 'expires_at'
const EMAIL_KEY = 'auth_email'

export interface StoredTokens {
  accessToken: string
  refreshToken: string
  expiresAt: number
}

export function saveTokens(tokens: StoredTokens): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken)
  localStorage.setItem(EXPIRES_AT_KEY, String(tokens.expiresAt))
}

export function loadTokens(): StoredTokens | null {
  const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY)
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
  const expiresAt = Number(localStorage.getItem(EXPIRES_AT_KEY))
  if (!accessToken || !refreshToken || !Number.isFinite(expiresAt)) return null
  return { accessToken, refreshToken, expiresAt }
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(EXPIRES_AT_KEY)
  localStorage.removeItem(EMAIL_KEY)
}

export function saveEmail(email: string): void {
  localStorage.setItem(EMAIL_KEY, email)
}

export function loadEmail(): string | null {
  return localStorage.getItem(EMAIL_KEY)
}
