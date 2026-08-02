/**
 * Helpers for booking extension date/price windows.
 * Day-based pricing (Daily/Weekly/Monthly) bills inclusive calendar days;
 * extension quotes must start on the next day after the already-paid end day.
 *
 * Important: browser-local midnight converted to ISO can still fall on the previous
 * UTC calendar day (e.g. IST Aug 5 00:00 → Aug 4 18:30Z). Backend inclusive-day
 * math with TimeZoneId null/UTC then counts 2 days for a 1-day extension.
 * Day-based price quotes therefore use noon-UTC anchors on the intended local
 * calendar dates so duration is stable across UTC and Asia/Kolkata facility TZs.
 */

/** PricingType: 0=Hourly, 1=Daily, 2=Weekly, 3=Monthly */
export const isDayBasedPricing = (pricingType) => Number(pricingType) > 0;

export const toDateOnly = (value) => {
  if (!value) return '';
  if (value instanceof Date) {
    const pad = (n) => String(n).padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
  }
  // Prefer parsing ISO timestamps via Date so local calendar day is used (not UTC slice).
  const asDate = new Date(value);
  if (!Number.isNaN(asDate.getTime()) && (String(value).includes('T') || String(value).includes('Z'))) {
    const pad = (n) => String(n).padStart(2, '0');
    return `${asDate.getFullYear()}-${pad(asDate.getMonth() + 1)}-${pad(asDate.getDate())}`;
  }
  return String(value).slice(0, 10);
};

/** YYYY-MM-DD → noon UTC ISO (stable inclusive day span in any common facility TZ). */
export const dateOnlyToNoonUtcIso = (dateOnly) => {
  if (!dateOnly || !/^\d{4}-\d{2}-\d{2}$/.test(dateOnly)) return null;
  const [y, m, d] = dateOnly.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d, 12, 0, 0, 0)).toISOString();
};

/** First calendar day after the booking end (local), as YYYY-MM-DD. */
export const firstExtensionEndDateOnly = (currentEnd) => {
  const end = new Date(currentEnd);
  const d = new Date(end.getFullYear(), end.getMonth(), end.getDate() + 1);
  return toDateOnly(d);
};

/**
 * Billable start of the extension window for price quotes.
 * Hourly: continuous from booking end.
 * Day-based: first unpaid local calendar day (noon UTC anchor).
 */
export const extensionPricingStartIso = (currentEnd, pricingType) => {
  const end = new Date(currentEnd);
  if (Number.isNaN(end.getTime())) return null;
  if (isDayBasedPricing(pricingType)) {
    return dateOnlyToNoonUtcIso(firstExtensionEndDateOnly(currentEnd));
  }
  return end.toISOString();
};

/**
 * Billable end of the extension window for price quotes (not the stored booking end).
 * Day-based: selected local calendar day (noon UTC anchor) so day count matches user intent.
 * Hourly: exact datetime-local.
 */
export const extensionPricingEndIso = (newEndValue, pricingType) => {
  if (!newEndValue) return null;
  if (isDayBasedPricing(pricingType)) {
    return dateOnlyToNoonUtcIso(toDateOnly(newEndValue));
  }
  return new Date(newEndValue).toISOString();
};

/** Default new end based on pricing unit after the current booking end. */
export const defaultExtensionEnd = (currentEnd, pricingType) => {
  const end = new Date(currentEnd);
  const type = Number(pricingType);
  if (type === 1) {
    return new Date(end.getFullYear(), end.getMonth(), end.getDate() + 1, 23, 59, 0, 0);
  }
  if (type === 2) {
    return new Date(end.getFullYear(), end.getMonth(), end.getDate() + 7, 23, 59, 0, 0);
  }
  if (type === 3) {
    return new Date(end.getFullYear(), end.getMonth(), end.getDate() + 30, 23, 59, 0, 0);
  }
  return new Date(end.getTime() + 60 * 60 * 1000);
};

/**
 * Resolve extension end ISO for the booking record / request-extension API.
 * Day-based: end of selected calendar day (local); hourly: exact datetime-local.
 */
export const resolveExtensionEndIso = (newEndValue, pricingType) => {
  if (!newEndValue) return null;
  if (isDayBasedPricing(pricingType)) {
    const dateOnly = toDateOnly(newEndValue);
    return new Date(`${dateOnly}T23:59:59`).toISOString();
  }
  return new Date(newEndValue).toISOString();
};

/**
 * Inclusive local calendar days in a day-based extension quote window
 * (first unpaid day → selected end day).
 */
export const billableExtensionDays = (currentEnd, newEndValue) => {
  const startDay = firstExtensionEndDateOnly(currentEnd);
  const endDay = toDateOnly(newEndValue);
  if (!startDay || !endDay || endDay < startDay) return 0;
  const [sy, sm, sd] = startDay.split('-').map(Number);
  const [ey, em, ed] = endDay.split('-').map(Number);
  const ms =
    Date.UTC(ey, em - 1, ed) - Date.UTC(sy, sm - 1, sd);
  return Math.floor(ms / (24 * 60 * 60 * 1000)) + 1;
};

export const isValidExtensionDate = (bookingEndDateTime, newEnd, pricingType = 0) => {
  if (!bookingEndDateTime || !newEnd) return false;
  const currentEnd = new Date(bookingEndDateTime);
  if (Number.isNaN(currentEnd.getTime())) return false;

  if (isDayBasedPricing(pricingType)) {
    const proposedDay = toDateOnly(newEnd);
    const currentEndDay = toDateOnly(currentEnd);
    return !!proposedDay && proposedDay > currentEndDay;
  }

  const proposedIso = resolveExtensionEndIso(newEnd, pricingType);
  if (!proposedIso) return false;
  const proposedEnd = new Date(proposedIso);
  if (Number.isNaN(proposedEnd.getTime())) return false;
  return proposedEnd > currentEnd;
};
