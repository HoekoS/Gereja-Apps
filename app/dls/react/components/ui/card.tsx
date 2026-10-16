import * as React from "react";
import { cn } from "../../lib/utils";
export const Card = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        "dls-card rounded-card border-none bg-card p-6 text-card-foreground shadow-soft-card",
        className,
      )}
      {...props}
    />
  ),
);
Card.displayName = "Card";
