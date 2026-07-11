import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import AuthLayout from './AuthLayout.tsx'

function ForgotPasswordPage() {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
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
        <button className="auth-submit" type="submit">
          Send reset link
        </button>
      </form>
      <div className="auth-links">
        <Link to="/login">Back to sign in</Link>
      </div>
    </AuthLayout>
  )
}

export default ForgotPasswordPage
