export type Role = 'Hr' | 'Employee';

const KNOWN_ROLES: readonly Role[] = ['Hr', 'Employee'];

/** The login/refresh body (D67: a user may have several roles). */
export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  roles: Role[];
  employeeId: number | null;
  email: string;
}

export type CurrentUser = LoginResponse;

/**
 * Normalizes a role claim or field: a single string or a JSON array of strings (D67).
 * Unknown roles are dropped.
 */
export function toRoles(value: unknown): Role[] {
  const items = Array.isArray(value) ? value : typeof value === 'string' ? [value] : [];
  return KNOWN_ROLES.filter((role) => items.includes(role));
}

export function hasRole(user: CurrentUser | null, role: Role): boolean {
  return !!user && user.roles.includes(role);
}

/**
 * The "My payslip" link: every Employee-only user (the page explains a missing employee record, item 11),
 * and an Hr + Employee user only when the account is linked to an employee.
 */
export function canSeeOwnPayslip(user: CurrentUser | null): boolean {
  if (!hasRole(user, 'Employee')) {
    return false;
  }

  return !hasRole(user, 'Hr') || user?.employeeId != null;
}

/** Where users land after login or on '/'. */
export function homeUrlFor(user: CurrentUser | null): string {
  return user ? '/home' : '/login';
}

export function isExpired(user: CurrentUser, now: Date = new Date()): boolean {
  return Date.parse(user.expiresAtUtc) <= now.getTime();
}
