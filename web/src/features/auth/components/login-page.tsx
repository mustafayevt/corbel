import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import { decodeUser } from '@/features/auth/api/decode-user';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { type LoginValues, loginSchema } from '@/features/auth/types/auth-schemas';
import { monitoring } from '@/lib/monitoring';
import { applyProblemDetails, asProblem, getErrorMessage } from '@/lib/problem';
import { $api } from '@/lib/react-query';
import { ErrorCode } from '@/types/api';
import { AuthScreen } from './auth-screen';

/** True if the string contains any C0/C7F control character (which could smuggle a different target). */
function hasControlChar(value: string): boolean {
  for (let i = 0; i < value.length; i += 1) {
    const code = value.charCodeAt(i);
    if (code < 0x20 || code === 0x7f) {
      return true;
    }
  }
  return false;
}

/**
 * Only allow same-origin, single-leading-slash redirects to avoid an open-redirect via `returnTo`.
 * Rejects protocol-relative (`//host`), backslash variants (some browsers treat `\` as `/`), and any
 * control characters that could smuggle a different target past the check.
 */
function safeReturnTo(value: string | null): string {
  if (!value) {
    return '/';
  }
  if (
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    hasControlChar(value)
  ) {
    return '/';
  }
  return value;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const returnTo = safeReturnTo(params.get('returnTo'));
  const notice = (location.state as { notice?: string } | null)?.notice ?? null;
  const accessToken = useAuthStore((state) => state.accessToken);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  // meta.errorMessage:false: this form surfaces failures inline (field + form errors), so suppress the
  // global MutationCache toast to avoid double-messaging.
  const login = $api.useMutation('post', '/api/auth/login', { meta: { errorMessage: false } });

  const onSubmit = handleSubmit((values) => {
    setFormError(null);
    login.mutate(
      // Browser SPA → cookie mode: the refresh token is set as an httpOnly cookie, not returned in the body.
      { body: { ...values, useCookies: true } },
      {
        onSuccess: (data) => {
          const user = decodeUser(data.accessToken) ?? { email: values.email };
          useAuthStore.getState().setSession(data.accessToken, user);
          monitoring.setUser({ id: user.id, email: user.email });
          navigate(returnTo, { replace: true });
        },
        onError: (error) => {
          const hadFieldErrors = applyProblemDetails(error, setError, ['email', 'password']);
          if (!hadFieldErrors) {
            // Tailor the locked-account case the API signals with a stable error code; everything else falls
            // back to the generic credentials message (which never reveals whether the email exists).
            setFormError(
              asProblem(error)?.errorCode === ErrorCode.AccountLocked
                ? 'Your account is temporarily locked after too many attempts. Please try again later.'
                : getErrorMessage(error, 'Invalid email or password.'),
            );
          }
        },
      },
    );
  });

  // Already signed in (e.g. the on-boot silent refresh restored a session): bounce to the app instead of
  // showing the form. Placed after all hooks so hook order stays stable.
  if (accessToken) {
    return <Navigate to={returnTo} replace />;
  }

  return (
    <AuthScreen
      title="Sign in"
      description="Welcome back. Enter your credentials to continue."
      footer={
        <span>
          New here?{' '}
          <Link to="/register" className="font-medium text-primary hover:underline">
            Create an account
          </Link>
        </span>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {notice ? <Alert variant="success">{notice}</Alert> : null}
        {formError ? <Alert variant="destructive">{formError}</Alert> : null}

        <Field id="email" label="Email" error={errors.email?.message}>
          <Input type="email" autoComplete="email" {...register('email')} />
        </Field>

        <Field id="password" label="Password" error={errors.password?.message}>
          <Input type="password" autoComplete="current-password" {...register('password')} />
        </Field>

        <Button
          type="submit"
          className="w-full"
          disabled={login.isPending}
          aria-busy={login.isPending}
        >
          {login.isPending ? <Spinner /> : null}
          Sign in
        </Button>
      </form>
    </AuthScreen>
  );
}
