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
  // Our own build outputs change every deploy (Core/Wasm assemblies get rebuilt for every
  // gameplay/balance change, blazor.boot.json's hashes change right along with them) — cache-first
  // here meant a returning player's browser would serve a stale WASM binary FOREVER, silently
  // ignoring every future deploy, since the request URL never changes even though the content does.
  // Stale-while-revalidate: serve the cached copy immediately (still fast), but always refetch in
  // the background and update the cache so the NEXT load picks up whatever just shipped.
  const isOwnBuildOutput = /\/_framework\/(Arkanoid\.(Core|Wasm)\.wasm|blazor\.boot\.json)$/.test(e.request.url);
  if (isOwnBuildOutput) {
    e.respondWith(
      caches.open(CACHE).then(async (cache) => {
        const cached = await cache.match(e.request);
        const network = fetch(e.request).then((res) => { cache.put(e.request, res.clone()); return res; });
        return cached ?? network;
      })
    );
    return;
  }
  // Cache-first for the rest of _framework (.NET runtime/ICU blobs) — genuinely pinned to the SDK
  // version, not something this repo's own deploys change.
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
