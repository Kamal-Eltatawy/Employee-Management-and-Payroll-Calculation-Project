export type Role = 'Hr' | 'Employee';

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  role: Role;
  employeeId: number | null;
  email: string;
}

export type CurrentUser = LoginResponse;

/** Where users land after login or on '/'. */
export function homeUrlFor(user: CurrentUser | null): string {
  return user ? '/home' : '/login';
}

export function isExpired(user: CurrentUser, now: Date = new Date()): boolean {
  return Date.parse(user.expiresAtUtc) <= now.getTime();
}
