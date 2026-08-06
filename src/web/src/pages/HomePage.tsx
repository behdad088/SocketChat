import { useAuth } from '../auth/AuthContext.tsx'

function HomePage() {
  const { email, logout } = useAuth()

  return (
    <div>
      <h1>Hello World</h1>
      {email && <p>Signed in as {email}</p>}
      <button type="button" onClick={() => void logout()}>
        Sign out
      </button>
    </div>
  )
}

export default HomePage
