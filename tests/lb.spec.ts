import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

// The leaderboard is backed by local SQLite (no cloud). Each test uses unique profile ids so the
// persistent db doesn't carry state between runs.
const uid = () => `t${Date.now()}_${Math.floor(Math.random() * 1e6)}`;
const hdr = (pid: string) => ({ "X-Profile-Id": pid });

test("clock endpoint exposes server-owned season/week/day", async ({ page }) => {
  const data = await (await page.request.get(`${API}/meta/clock`)).json();
  expect(typeof data.weekId).toBe("number");
  expect(typeof data.seasonId).toBe("number");
  expect(data.weekEndsAt).toBeTruthy();
});

test("a legit score enters the league and ranks the player", async ({ page }) => {
  const pid = uid();
  const r = await (await page.request.post(`${API}/lb/submit?board=trial&score=4200`, { headers: hdr(pid) })).json();
  expect(r.accepted).toBe(true);

  const lg = await (await page.request.get(`${API}/lb/league?board=trial`, { headers: hdr(pid) })).json();
  expect(lg.cohortSize).toBe(30);
  const me = lg.entries.find((e: any) => e.isMe);
  expect(me.score).toBe(4200);
  expect(me.rank).toBe(1); // 4200 beats the Wood-tier bots
});

test("impossible scores are silently shadow-banned and invisible to others", async ({ page }) => {
  const cheater = uid();
  const honest = uid();
  const impossible = 99_999_999;

  // Cheater submits impossible scores twice → crosses the shadow threshold. Call still looks successful.
  const c1 = await (await page.request.post(`${API}/lb/submit?board=trial&score=${impossible}`, { headers: hdr(cheater) })).json();
  expect(c1.ok).toBe(true);
  expect(c1.accepted).toBe(false); // never enters the public board
  await page.request.post(`${API}/lb/submit?board=trial&score=${impossible}`, { headers: hdr(cheater) });

  await page.request.post(`${API}/lb/submit?board=trial&score=5000`, { headers: hdr(honest) });

  // The honest player's league must NOT contain the cheater.
  const lg = await (await page.request.get(`${API}/lb/league?board=trial`, { headers: hdr(honest) })).json();
  expect(lg.entries.some((e: any) => e.playerId === cheater)).toBe(false);

  // The cheater still sees their own row (no signal they were caught).
  const own = await (await page.request.get(`${API}/lb/league?board=trial`, { headers: hdr(cheater) })).json();
  expect(own.entries.some((e: any) => e.isMe)).toBe(true);
});
