import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
//
// Free-tier friendly build: keep page-level React.lazy as-is, and split heavy
// vendor libs into stable named chunks so:
// - Login/Home do not download recharts / leaflet / chat-ui
// - Browser can cache vendors across app deploys when only app code changes
// Behavior and UI are unchanged — only network payload shape.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    include: ['src/**/*.{test,spec}.{js,jsx}'],
    setupFiles: ['./src/test/setup.js'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      // Gate pure utils only — api.js / corporateService.js have large untested surfaces;
      // their critical paths are still unit-tested, but full-file floors come later.
      // RTL covers AuthContext / Login / CompanySwitcher separately (not full-file gated yet).
      include: [
        'src/utils/errorHandler.js',
        'src/utils/authEvents.js',
        'src/utils/indianStatesCities.js',
        'src/utils/notificationHelpers.js',
        'src/utils/formatCurrency.js',
        'src/utils/safeReturnUrl.js',
      ],
      thresholds: {
        lines: 90,
        functions: 90,
        branches: 70,
        statements: 90,
      },
    },
  },
  build: {
    // Slightly higher than Vite default so named vendor chunks do not warn noisily
    chunkSizeWarningLimit: 700,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return

          // Charts — only needed on dashboard pages
          if (
            id.includes('recharts') ||
            id.includes('d3-') ||
            id.includes('victory-vendor')
          ) {
            return 'vendor-recharts'
          }

          // Maps — only needed on Search / Details / listing forms
          if (
            id.includes('leaflet') ||
            id.includes('react-leaflet')
          ) {
            return 'vendor-leaflet'
          }

          // Chat UI kit — only needed on /chat page
          if (id.includes('@chatscope')) {
            return 'vendor-chat-ui'
          }

          // Realtime client — used by Auth shell when logged in, but cacheable alone
          if (id.includes('@microsoft/signalr')) {
            return 'vendor-signalr'
          }

          // React core — shared, long-lived cache
          if (
            id.includes('node_modules/react-dom') ||
            id.includes('node_modules/react-router') ||
            id.includes('node_modules/react/') ||
            id.includes('node_modules\\react\\') ||
            id.includes('scheduler')
          ) {
            return 'vendor-react'
          }
        },
      },
    },
  },
})
