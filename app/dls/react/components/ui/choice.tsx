import * as React from "react";
import { cn } from "../../lib/utils";
export const ChoiceInput = React.forwardRef<
  HTMLInputElement,
  React.InputHTMLAttributes<HTMLInputElement>
>(({ className, ...props }, ref) => (
  <input
    ref={ref}
    className={cn("dls-choice size-5 shrink-0 accent-primary", className)}
    {...props}
  />
));
ChoiceInput.displayName = "ChoiceInput";
export const RangeInput = React.forwardRef<
  HTMLInputElement,
  React.InputHTMLAttributes<HTMLInputElement>
>(({ className, ...props }, ref) => (
  <input
    ref={ref}
    className={cn("dls-range min-h-11 w-full accent-primary", className)}
    {...props}
  />
));
RangeInput.displayName = "RangeInput";
