import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { Alert } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { type RegisterValues, registerSchema } from '@/features/auth/types/auth-schemas';
import { applyProblemDetails, getErrorMessage } from '@/lib/problem';
import { $api } from '@/lib/react-query';
import { AuthScreen } from './auth-screen';

export function RegisterPage() {
  const navigate = useNavigate();
  const accessToken = useAuthStore((state) => state.accessToken);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { email: '', displayName: '', password: '', confirmPassword: '' },
  });

  // Inline field/form errors → suppress the global MutationCache toast.
  const signUp = $api.useMutation('post', '/api/auth/register', { meta: { errorMessage: false } });

  const onSubmit = handleSubmit((values) => {
    setFormError(null);
    const displayName = values.displayName?.trim();
    signUp.mutate(
      {
        body: { email: values.email, password: values.password, displayName: displayName || null },
      },
      {
        onSuccess: () => {
          // Email confirmation may be required, so we don't assume an auto-login token. Send the user
          // to sign-in with a confirmation message rather than guessing at the session state.
          navigate('/login', {
            replace: true,
            state: { notice: 'Account created. Please sign in.' },
          });
        },
        onError: (error) => {
          const hadFieldErrors = applyProblemDetails(error, setError, [
            'email',
            'password',
            'displayName',
          ]);
          if (!hadFieldErrors) {
            setFormError(getErrorMessage(error, 'Could not create your account.'));
          }
        },
      },
    );
  });

  // An already-authenticated user has no business on the register form — send them into the app.
  if (accessToken) {
    return <Navigate to="/" replace />;
  }

  return (
    <AuthScreen
      title="Create your account"
      description="Get started — it only takes a moment."
      footer={
        <span>
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-primary hover:underline">
            Sign in
          </Link>
        </span>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {formError ? <Alert variant="destructive">{formError}</Alert> : null}

        <Field
          id="displayName"
          label="Display name"
          error={errors.displayName?.message}
          hint="Optional"
        >
          <Input autoComplete="name" {...register('displayName')} />
        </Field>

        <Field id="email" label="Email" error={errors.email?.message}>
          <Input type="email" autoComplete="email" {...register('email')} />
        </Field>

        <Field id="password" label="Password" error={errors.password?.message}>
          <Input type="password" autoComplete="new-password" {...register('password')} />
        </Field>

        <Field
          id="confirmPassword"
          label="Confirm password"
          error={errors.confirmPassword?.message}
        >
          <Input type="password" autoComplete="new-password" {...register('confirmPassword')} />
        </Field>

        <Button
          type="submit"
          className="w-full"
          disabled={signUp.isPending}
          aria-busy={signUp.isPending}
        >
          {signUp.isPending ? <Spinner /> : null}
          Create account
        </Button>
      </form>
    </AuthScreen>
  );
}
