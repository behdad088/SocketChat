import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Identity.API — https://localhost:7063 in docker compose, :5007 via dotnet run
  const identityUrl = env.VITE_IDENTITY_PROXY_TARGET ?? 'https://localhost:7063'
  const identityProxy = {
    target: identityUrl,
    changeOrigin: true,
    // dev certificate is self-signed
    secure: false,
  }

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/connect': identityProxy,
        '/api': identityProxy,
      },
    },
  }
})
