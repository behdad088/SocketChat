import { Navigate, Outlet } from 'react-router-dom'

function RequireAuth() {
  const token = localStorage.getItem('access_token')
  return token ? <Outlet /> : <Navigate to="/login" replace />
}

export default RequireAuth
