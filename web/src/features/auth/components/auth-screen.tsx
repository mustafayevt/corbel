import type { ReactNode } from 'react';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { env } from '@/config/env';
import { useDocumentTitle } from '@/hooks/use-document-title';

interface AuthScreenProps {
  title: string;
  description?: string;
  children: ReactNode;
  footer?: ReactNode;
}

/** Centered card chrome shared by every unauthenticated page (login, register, password flows). */
export function AuthScreen({ title, description, children, footer }: AuthScreenProps) {
  // The card title doubles as both the page's single <h1> and its document title.
  useDocumentTitle(title);

  return (
    <main className="flex min-h-svh items-center justify-center bg-muted/30 px-4 py-12">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <span className="mb-1 text-sm font-semibold tracking-tight text-primary">
            {env.appName}
          </span>
          <CardTitle as="h1">{title}</CardTitle>
          {description ? <CardDescription>{description}</CardDescription> : null}
        </CardHeader>
        <CardContent>{children}</CardContent>
        {footer ? (
          <CardFooter className="justify-center text-sm text-muted-foreground">{footer}</CardFooter>
        ) : null}
      </Card>
    </main>
  );
}
