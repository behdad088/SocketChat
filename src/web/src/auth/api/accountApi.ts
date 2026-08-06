export class AccountError extends Error {
  readonly status: number

  constructor(message: string, status = 0) {
    super(message)
    this.name = 'AccountError'
    this.status = status
  }
}

function problemMessage(problem: unknown, fallback: string): string {
  if (problem && typeof problem === 'object') {
    const { detail, errors } = problem as {
      detail?: string
      errors?: Record<string, string[]>
    }
    if (detail) return detail
    if (errors) {
      const messages = Object.values(errors).flat()
      if (messages.length > 0) return messages.join(' ')
    }
  }
  return fallback
}

async function postJson(path: string, payload: unknown): Promise<Response> {
  try {
    return await fetch(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
  } catch (error) {
    console.error('Account request failed', path, error)
    throw new AccountError('Could not reach the server. Try again.')
  }
}

async function rejectWithProblem(response: Response, fallback: string): Promise<never> {
  if (response.status === 429) {
    throw new AccountError('Too many attempts. Try again in a minute.', 429)
  }
  const problem = await response.json().catch(() => null)
  throw new AccountError(problemMessage(problem, fallback), response.status)
}

export interface RegisteredAccount {
  userId: string
  email: string
}

export async function register(
  email: string,
  password: string,
  confirmPassword: string,
): Promise<RegisteredAccount> {
  const response = await postJson('/api/account/register', {
    email,
    password,
    confirmPassword,
  })

  if (response.status === 409) {
    throw new AccountError('An account with this email already exists.')
  }
  if (!response.ok) {
    await rejectWithProblem(response, 'Registration failed. Try again.')
  }
  return (await response.json()) as RegisteredAccount
}

export async function resendVerificationEmail(email: string): Promise<void> {
  const response = await postJson(
    '/api/account/register/resend-verification-email',
    { email },
  )
  if (!response.ok) {
    await rejectWithProblem(response, 'Could not resend the email. Try again.')
  }
}

export async function forgotPassword(email: string): Promise<void> {
  const response = await postJson('/api/account/forgot-password', {
    email,
    returnUrl: `${window.location.origin}/login`,
  })
  if (!response.ok) {
    await rejectWithProblem(response, 'Could not send the reset link. Try again.')
  }
}

export async function verifyEmail(code: string): Promise<void> {
  const response = await postJson('/api/account/email-verification', { code })
  if (!response.ok) {
    await rejectWithProblem(response, 'Email verification failed.')
  }
}

export async function validateResetCode(code: string): Promise<string> {
  let response: Response
  try {
    response = await fetch(
      `/api/account/reset-password/validate?code=${encodeURIComponent(code)}`,
    )
  } catch (error) {
    console.error('Reset code validation failed', error)
    throw new AccountError('Could not reach the server. Try again.')
  }
  if (!response.ok) {
    await rejectWithProblem(response, 'This reset link is invalid.')
  }
  const { userId } = (await response.json()) as { userId: string }
  return userId
}

export async function resetPassword(
  userId: string,
  code: string,
  password: string,
  confirmPassword: string,
): Promise<void> {
  const response = await postJson('/api/account/reset-password', {
    userId,
    code,
    password,
    confirmPassword,
  })
  if (!response.ok) {
    await rejectWithProblem(response, 'Password reset failed. Try again.')
  }
}
