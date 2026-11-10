export type Role = 'Hr' | 'Employee';

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: Role;
  employeeId: number | null;
}

export interface CurrentUser {
  email: string;
  role: Role;
  employeeId: number | null;
  accessToken: string;
  expiresAtUtc: string;
}

/** Where each role lands after login or on '/'. */
export function homeUrlFor(user: CurrentUser | null): string {
  if (!user) {
    return '/login';
  }

  return user.role === 'Hr' ? '/employees' : '/my-payslip';
}

export function isExpired(user: CurrentUser, now: Date = new Date()): boolean {
  return Date.parse(user.expiresAtUtc) <= now.getTime();
}

/**
 * Restores a session from storage; anything malformed is treated as signed out.
 * An expired access token is kept: the HttpOnly refresh cookie can still renew it (D55).
 */
export function parseStoredUser(raw: string | null): CurrentUser | null {
  if (!raw) {
    return null;
  }

  try {
    const value = JSON.parse(raw) as Partial<CurrentUser>;
    const valid =
      typeof value.accessToken === 'string' &&
      typeof value.email === 'string' &&
      typeof value.expiresAtUtc === 'string' &&
      (value.role === 'Hr' || value.role === 'Employee');
    if (!valid) {
      return null;
    }

    return value as CurrentUser;
  } catch {
    return null;
  }
}
