import { zodResolver } from '@hookform/resolvers/zod';
import type { UseFormSetError } from 'react-hook-form';
import { useForm } from 'react-hook-form';
import { Alert } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import { Textarea } from '@/components/ui/textarea';
import { type NoteFormValues, noteSchema } from '@/features/notes/types/note-schemas';

interface NoteFormProps {
  defaultValues: NoteFormValues;
  submitLabel: string;
  pending: boolean;
  formError?: string | null;
  /** Receives validated values plus `setError` so the page can map server-side field errors back. */
  onSubmit: (values: NoteFormValues, setError: UseFormSetError<NoteFormValues>) => void;
  onCancel: () => void;
}

export function NoteForm({
  defaultValues,
  submitLabel,
  pending,
  formError,
  onSubmit,
  onCancel,
}: NoteFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<NoteFormValues>({
    resolver: zodResolver(noteSchema),
    defaultValues,
  });

  const submit = handleSubmit((values) => onSubmit(values, setError));

  return (
    <form onSubmit={submit} noValidate className="space-y-5">
      {formError ? <Alert variant="destructive">{formError}</Alert> : null}

      <Field id="title" label="Title" error={errors.title?.message}>
        <Input autoComplete="off" {...register('title')} />
      </Field>

      <Field id="content" label="Content" error={errors.content?.message}>
        <Textarea rows={12} {...register('content')} />
      </Field>

      <div className="flex items-center justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onCancel} disabled={pending}>
          Cancel
        </Button>
        <Button type="submit" disabled={pending} aria-busy={pending}>
          {pending ? <Spinner /> : null}
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
