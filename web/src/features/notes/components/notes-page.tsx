import { useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Alert } from '@/components/ui/alert';
import { Button, buttonVariants } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { paths } from '@/config/paths';
import { useNotes } from '@/features/notes/api/get-notes';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { getErrorMessage } from '@/lib/problem';
import { cn } from '@/lib/utils';
import { NoteCard } from './note-card';

const PAGE_SIZE = 9;

/** Encode the page into ?page=, omitting it for page 1 so the first page has a clean URL. */
function writePageParam(params: URLSearchParams, page: number): URLSearchParams {
  if (page <= 1) {
    params.delete('page');
  } else {
    params.set('page', String(page));
  }
  return params;
}

export function NotesPage() {
  // Pagination lives in the URL (?page=) so a list view is shareable, bookmarkable, and survives reload.
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  // { replace: true } so paging doesn't spam the back-button history.
  const setPage = (next: number) =>
    setSearchParams((prev) => writePageParam(prev, next), { replace: true });
  useDocumentTitle('Your notes');

  const notesQuery = useNotes({ page, pageSize: PAGE_SIZE });
  const data = notesQuery.data;

  // After a delete empties the current page (e.g. removing the last note on the last page), step back to
  // the last page that still has notes instead of stranding the user on an empty "No notes yet" screen.
  // setSearchParams (stable from the router) is used directly so this isn't a useState-in-effect cascade.
  useEffect(() => {
    if (data && data.totalCount > 0 && page > data.totalPages) {
      const last = Math.max(data.totalPages, 1);
      setSearchParams((prev) => writePageParam(prev, last), { replace: true });
    }
  }, [data, page, setSearchParams]);

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-8">
      <header className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Your notes</h1>
          <p className="text-sm text-muted-foreground">
            {data ? `${data.totalCount} note${data.totalCount === 1 ? '' : 's'}` : ' '}
          </p>
        </div>
        <Link to={paths.notes.new.getHref()} className={buttonVariants()}>
          New note
        </Link>
      </header>

      {notesQuery.isLoading ? (
        <div className="flex justify-center py-24">
          <Spinner className="size-8 text-muted-foreground" label="Loading notes" />
        </div>
      ) : notesQuery.isError ? (
        <Alert variant="destructive" className="flex items-center justify-between gap-4">
          <span>{getErrorMessage(notesQuery.error, 'Could not load your notes.')}</span>
          <Button variant="outline" size="sm" onClick={() => notesQuery.refetch()}>
            Retry
          </Button>
        </Alert>
      ) : data && data.totalCount === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-16 text-center">
            <p className="text-lg font-medium">No notes yet</p>
            <p className="max-w-sm text-sm text-muted-foreground">
              Create your first note to see it here. Everything you write stays private to your
              account.
            </p>
            <Link to={paths.notes.new.getHref()} className={cn(buttonVariants(), 'mt-2')}>
              Create a note
            </Link>
          </CardContent>
        </Card>
      ) : data && data.items.length > 0 ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {data.items.map((note) => (
              <NoteCard key={note.id} note={note} />
            ))}
          </div>

          <nav className="mt-6 flex items-center justify-between" aria-label="Pagination">
            <p className="text-sm text-muted-foreground" aria-live="polite">
              Page {data.page} of {Math.max(data.totalPages, 1)}
              {notesQuery.isFetching ? <span className="ml-2">Updating…</span> : null}
            </p>
            <div className="flex gap-2">
              {/* Gate on the boundary flags only, not isFetching: keepPreviousData holds the current page visible
                  during a fetch, so the just-clicked button stays enabled and keyboard focus is never dropped. */}
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(Math.max(page - 1, 1))}
                disabled={!data.hasPrevious}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(page + 1)}
                disabled={!data.hasNext}
              >
                Next
              </Button>
            </div>
          </nav>
        </>
      ) : (
        // Empty page after a delete on a later page: the clamp effect above is about to step us back, so
        // hold a spinner instead of flashing the "No notes yet" state for an account that still has notes.
        <div className="flex justify-center py-24">
          <Spinner className="size-8 text-muted-foreground" label="Loading notes" />
        </div>
      )}
    </div>
  );
}
