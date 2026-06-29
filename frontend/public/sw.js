const CACHE = "arkanoid-v2";
const BASE = "/Heroes-Of-Arkanoid/";

self.addEventListener("install", (e) => {
  e.waitUntil(
    caches.open(CACHE).then((cache) =>
      cache.addAll([BASE, BASE + "index.html"])
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
  // Cache-first for _framework (WASM files) — large, never change between deploys.
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
