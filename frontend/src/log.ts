export interface LogEntry { ts: number; tag: string; msg: string; data?: unknown }
const BUF: LogEntry[] = [];
const MAX = 3000;

export function log(tag: string, msg: string, data?: unknown) {
  BUF.push({ ts: Date.now(), tag, msg, data });
  if (BUF.length > MAX) BUF.shift();
  // mirror to console so Playwright's page.on('console') captures it live
  console.info(`[ark:${tag}] ${msg}`, data ?? "");
}
export function getLogs(): LogEntry[] { return BUF.slice(); }
