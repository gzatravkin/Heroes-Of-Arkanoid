import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  // 45s (was 30s): heavy multi-floor sim tests need headroom under 4-worker CPU contention.
  timeout: 45_000,
  globalSetup: "./global-setup.ts",
  // Parallel-safe: each worker uses its own backend profile namespace (see
  // helpers/fixtures.ts) so meta-state tests never clobber each other, and sim
  // state is already per-WebSocket. Capped at 4 workers to keep the machine
  // responsive and well under the WebGL-context ceiling.
  fullyParallel: true,
  workers: 4,
  retries: 1,
  use: {
    baseURL: "http://localhost:5175",
    headless: true,
    trace: "retain-on-failure",
    // Mobile-first viewport: iPhone 13 dimensions, touch enabled.
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
    // DPR 2 (not 3): still a mobile/retina backing store but ~2.25× less GPU
    // fill per frame, which cuts render cost and keeps parallel workers smooth.
    deviceScaleFactor: 2,
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
