import { forwardRef, type ReactNode } from "react";
import {
  AnimatePresence,
  LayoutGroup,
  motion,
  MotionConfig,
  useIsPresent,
  type HTMLMotionProps,
} from "motion/react";
import { useMotionPreference } from "../../hooks/useMotionPreference";

export function MotionPolicy({ children }: { children: ReactNode }) {
  const reduced = useMotionPreference();
  return (
    <MotionConfig
      reducedMotion={reduced ? "always" : "never"}
      transition={{ type: "spring", stiffness: 420, damping: 36 }}
    >
      {children}
    </MotionConfig>
  );
}
/** Stable React keys preserve identity through add/remove/reorder. No stagger delays. */
export function MotionList({ children }: { children: ReactNode }) {
  return (
    <MotionPolicy>
      <LayoutGroup>
        <AnimatePresence initial={false}>{children}</AnimatePresence>
      </LayoutGroup>
    </MotionPolicy>
  );
}
export const MotionItem = forwardRef<HTMLElement, HTMLMotionProps<"article">>(function MotionItem(
  { children, layout = "position", ...props },
  ref,
) {
  const reduced = useMotionPreference();
  const present = useIsPresent();
  return (
    <motion.article
      ref={ref}
      inert={!present}
      aria-hidden={!present || undefined}
      layout={reduced ? false : layout}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={
        reduced
          ? { duration: 0 }
          : { type: "spring", stiffness: 420, damping: 36, opacity: { duration: 0.12 } }
      }
      {...props}
    >
      {children}
    </motion.article>
  );
});
