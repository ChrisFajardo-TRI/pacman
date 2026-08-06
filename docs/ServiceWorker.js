const cacheName = "pacman-20260806005949";
const contentToCache = [
    "Build/WebGL.loader.js",
    "Build/WebGL.framework.js",
    "Build/WebGL.data",
    "Build/WebGL.wasm",
    "TemplateData/style.css"

];

self.addEventListener('install', function (e) {
    console.log('[Service Worker] Install');
    
    e.waitUntil((async function () {
      const cache = await caches.open(cacheName);
      console.log('[Service Worker] Caching all: app shell and content');
      await cache.addAll(contentToCache);
    })());
});

self.addEventListener('fetch', function (e) {
    e.respondWith((async function () {
      let response = await caches.match(e.request);
      console.log(`[Service Worker] Fetching resource: ${e.request.url}`);
      if (response) { return response; }

      response = await fetch(e.request);
      const cache = await caches.open(cacheName);
      console.log(`[Service Worker] Caching new resource: ${e.request.url}`);
      cache.put(e.request, response.clone());
      return response;
    })());
});

// Appended post-build: take over immediately and drop caches from older builds,
// otherwise the cache-first fetch handler above serves stale content forever.
self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) {
    e.waitUntil((async function () {
        const keys = await caches.keys();
        await Promise.all(keys.filter(k => k !== cacheName).map(k => caches.delete(k)));
        await self.clients.claim();
    })());
});
