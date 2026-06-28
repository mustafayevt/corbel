import type { AuthUser } from '@/features/auth/stores/auth-store';

function base64UrlDecode(value: string): string {
  const padded = value
    .replace(/-/g, '+')
    .replace(/_/g, '/')
    .padEnd(Math.ceil(value.length / 4) * 4, '=');
  const binary = atob(padded);
  // Reconstruct UTF-8 from the binary string so non-ASCII display names survive.
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function pickClaim(claims: Record<string, unknown>, keys: readonly string[]): string | undefined {
  for (const key of keys) {
    const value = claims[key];
    if (typeof value === 'string' && value.length > 0) {
      return value;
    }
  }
  return undefined;
}

/**
 * Best-effort decode of display info from a JWT for the navbar. Claims are NEVER used for authorization
 * (the API re-validates every request); this is purely cosmetic. The backend issues compact claim names
 * (`sub`/`email`/`name`) and pins `MapInboundClaims=false`, so those are the only keys we read.
 */
export function decodeUser(token: string): AuthUser | null {
  try {
    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }
    const claims = JSON.parse(base64UrlDecode(payload)) as Record<string, unknown>;
    const id = pickClaim(claims, ['sub']);
    const email = pickClaim(claims, ['email']);
    const displayName = pickClaim(claims, ['name']);
    if (!id && !email) {
      return null;
    }
    return { id, email: email ?? '', displayName: displayName ?? null };
  } catch {
    return null;
  }
}
