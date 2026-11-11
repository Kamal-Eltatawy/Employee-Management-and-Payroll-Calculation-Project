import { CurrentUser, canSeeOwnPayslip, hasRole, homeUrlFor, isExpired, toRoles } from './auth.models';

describe('auth models', () => {
  const now = new Date('2026-03-15T09:00:00Z');
  const user: CurrentUser = {
    email: 'ali@demo.local',
    roles: ['Employee'],
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

  it('reads a role claim given as a string or as an array, dropping unknown roles', () => {
    expect(toRoles('Hr')).toEqual(['Hr']);
    expect(toRoles(['Employee', 'Hr', 'Admin'])).toEqual(['Hr', 'Employee']);
    expect(toRoles(undefined)).toEqual([]);
  });

  it('checks roles with includes', () => {
    const both: CurrentUser = { ...user, roles: ['Hr', 'Employee'] };
    expect(hasRole(both, 'Hr')).toBe(true);
    expect(hasRole(both, 'Employee')).toBe(true);
    expect(hasRole(user, 'Hr')).toBe(false);
    expect(hasRole(null, 'Hr')).toBe(false);
  });

  it('shows "My payslip" to employees, and to Hr + Employee users only with an employee record', () => {
    expect(canSeeOwnPayslip(user)).toBe(true);
    expect(canSeeOwnPayslip({ ...user, employeeId: null })).toBe(true);
    expect(canSeeOwnPayslip({ ...user, roles: ['Hr', 'Employee'] })).toBe(true);
    expect(canSeeOwnPayslip({ ...user, roles: ['Hr', 'Employee'], employeeId: null })).toBe(false);
    expect(canSeeOwnPayslip({ ...user, roles: ['Hr'] })).toBe(false);
  });
});
