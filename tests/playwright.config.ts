import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  // 45s headroom for tests with 1-2 remaining page.evaluate() stalls.
  timeout: 45_000,
  globalSetup: "./global-setup.ts",
  fullyParallel: true,
  // 2 workers instead of 4: halves Chromium instances, halves backend sessions,
  // and cuts GPU stall duration from 12-15s (4-way) down to ~6-8s (2-way).
  workers: 2,
  retries: 1,
  use: {
    baseURL: "http://localhost:5175",
    headless: true,
    trace: "retain-on-failure",
    // Mobile-first viewport: iPhone 13 dimensions, touch enabled.
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
    // DPR 1: 390×844 physical pixels instead of 780×1688 at DPR 2 — 4× less
    // GPU fill per frame, significantly reducing GPU load during parallel runs.
    deviceScaleFactor: 1,
  },
  webServer: [
    {
      command: "dotnet run --project ../backend/Arkanoid.Server",
      url: "http://localhost:5080/",
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: "npm run dev",
      cwd: "../frontend",
      url: "http://localhost:5175",
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
});
