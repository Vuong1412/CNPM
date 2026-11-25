export function formatDate(value: string, locale = "vi-VN"): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(
    new Date(value)
  );
}
