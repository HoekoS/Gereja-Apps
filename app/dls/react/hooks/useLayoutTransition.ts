import { useCallback, useEffect, useRef } from "react";
import { flushSync } from "react-dom";
import { useMotionPreference } from "./useMotionPreference";

/** Snapshot geometry bridges a layout change while the real canvas resizes once.
 * State/focus stay in React. Unsupported browsers and reduced motion commit directly. */
export function useLayoutTransition() {
  const reduced = useMotionPreference();
  const active = useRef<ViewTransition | null>(null);
  useEffect(() => () => active.current?.skipTransition(), []);
  return useCallback(
    (update: () => void) => {
      active.current?.skipTransition();
      if (reduced || document.hidden || !document.startViewTransition) {
        update();
        return;
      }
      const transition = document.startViewTransition(() => flushSync(update));
      active.current = transition;
      const clear = () => {
        if (active.current === transition) active.current = null;
      };
      void transition.ready.catch(() => {
        /* Duplicate names can skip visuals; React state still commits. */
      });
      void transition.finished.then(clear, clear);
    },
    [reduced],
  );
}
