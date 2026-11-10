import { formatMonth, monthNames } from './months';

describe('months', () => {
  it('formats a yyyy-MM value in the UI language with Latin digits', () => {
    expect(formatMonth('2026-11', 'en')).toBe('November 2026');
    expect(formatMonth('2026-11', 'ar')).toContain('2026');
    expect(formatMonth('2026-11', 'ar')).not.toContain('٢');
  });

  it('lists twelve month names', () => {
    expect(monthNames('en')[0]).toBe('January');
    expect(monthNames('ar')).toHaveLength(12);
  });
});
