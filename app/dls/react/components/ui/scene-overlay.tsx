import type { ReactNode } from "react";
/** Informational overlay anchored within a renderer viewport. */
export function SceneOverlay({ x, y, children }: { x: number; y: number; children: ReactNode }) {
  return (
    <div
      role="tooltip"
      className="pointer-events-none absolute z-20 grid gap-1 rounded-lg border border-border bg-popover p-3 text-sm text-popover-foreground shadow-soft-card"
      style={{
        maxWidth: "min(20rem, calc(100% - 24px))",
        left: `clamp(12px, ${x + 12}px, calc(100% - 21rem))`,
        top: `clamp(12px, ${y + 12}px, calc(100% - 12rem))`,
      }}
    >
      {children}
    </div>
  );
}
