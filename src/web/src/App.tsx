import { Navigate, Route, Routes } from 'react-router-dom'
import EmailVerificationPage from './auth/pages/EmailVerificationPage.tsx'
import ForgotPasswordPage from './auth/pages/ForgotPasswordPage.tsx'
import LoginPage from './auth/pages/LoginPage.tsx'
import RegisterPage from './auth/pages/RegisterPage.tsx'
import RequireAuth from './auth/RequireAuth.tsx'
import ResetPasswordPage from './auth/pages/ResetPasswordPage.tsx'
import HomePage from './pages/HomePage.tsx'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/email-verification" element={<EmailVerificationPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/" element={<HomePage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
