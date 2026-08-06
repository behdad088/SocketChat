import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import AuthLayout from '../AuthLayout.tsx'
import { AccountError, resetPassword, validateResetCode } from '../api/accountApi.ts'

type Status = 'validating' | 'ready' | 'invalid' | 'done'

function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const code = searchParams.get('code')
  const [status, setStatus] = useState<Status>(code ? 'validating' : 'invalid')
  const [userId, setUserId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(
    code ? null : 'This reset link is missing its code.',
  )
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (!code) return
    let active = true
    validateResetCode(code)
      .then((id) => {
        if (!active) return
        setUserId(id)
        setStatus('ready')
      })
      .catch((validateError) => {
        if (!active) return
        setStatus('invalid')
        setError(
          validateError instanceof AccountError
            ? validateError.message
            : 'This reset link is invalid.',
        )
      })
    return () => {
      active = false
    }
  }, [code])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!code || !userId) return
    const form = new FormData(event.currentTarget)
    const password = String(form.get('password') ?? '')
    const confirmPassword = String(form.get('confirmPassword') ?? '')

    if (password !== confirmPassword) {
      setError('The password and confirmation password do not match.')
      return
    }

    setError(null)
    setSubmitting(true)
    try {
      await resetPassword(userId, code, password, confirmPassword)
      setStatus('done')
    } catch (resetError) {
      if (resetError instanceof AccountError) {
        // this error code 410 means, the code is either expired or already used
        if (resetError.status === 410) setStatus('invalid')
        setError(resetError.message)
      } else {
        setError('Password reset failed. Try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (status === 'validating') {
    return (
      <AuthLayout title="Checking reset link…" subtitle="Hang tight">
        <p className="auth-subtitle">Validating your reset code.</p>
      </AuthLayout>
    )
  }

  if (status === 'invalid') {
    return (
      <AuthLayout title="Reset link problem" subtitle="Something's off">
        <p className="auth-error" role="alert">
          {error}
        </p>
        <div className="auth-links">
          <Link to="/forgot-password">Request a new reset link</Link>
          <Link to="/login">Back to sign in</Link>
        </div>
      </AuthLayout>
    )
  }

  if (status === 'done') {
    return (
      <AuthLayout title="Password reset" subtitle="You're all set">
        <p className="auth-notice">Your password has been successfully reset.</p>
        <div className="auth-links">
          <Link to="/login">Go to sign in</Link>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Choose a new password" subtitle="Almost there">
      <form className="auth-form" onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="reset-password">New password</label>
          <input
            id="reset-password"
            name="password"
            type="password"
            autoComplete="new-password"
            required
          />
        </div>
        <div className="auth-field">
          <label htmlFor="reset-confirm-password">Confirm new password</label>
          <input
            id="reset-confirm-password"
            name="confirmPassword"
            type="password"
            autoComplete="new-password"
            required
          />
        </div>
        {error && (
          <p className="auth-error" role="alert">
            {error}
          </p>
        )}
        <button className="auth-submit" type="submit" disabled={submitting}>
          {submitting ? 'Resetting…' : 'Reset password'}
        </button>
      </form>
      <div className="auth-links">
        <Link to="/login">Back to sign in</Link>
      </div>
    </AuthLayout>
  )
}

export default ResetPasswordPage
