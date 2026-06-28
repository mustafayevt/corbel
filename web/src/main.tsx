import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { monitoring } from '@/lib/monitoring';
import { App } from './app/app';
import './styles/index.css';

// Initialize error tracking (no-op unless VITE_SENTRY_DSN is set) before the app mounts.
void monitoring.init();

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root element #root not found in index.html');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
