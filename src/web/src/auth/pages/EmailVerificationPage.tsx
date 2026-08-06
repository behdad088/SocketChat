import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import AuthLayout from '../AuthLayout.tsx'
import { AccountError, verifyEmail } from '../api/accountApi.ts'

// Verification codes are one-time-use, so dedupe the on-mount request
// (React StrictMode runs effects twice in development).
const pendingVerifications = new Map<string, Promise<void>>()

function verifyOnce(code: string): Promise<void> {
  let pending = pendingVerifications.get(code)
  if (!pending) {
    pending = verifyEmail(code)
    pendingVerifications.set(code, pending)
  }
  return pending
}

type Status = 'verifying' | 'success' | 'error'

function EmailVerificationPage() {
  const [searchParams] = useSearchParams()
  const code = searchParams.get('code')
  const [status, setStatus] = useState<Status>(code ? 'verifying' : 'error')
  const [error, setError] = useState<string | null>(
    code ? null : 'This verification link is missing its code.',
  )

  useEffect(() => {
    if (!code) return
    let active = true
    verifyOnce(code)
      .then(() => {
        if (active) setStatus('success')
      })
      .catch((verifyError) => {
        if (!active) return
        setStatus('error')
        setError(
          verifyError instanceof AccountError
            ? verifyError.message
            : 'Email verification failed.',
        )
      })
    return () => {
      active = false
    }
  }, [code])

  if (status === 'verifying') {
    return (
      <AuthLayout title="Verifying your email…" subtitle="Hang tight">
        <p className="auth-subtitle">Checking your verification code.</p>
      </AuthLayout>
    )
  }

  if (status === 'success') {
    return (
      <AuthLayout title="Email verified" subtitle="You're all set">
        <p className="auth-notice">Your email has been successfully verified.</p>
        <div className="auth-links">
          <Link to="/login">Go to sign in</Link>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Verification failed" subtitle="Something's off">
      <p className="auth-error" role="alert">
        {error}
      </p>
      <div className="auth-links">
        <Link to="/login">Go to sign in</Link>
        <Link to="/register">Create an account</Link>
      </div>
    </AuthLayout>
  )
}

export default EmailVerificationPage
