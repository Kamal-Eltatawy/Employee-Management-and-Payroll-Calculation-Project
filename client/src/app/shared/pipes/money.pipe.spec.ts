import { formatMoney } from './money.pipe';

describe('formatMoney', () => {
  it('formats with two decimals and thousands separators', () => {
    expect(formatMoney(15950)).toBe('15,950.00');
  });

  it('rounds half away from zero, for display only', () => {
    expect(formatMoney(6175.806451612903)).toBe('6,175.81');
    expect(formatMoney(0.125)).toBe('0.13');
    expect(formatMoney(-0.125)).toBe('-0.13');
  });

  it('keeps negative amounts visible', () => {
    expect(formatMoney(-1470)).toBe('-1,470.00');
  });

  it('renders nothing for missing values', () => {
    expect(formatMoney(null)).toBe('');
    expect(formatMoney(undefined)).toBe('');
  });

  it('keeps Latin digits in Arabic', () => {
    expect(formatMoney(15950, 'ar')).toMatch(/^15.950.00$/);
  });
});
