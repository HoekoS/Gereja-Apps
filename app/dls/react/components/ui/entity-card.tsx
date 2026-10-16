import { forwardRef, type ButtonHTMLAttributes } from "react";
import { cn } from "../../lib/utils";
/** Select an entity for editing; card geometry is distinct from action buttons. */
export const EntityCard = forwardRef<
  HTMLButtonElement,
  ButtonHTMLAttributes<HTMLButtonElement> & { selected?: boolean }
>(({ selected = false, className, children, ...props }, ref) => (
  <button
    {...props}
    ref={ref}
    type="button"
    aria-pressed={selected}
    className={cn(
      "dls-entity-card grid min-w-0 gap-4 rounded-card border border-border bg-card p-5 text-left text-card-foreground shadow-soft-card transition-shadow hover:ring-1 hover:ring-border",
      selected && "ring-2 ring-primary",
      className,
    )}
  >
    {children}
  </button>
));
EntityCard.displayName = "EntityCard";
