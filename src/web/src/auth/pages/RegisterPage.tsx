import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import AuthLayout from '../AuthLayout.tsx'
import { AccountError, register, resendVerificationEmail } from '../api/accountApi.ts'

function RegisterPage() {
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null)
  const [resendNotice, setResendNotice] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const email = String(form.get('email') ?? '')
    const password = String(form.get('password') ?? '')
    const confirmPassword = String(form.get('confirmPassword') ?? '')

    if (password !== confirmPassword) {
      setError('The password and confirmation password do not match.')
      return
    }

    setError(null)
    setSubmitting(true)
    try {
      const account = await register(email, password, confirmPassword)
      setRegisteredEmail(account.email)
    } catch (registerError) {
      setError(
        registerError instanceof AccountError
          ? registerError.message
          : 'Registration failed. Try again.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  async function handleResend() {
    if (!registeredEmail) return
    setResendNotice(null)
    try {
      await resendVerificationEmail(registeredEmail)
      setResendNotice('Verification email sent.')
    } catch (resendError) {
      setResendNotice(
        resendError instanceof AccountError
          ? resendError.message
          : 'Could not resend the email. Try again.',
      )
    }
  }

  if (registeredEmail) {
    return (
      <AuthLayout title="Check your inbox" subtitle="One more step">
        <p className="auth-subtitle">
          Account created. We sent a verification link to{' '}
          <strong>{registeredEmail}</strong>.
        </p>
        {resendNotice && <p className="auth-notice">{resendNotice}</p>}
        <button className="auth-submit" type="button" onClick={() => void handleResend()}>
          Resend email
        </button>
        <div className="auth-links">
          <Link to="/login">Go to sign in</Link>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Create an account" subtitle="Join the conversation">
      <form className="auth-form" onSubmit={handleSubmit}>
        <div className="auth-field">
          <label htmlFor="register-email">Email</label>
          <input
            id="register-email"
            name="email"
            type="email"
            autoComplete="email"
            required
          />
        </div>
        <div className="auth-field">
          <label htmlFor="register-password">Password</label>
          <input
            id="register-password"
            name="password"
            type="password"
            autoComplete="new-password"
            required
          />
        </div>
        <div className="auth-field">
          <label htmlFor="register-confirm-password">Confirm password</label>
          <input
            id="register-confirm-password"
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
          {submitting ? 'Creating account…' : 'Create account'}
        </button>
      </form>
      <div className="auth-links">
        <Link to="/login">Back to sign in</Link>
      </div>
    </AuthLayout>
  )
}

export default RegisterPage
