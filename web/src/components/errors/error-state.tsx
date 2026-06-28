import type { ReactNode } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { env } from '@/config/env';

interface ErrorStateProps {
  title: string;
  description?: string;
  /** Optional HTTP-style code rendered large above the title (e.g. 404). */
  status?: number;
  /** Optional ProblemDetails trace id, shown small/muted so a user can quote it to support. */
  traceId?: string;
  /** Recovery actions (buttons/links) rendered below the message. */
  children?: ReactNode;
}

/**
 * Full-page, branded fallback shared by the route error boundary, the 404 page, and the top-level
 * bootstrap boundary. Presentational only — no router or query hooks — so it is safe to render even when
 * one of those providers is the thing that failed.
 */
export function ErrorState({ title, description, status, traceId, children }: ErrorStateProps) {
  return (
    <main className="flex min-h-svh items-center justify-center bg-muted/30 px-4 py-12">
      <Card className="w-full max-w-md text-center">
        <CardHeader className="items-center">
          <span className="mb-1 text-sm font-semibold tracking-tight text-primary">
            {env.appName}
          </span>
          {status ? (
            <p className="text-5xl font-bold tracking-tight text-muted-foreground">{status}</p>
          ) : null}
          <CardTitle as="h1" className="text-xl">
            {title}
          </CardTitle>
          {description ? <CardDescription>{description}</CardDescription> : null}
        </CardHeader>
        {children ? (
          <CardContent className="flex flex-col items-center gap-2">{children}</CardContent>
        ) : null}
        {traceId ? (
          <CardContent>
            <p className="text-xs text-muted-foreground">
              Reference: <span className="font-mono">{traceId}</span>
            </p>
          </CardContent>
        ) : null}
      </Card>
    </main>
  );
}
