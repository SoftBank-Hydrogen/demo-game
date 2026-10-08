mergeInto(LibraryManager.library, {
  PulseReport: function (jsonPtr) {
    window.__pulseState = JSON.parse(UTF8ToString(jsonPtr));
    window.dispatchEvent(new CustomEvent('pulse-state', { detail: window.__pulseState }));
  },
  PulseReducedMotion: function () {
    return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 1 : 0;
  }
});
