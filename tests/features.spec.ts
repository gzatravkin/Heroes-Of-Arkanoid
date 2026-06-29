import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("locked features are gated in the menu with a clear requirement", async ({ page }) => {
  await page.request.post(`${API}/reset`); // fresh: only hell-1 unlocked, nothing completed
  await page.goto("/?scene=menu");
  await page.waitForSelector("#btn-cards", { timeout: 10000 });

  // Daily is always available; Cards/Modules/League/Season are locked at the start.
  await expect(page.locator("#btn-daily")).toHaveAttribute("data-locked", "false");
  await expect(page.locator("#btn-cards")).toHaveAttribute("data-locked", "true");
  await expect(page.locator("#btn-modules")).toHaveAttribute("data-locked", "true");
  await expect(page.locator("#btn-league")).toHaveAttribute("data-locked", "true");

  // Clicking a locked feature shows the requirement toast and does NOT navigate.
  await page.locator("#btn-cards").click();
  await expect(page.locator(".menu-toast")).toContainText("unlocks after", { timeout: 4000 });
  expect(page.url()).toContain("scene=menu");
  await page.screenshot({ path: "menu-locked.png" });
});

test("clearing the gate level unlocks the feature", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.request.post(`${API}/complete?level=hell-2&rift=none`); // Cards gate = hell-2
  await page.goto("/?scene=menu");
  await page.waitForSelector("#btn-cards", { timeout: 10000 });

  await expect(page.locator("#btn-cards")).toHaveAttribute("data-locked", "false");
  await page.locator("#btn-cards").click();
  await page.waitForURL("**/?scene=cards*", { timeout: 8000 });
});
