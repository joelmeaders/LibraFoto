/**
 * LibraFoto Display Service Worker
 * Provides caching for media files only (photos/videos)
 * HTML, JS, and CSS are always fetched from network
 */

const MEDIA_CACHE = "librafoto-media-v3";

// Maximum cached media items
const MAX_MEDIA_CACHE_ITEMS = 100;

/**
 * Install event - skip waiting immediately
 */
self.addEventListener("install", (event) => {
  console.log("[SW] Installing service worker...");
  // Skip waiting to activate immediately
  event.waitUntil(self.skipWaiting());
});

/**
 * Activate event - clean up old caches and claim clients
 */
self.addEventListener("activate", (event) => {
  console.log("[SW] Activating service worker...");

  event.waitUntil(
    caches
      .keys()
      .then((cacheNames) => {
        return Promise.all(
          cacheNames
            .filter((name) => {
              // Delete all old caches except current media cache
              return name.startsWith("librafoto-") && name !== MEDIA_CACHE;
            })
            .map((name) => {
              console.log("[SW] Deleting old cache:", name);
              return caches.delete(name);
            })
        );
      })
      .then(() => {
        console.log("[SW] Service worker activated");
        return self.clients.claim();
      })
  );
});

/**
 * Fetch event - only cache media, everything else goes to network
 */
self.addEventListener("fetch", (event) => {
  const url = new URL(event.request.url);

  // Only cache media requests (photos/videos)
  if (
    url.pathname.includes("/media/photos/") ||
    url.pathname.includes("/media/thumbnails/")
  ) {
    event.respondWith(cacheFirstMedia(event.request));
    return;
  }

  // Everything else (HTML, JS, CSS, API) goes directly to network
  // Do not intercept - let browser handle normally
});

/**
 * Cache-first strategy for media with LRU eviction
 */
async function cacheFirstMedia(request) {
  const cache = await caches.open(MEDIA_CACHE);
  const cached = await cache.match(request);

  if (cached) {
    console.log("[SW] Serving from cache:", request.url);
    return cached;
  }

  try {
    const response = await fetch(request);

    // Cache successful media responses
    if (response.ok) {
      // Clone response before caching
      const responseToCache = response.clone();

      // Evict old entries if cache is full
      await evictOldMediaIfNeeded(cache);

      cache.put(request, responseToCache);
      console.log("[SW] Cached media:", request.url);
    }

    return response;
  } catch (error) {
    console.error("[SW] Media fetch failed:", error);
    throw error;
  }
}

/**
 * Evict old media entries if cache exceeds limit
 */
async function evictOldMediaIfNeeded(cache) {
  const keys = await cache.keys();

  if (keys.length >= MAX_MEDIA_CACHE_ITEMS) {
    // Delete oldest entries (first in, first out)
    const toDelete = keys.slice(0, keys.length - MAX_MEDIA_CACHE_ITEMS + 10);

    for (const request of toDelete) {
      await cache.delete(request);
      console.log("[SW] Evicted from cache:", request.url);
    }
  }
}

/**
 * Message handler for cache control
 */
self.addEventListener("message", (event) => {
  const { type, payload } = event.data || {};

  switch (type) {
    case "CLEAR_CACHE":
      clearAllCaches();
      break;

    case "PRELOAD_PHOTOS":
      preloadPhotos(payload?.urls || []);
      break;

    case "SKIP_WAITING":
      self.skipWaiting();
      break;
  }
});

/**
 * Clear all caches
 */
async function clearAllCaches() {
  const cacheNames = await caches.keys();
  await Promise.all(
    cacheNames
      .filter((name) => name.startsWith("librafoto-"))
      .map((name) => caches.delete(name))
  );
  console.log("[SW] All caches cleared");
}

/**
 * Preload photos into cache
 */
async function preloadPhotos(urls) {
  const cache = await caches.open(MEDIA_CACHE);

  for (const url of urls) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        await cache.put(url, response);
        console.log("[SW] Preloaded:", url);
      }
    } catch (error) {
      console.warn("[SW] Preload failed:", url, error);
    }
  }
}
