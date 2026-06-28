import { Link } from 'react-router-dom';
import { Button, buttonVariants } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { paths } from '@/config/paths';
import { useArchiveNote } from '@/features/notes/api/archive-note';
import { useDeleteNote } from '@/features/notes/api/delete-note';
import { usePrefetchNote } from '@/features/notes/api/get-note';
import { useConfirm } from '@/lib/confirm';
import { formatDateTime } from '@/lib/format';
import type { Note } from '@/types/api';

interface NoteCardProps {
  note: Note;
}

export function NoteCard({ note }: NoteCardProps) {
  const archive = useArchiveNote();
  const remove = useDeleteNote();
  const confirm = useConfirm();
  const prefetchNote = usePrefetchNote();

  // Each card owns its mutations, so `busy` is scoped to this card (no shared pendingId race).
  const busy = archive.isPending || remove.isPending;
  const href = paths.notes.detail.getHref(note.id);

  // Warm the editor's query on intent so the click feels instant.
  const warm = {
    onMouseEnter: () => prefetchNote(note.id),
    onFocus: () => prefetchNote(note.id),
  };

  const handleArchive = async () => {
    if (await confirm({ title: `Archive "${note.title}"?`, confirmLabel: 'Archive' })) {
      archive.mutate({ params: { path: { id: note.id } } });
    }
  };

  const handleDelete = async () => {
    if (
      await confirm({
        title: `Delete "${note.title}"?`,
        description: "This can't be undone.",
        confirmLabel: 'Delete',
        destructive: true,
      })
    ) {
      remove.mutate({ params: { path: { id: note.id } } });
    }
  };

  return (
    <Card className="flex flex-col">
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <CardTitle as="h2" className="truncate">
            <Link to={href} className="hover:underline" {...warm}>
              {note.title}
            </Link>
          </CardTitle>
          {note.isArchived ? (
            <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
              Archived
            </span>
          ) : null}
        </div>
        <time dateTime={note.createdAtUtc} className="text-xs text-muted-foreground">
          {formatDateTime(note.createdAtUtc)}
        </time>
      </CardHeader>

      <CardContent className="flex-1">
        <p className="line-clamp-3 whitespace-pre-wrap text-sm text-muted-foreground">
          {note.content || <span className="italic">No content</span>}
        </p>
      </CardContent>

      <CardFooter className="justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={handleDelete} disabled={busy}>
          Delete
        </Button>
        {note.isArchived ? null : (
          <Button variant="outline" size="sm" onClick={handleArchive} disabled={busy}>
            Archive
          </Button>
        )}
        <Link to={href} className={buttonVariants({ size: 'sm' })} {...warm}>
          Edit
        </Link>
      </CardFooter>
    </Card>
  );
}
