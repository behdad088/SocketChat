import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { onSessionExpired } from './api/authFetch.ts'
import { login as requestLogin, revoke } from './api/identityApi.ts'
import {
  clearTokens,
  loadEmail,
  loadTokens,
  saveEmail,
  saveTokens,
} from './api/tokenStore.ts'

interface AuthContextValue {
  isAuthenticated: boolean
  email: string | null
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [email, setEmail] = useState<string | null>(() =>
    loadTokens() ? loadEmail() : null,
  )
  const [isAuthenticated, setIsAuthenticated] = useState(() =>
    Boolean(loadTokens()),
  )

  useEffect(
    () =>
      onSessionExpired(() => {
        setIsAuthenticated(false)
        setEmail(null)
      }),
    [],
  )

  const login = useCallback(async (userEmail: string, password: string) => {
    const tokens = await requestLogin(userEmail, password)
    saveTokens(tokens)
    saveEmail(userEmail)
    setEmail(userEmail)
    setIsAuthenticated(true)
  }, [])

  const logout = useCallback(async () => {
    const tokens = loadTokens()
    clearTokens()
    setIsAuthenticated(false)
    setEmail(null)
    if (tokens) await revoke(tokens.refreshToken)
  }, [])

  const value = useMemo(
    () => ({ isAuthenticated, email, login, logout }),
    [isAuthenticated, email, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}
