import { Link } from 'react-router-dom';
import { buttonVariants } from '@/components/ui/button';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { ErrorState } from './error-state';

/** Branded 404 for any unmatched URL — the router's catch-all. */
export function NotFound() {
  useDocumentTitle('Page not found');

  return (
    <ErrorState
      status={404}
      title="Page not found"
      description="The page you're looking for doesn't exist or may have moved."
    >
      <Link to="/" className={buttonVariants()}>
        Back to home
      </Link>
    </ErrorState>
  );
}
