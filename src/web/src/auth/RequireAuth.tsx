import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext.tsx'

function RequireAuth() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: location.pathname + location.search }}
      />
    )
  }
  return <Outlet />
}

export default RequireAuth
