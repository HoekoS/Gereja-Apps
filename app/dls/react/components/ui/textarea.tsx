import * as React from "react";
import { cn } from "../../lib/utils";
export const Textarea = React.forwardRef<
  HTMLTextAreaElement,
  React.TextareaHTMLAttributes<HTMLTextAreaElement>
>(({ className, ...props }, ref) => (
  <textarea
    ref={ref}
    className={cn(
      "dls-textarea min-h-24 w-full min-w-0 resize-y rounded-lg border border-input bg-card text-card-foreground px-4 py-3 text-base placeholder:text-muted-foreground disabled:opacity-50 shadow-pill-inset",
      className,
    )}
    {...props}
  />
));
Textarea.displayName = "Textarea";
