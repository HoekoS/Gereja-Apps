import { forwardRef } from "react";
import { cn } from "../../lib/utils";
/** Native checked/change contract retained for runtime-owned settings. */
export const SwitchInput = forwardRef<
  HTMLInputElement,
  Omit<React.InputHTMLAttributes<HTMLInputElement>, "type" | "role">
>(({ className, ...props }, ref) => (
  <input
    {...props}
    ref={ref}
    type="checkbox"
    role="switch"
    className={cn("dls-switch", className)}
  />
));
SwitchInput.displayName = "SwitchInput";
