import * as React from "react";
import { cn } from "../../lib/utils";
// A real select remains the controller port: native value, options, change and
// disabled semantics. The named-server adapter can enhance it without replacing it.
export const Select = React.forwardRef<
  HTMLSelectElement,
  React.SelectHTMLAttributes<HTMLSelectElement> & { leadingIcon?: React.ReactNode }
>(({ className, children, leadingIcon, ...props }, ref) => {
  const control = (
    <select
      ref={ref}
      className={cn(
        leadingIcon && "dls-select-with-icon",
        "dls-select min-h-11 min-w-0 rounded-pill border border-input bg-card text-card-foreground px-4 py-2 pr-10 text-base placeholder:text-muted-foreground disabled:opacity-50 shadow-pill-inset",
        className,
      )}
      {...props}
    >
      {children}
    </select>
  );
  return leadingIcon ? (
    <div className="dls-select-field">
      <span className="dls-select-leading" aria-hidden="true">
        {leadingIcon}
      </span>
      {control}
    </div>
  ) : (
    control
  );
});
Select.displayName = "Select";
