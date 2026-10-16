/** Renderer snapshots contain geometry; React owns text, semantic color and DOM. */
export type SceneLabel = {
  id: string | number;
  text: string;
  x: number;
  y: number;
  status?: string;
  emphasis?: boolean;
  warning?: boolean;
  error?: boolean;
  stale?: boolean;
};
export function SceneLabels({ labels, above = false }: { labels: SceneLabel[]; above?: boolean }) {
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
      {labels.map((label) => (
        <span
          key={label.id}
          className={`absolute -translate-x-1/2 ${above ? "-translate-y-full" : "-translate-y-1/2"} grid gap-1 rounded-pill border border-border bg-popover/90 px-3 py-1 text-xs text-popover-foreground shadow-sm ${label.emphasis ? "font-semibold ring-1 ring-primary" : ""} ${label.stale ? "opacity-60" : ""}`}
          style={{
            left: label.x,
            top: label.y,
            borderColor: label.error
              ? "var(--dls-danger)"
              : label.warning
                ? "var(--dls-warning)"
                : undefined,
          }}
        >
          <span>{label.text}</span>
          {label.status && <span className="text-muted-foreground">{label.status}</span>}
        </span>
      ))}
    </div>
  );
}
