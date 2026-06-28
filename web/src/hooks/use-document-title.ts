import { useEffect } from 'react';
import { env } from '@/config/env';

/**
 * Sets `document.title` to `"<page> · <app>"` (or just the app name when no page title is given) for the
 * lifetime of the calling route. Every routed page sets its own title, so client-side navigation always
 * reflects the page the user is actually on.
 */
export function useDocumentTitle(title?: string) {
  useEffect(() => {
    document.title = title ? `${title} · ${env.appName}` : env.appName;
  }, [title]);
}
