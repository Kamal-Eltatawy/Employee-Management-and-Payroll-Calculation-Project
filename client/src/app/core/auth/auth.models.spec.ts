import { CurrentUser, homeUrlFor, isExpired } from './auth.models';

describe('auth models', () => {
  const now = new Date('2026-03-15T09:00:00Z');
  const user: CurrentUser = {
    email: 'ali@demo.local',
    role: 'Employee',
    employeeId: 1,
    accessToken: 'token',
    expiresAtUtc: '2026-03-15T09:15:00Z',
  };

  it('sends signed-in users to the home page and others to login', () => {
    expect(homeUrlFor(null)).toBe('/login');
    expect(homeUrlFor(user)).toBe('/home');
  });

  it('knows when the access token has expired', () => {
    expect(isExpired(user, now)).toBe(false);
    expect(isExpired({ ...user, expiresAtUtc: '2026-03-15T08:59:59Z' }, now)).toBe(true);
  });
});
