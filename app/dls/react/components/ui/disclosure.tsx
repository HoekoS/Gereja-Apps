import { ChevronRight } from "lucide-react";
import type { ReactNode, HTMLAttributes } from "react";
import { cn } from "../../lib/utils";
export function Disclosure({
  ref,
  title,
  description,
  children,
  className,
  ...props
}: Omit<HTMLAttributes<HTMLDetailsElement>, "title"> & {
  ref?: import("react").Ref<HTMLDetailsElement>;
  title: ReactNode;
  description?: ReactNode;
  open?: boolean;
}) {
  return (
    <details
      ref={ref}
      {...props}
      className={cn("dls-disclosure", description && "dls-disclosure-settings", className)}
    >
      <summary>
        {description ? (
          <span className="dls-disclosure-caption">
            <span>{title}</span>
            <span>{description}</span>
          </span>
        ) : (
          title
        )}
        <ChevronRight aria-hidden="true" className="dls-disclosure-chevron" />
      </summary>
      <div className="dls-disclosure-body">{children}</div>
    </details>
  );
}
/** One visible, mutually exclusive choice; native radio keyboard navigation. */
export function SegmentedChoice({
  label,
  children,
  className,
  ...props
}: HTMLAttributes<HTMLFieldSetElement> & { label: string }) {
  return (
    <fieldset {...props} className={cn("dls-segmented", className)}>
      <legend className="sr-only">{label}</legend>
      {children}
    </fieldset>
  );
}
