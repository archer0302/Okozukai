# Okozukai Frontend

Vue 3 + TypeScript + Vite SPA for the [Okozukai](../../README.md) personal budget tracker.

## Development

```bash
# Install dependencies
npm install

# Start dev server (normally launched via .NET Aspire)
npm run dev

# Run component tests (Vitest)
npm test

# Run E2E tests (Playwright — requires the full app to be running)
npm run test:e2e

# Production build
npm run build
```

The `VITE_API_URL` environment variable is injected by the Aspire AppHost at dev time.
For standalone development, create a `.env.local` file:

```
VITE_API_URL=http://localhost:5005
```
