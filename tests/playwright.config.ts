import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  timeout: 30_000,
  // PixiJS/WebGL contexts are GPU-limited; running specs in parallel exhausts
  // contexts and causes "context lost" flakiness. Serialize for reliable integration runs.
  fullyParallel: false,
  workers: 1,
  retries: 1,
  use: {
    baseURL: "http://localhost:5175",
    headless: true,
    trace: "retain-on-failure",
    // Mobile-first viewport: iPhone 13 dimensions, touch enabled.
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
    deviceScaleFactor: 3,
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
