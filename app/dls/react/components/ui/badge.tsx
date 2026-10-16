import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "../../lib/utils";
import type { HTMLAttributes } from "react";
const badgeVariants = cva(
  "dls-badge inline-flex items-center gap-1 rounded-pill px-3 py-1 text-xs font-semibold",
  {
    variants: {
      variant: {
        default: "bg-secondary text-secondary-foreground",
        positive: "",
        alert: "",
      },
    },
    defaultVariants: { variant: "default" },
  },
);
export function Badge({
  className,
  variant,
  ...props
}: HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badgeVariants>) {
  return (
    <span
      className={cn(badgeVariants({ variant }), className)}
      data-tone={variant || "default"}
      {...props}
    />
  );
}
