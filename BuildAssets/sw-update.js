
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
