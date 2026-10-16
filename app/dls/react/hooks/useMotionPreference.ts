import { useSyncExternalStore } from "react";
const query = "(prefers-reduced-motion: reduce)";
function subscribe(notify: () => void) {
  const media = window.matchMedia?.(query);
  media?.addEventListener("change", notify);
  window.addEventListener("dls-theme-changed", notify);
  window.addEventListener("storage", notify);
  return () => {
    media?.removeEventListener("change", notify);
    window.removeEventListener("dls-theme-changed", notify);
    window.removeEventListener("storage", notify);
  };
}
export function useMotionPreference() {
  return useSyncExternalStore(
    subscribe,
    () =>
      document.documentElement.dataset.motion === "reduce" || !!window.matchMedia?.(query).matches,
    () => true,
  );
}
