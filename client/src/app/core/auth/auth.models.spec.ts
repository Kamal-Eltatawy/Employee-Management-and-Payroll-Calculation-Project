import { CurrentUser, homeUrlFor, isExpired, parseStoredUser } from './auth.models';

describe('auth models', () => {
  const now = new Date('2026-03-15T09:00:00Z');
  const user: CurrentUser = {
    email: 'ali@demo.local',
    role: 'Employee',
    employeeId: 1,
    accessToken: 'token',
    expiresAtUtc: '2026-03-15T10:00:00Z',
  };

  it('sends each role to its home page', () => {
    expect(homeUrlFor(null)).toBe('/login');
    expect(homeUrlFor(user)).toBe('/my-payslip');
    expect(homeUrlFor({ ...user, role: 'Hr', employeeId: null })).toBe('/employees');
  });

  it('restores a stored session, even with an expired access token (the refresh cookie can renew it)', () => {
    const expired = { ...user, expiresAtUtc: '2026-03-15T08:59:59Z' };

    expect(parseStoredUser(JSON.stringify(user))).toEqual(user);
    expect(parseStoredUser(JSON.stringify(expired))).toEqual(expired);
    expect(isExpired(expired, now)).toBe(true);
    expect(isExpired(user, now)).toBe(false);
  });

  it('drops a malformed or missing session', () => {
    expect(parseStoredUser(JSON.stringify({ ...user, role: 'Admin' }))).toBeNull();
    expect(parseStoredUser('{not json')).toBeNull();
    expect(parseStoredUser(null)).toBeNull();
  });
});
