import type { ReactNode } from 'react'
import './auth.css'

interface AuthLayoutProps {
  title: string
  subtitle?: string
  children: ReactNode
}

function AuthLayout({ title, subtitle, children }: AuthLayoutProps) {
  return (
    <div className="auth-layout">
      <div className="auth-card">
        <span className="auth-mascot" aria-hidden="true">
          💬
        </span>
        <h1 className="auth-brand">SocketChat</h1>
        <h2>{title}</h2>
        {subtitle && <p className="auth-subtitle">{subtitle}</p>}
        {children}
      </div>
    </div>
  )
}

export default AuthLayout
