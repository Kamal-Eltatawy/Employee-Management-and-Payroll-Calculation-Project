/** Local date as yyyy-MM-dd (the browser's "today", D15). */
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** Local month as yyyy-MM. */
export function toIsoMonth(date: Date): string {
  return toIsoDate(date).slice(0, 7);
}
