import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import type { UseFormSetError } from 'react-hook-form';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Alert } from '@/components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { useNote } from '@/features/notes/api/get-note';
import { notesKeys } from '@/features/notes/api/notes-keys';
import type { NoteFormValues } from '@/features/notes/types/note-schemas';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { applyProblemOrFormError, getErrorMessage } from '@/lib/problem';
import { $api } from '@/lib/react-query';
import { NoteForm } from './note-form';

export function NoteEditorPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { id } = useParams();
  const isEdit = Boolean(id);
  const [formError, setFormError] = useState<string | null>(null);

  const heading = isEdit ? 'Edit note' : 'New note';
  useDocumentTitle(heading);

  // Only fetch in edit mode; seeded from the list cache (get-note) so a click from the list paints instantly.
  const noteQuery = useNote(id, { enabled: isEdit });

  // Inline (not extracted hooks): a thin re-exported wrapper can't have its openapi-react-query return type
  // named under the composite build (TS2742). meta.errorMessage:false routes errors to the inline form.
  const create = $api.useMutation('post', '/api/notes', { meta: { errorMessage: false } });
  const update = $api.useMutation('put', '/api/notes/{id}', { meta: { errorMessage: false } });
  const pending = create.isPending || update.isPending;

  const goToList = () => navigate('/');

  const handleSubmit = (values: NoteFormValues, setError: UseFormSetError<NoteFormValues>) => {
    setFormError(null);

    const onError = (error: unknown) =>
      applyProblemOrFormError(
        error,
        setError,
        ['title', 'content'],
        setFormError,
        'Could not save the note.',
      );
    const onSuccess = async () => {
      await queryClient.invalidateQueries({ queryKey: notesKeys.list() });
      if (isEdit && id) {
        await queryClient.invalidateQueries({ queryKey: notesKeys.detail(id) });
      }
      navigate('/');
    };

    if (isEdit && id) {
      update.mutate({ params: { path: { id } }, body: values }, { onSuccess, onError });
    } else {
      create.mutate({ body: values }, { onSuccess, onError });
    }
  };

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-8">
      <Link to="/" className="text-sm text-muted-foreground hover:text-foreground">
        ← Back to notes
      </Link>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle as="h1">{heading}</CardTitle>
        </CardHeader>
        <CardContent>
          {isEdit && noteQuery.isLoading ? (
            <div className="flex justify-center py-12">
              <Spinner className="size-6 text-muted-foreground" label="Loading note" />
            </div>
          ) : isEdit && !noteQuery.data ? (
            // Only when there's nothing to show: a background revalidation error keeps the editable form
            // (with the user's in-progress edits) rather than swapping it for this alert.
            <Alert variant="destructive">
              {getErrorMessage(
                noteQuery.error,
                "That note doesn't exist or you don't have access to it.",
              )}
            </Alert>
          ) : (
            <NoteForm
              // Re-mount per note so react-hook-form re-seeds its defaultValues when navigating between two
              // already-cached editors (defaultValues are applied on mount only, never re-synced on prop change).
              key={id ?? 'new'}
              defaultValues={{
                title: noteQuery.data?.title ?? '',
                content: noteQuery.data?.content ?? '',
              }}
              submitLabel={isEdit ? 'Save changes' : 'Create note'}
              pending={pending}
              formError={formError}
              onSubmit={handleSubmit}
              onCancel={goToList}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
