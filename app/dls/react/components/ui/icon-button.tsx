import { forwardRef } from "react";
import { Button, type ButtonProps } from "./button";
import { cn } from "../../lib/utils";
/** Icon-only actions retain a 44px target and an explicit accessible name. */
export const IconButton = forwardRef<HTMLButtonElement, ButtonProps & { label: string }>(
  ({ label, className, variant = "ghost", ...props }, ref) => (
    <Button
      {...props}
      ref={ref}
      variant={variant}
      aria-label={label}
      className={cn("dls-icon-button size-11 shrink-0 p-0", className)}
    />
  ),
);
IconButton.displayName = "IconButton";
