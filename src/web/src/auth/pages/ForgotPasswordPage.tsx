import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import AuthLayout from '../AuthLayout.tsx'
import { AccountError, forgotPassword } from '../api/accountApi.ts'

function ForgotPasswordPage() {
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [sentTo, setSentTo] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const email = String(new FormData(event.currentTarget).get('email') ?? '')

    setError(null)
    setSubmitting(true)
    try {
      await forgotPassword(email)
      setSentTo(email)
    } catch (forgotError) {
      setError(
        forgotError instanceof AccountError
          ? forgotError.message
          : 'Could not send the reset link. Try again.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  if (sentTo) {
    return (
      <AuthLayout title="Check your inbox" subtitle="Reset link on its way">
        <p className="auth-subtitle">
          If an account exists for <strong>{sentTo}</strong>, a password reset
          link has been sent.
        </p>
        <div className="auth-links">
          <Link to="/login">Back to sign in</Link>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Reset your password" subtitle="We'll send you a reset link">
      <form className="auth-form" onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="forgot-email">Email</label>
          <input
            id="forgot-email"
            name="email"
            type="email"
            autoComplete="email"
            required
          />
        </div>
        {error && (
          <p className="auth-error" role="alert">
            {error}
          </p>
        )}
        <button className="auth-submit" type="submit" disabled={submitting}>
          {submitting ? 'Sending…' : 'Send reset link'}
        </button>
      </form>
      <div className="auth-links">
        <Link to="/login">Back to sign in</Link>
      </div>
    </AuthLayout>
  )
}

export default ForgotPasswordPage
