const CACHE = "arkanoid-v1";

// On install: cache the shell.
self.addEventListener("install", (e) => {
  e.waitUntil(
    caches.open(CACHE).then((cache) =>
      cache.addAll(["/", "/index.html"])
      // Don't precache _framework files here — they're large and auto-fetched on first game load.
    )
  );
  self.skipWaiting();
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))
    )
  );
  self.clients.claim();
});

self.addEventListener("fetch", (e) => {
  // Cache-first for _framework (WASM files) — they don't change between reloads.
  if (e.request.url.includes("/_framework/")) {
    e.respondWith(
      caches.open(CACHE).then((cache) =>
        cache.match(e.request).then(
          (cached) => cached ?? fetch(e.request).then((res) => { cache.put(e.request, res.clone()); return res; })
        )
      )
    );
    return;
  }
  // Network-first for everything else (index.html, assets).
  e.respondWith(fetch(e.request).catch(() => caches.match(e.request)));
});
