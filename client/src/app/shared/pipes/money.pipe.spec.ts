import { MoneyPipe } from './money.pipe';

describe('MoneyPipe', () => {
  const pipe = new MoneyPipe();

  it('formats with two decimals and thousands separators', () => {
    expect(pipe.transform(15950)).toBe('15,950.00');
  });

  it('rounds half away from zero, for display only', () => {
    expect(pipe.transform(6175.806451612903)).toBe('6,175.81');
    expect(pipe.transform(0.125)).toBe('0.13');
    expect(pipe.transform(-0.125)).toBe('-0.13');
  });

  it('keeps negative amounts visible', () => {
    expect(pipe.transform(-1470)).toBe('-1,470.00');
  });

  it('renders nothing for missing values', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });
});
