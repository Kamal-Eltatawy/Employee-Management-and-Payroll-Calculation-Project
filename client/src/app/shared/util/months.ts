/** Month names in the UI language with Latin digits (e.g. "نوفمبر 2026", "November 2026"). */
export function formatMonth(value: string, locale: string): string {
  const [year, month] = value.split('-').map(Number);
  if (!year || !month) {
    return value;
  }

  return new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric', numberingSystem: 'latn', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, 1)),
  );
}

/** The twelve month names in the UI language. */
export function monthNames(locale: string): string[] {
  const formatter = new Intl.DateTimeFormat(locale, { month: 'long', timeZone: 'UTC' });
  return Array.from({ length: 12 }, (_, index) => formatter.format(new Date(Date.UTC(2026, index, 1))));
}
