import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "../../lib/utils";
import { LoaderCircle } from "lucide-react";

export const buttonVariants = cva(
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-pill px-5 py-2 text-base font-medium transition-colors aria-pressed:ring-1 aria-pressed:ring-ring disabled:pointer-events-none disabled:opacity-50 [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary/90",
        secondary: "border-none bg-secondary text-secondary-foreground hover:bg-accent",
        ghost: "bg-transparent hover:bg-accent hover:text-accent-foreground",
        destructive: "bg-destructive text-destructive-foreground hover:bg-destructive/90",
        bare: "min-h-0 rounded-md bg-transparent p-0",
      },
    },
    defaultVariants: { variant: "default" },
  },
);
export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
  pending?: boolean;
  pendingLabel?: string;
}
export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant,
      asChild = false,
      type = "button",
      pending = false,
      pendingLabel,
      disabled,
      children,
      onClickCapture,
      ...props
    },
    ref,
  ) => {
    const Comp = asChild ? Slot : "button";
    return (
      <Comp
        className={cn(buttonVariants({ variant, className }))}
        data-dls-button=""
        data-variant={variant || "default"}
        aria-busy={pending || undefined}
        aria-disabled={(asChild && (disabled || pending)) || undefined}
        {...(!asChild ? { disabled: disabled || pending } : {})}
        ref={ref}
        {...(!asChild ? { type } : {})}
        {...props}
        onClickCapture={(event) => {
          if (pending || (asChild && disabled)) {
            event.preventDefault();
            event.stopPropagation();
            return;
          }
          onClickCapture?.(event);
        }}
      >
        {asChild ? (
          children
        ) : (
          <>
            {pending && <LoaderCircle className="dls-pending-spinner" aria-hidden="true" />}
            {pending && pendingLabel ? pendingLabel : children}
          </>
        )}
      </Comp>
    );
  },
);
Button.displayName = "Button";
