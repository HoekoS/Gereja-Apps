import type {
  HTMLAttributes,
  FieldsetHTMLAttributes,
  LabelHTMLAttributes,
  ReactNode,
  TableHTMLAttributes,
} from "react";
import { cn } from "../../lib/utils";
export function FormField({
  id,
  label,
  children,
  className,
  ...props
}: Omit<HTMLAttributes<HTMLDivElement>, "id"> & {
  id: string;
  label: ReactNode;
}) {
  return (
    <div {...props} className={cn("dls-form-field", className)}>
      <label htmlFor={id}>{label}</label>
      {children}
    </div>
  );
}
export function FieldGroup({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div {...props} className={cn("dls-field-group", className)} />;
}
export function ActionBar({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div {...props} className={cn("dls-action-bar", className)} />;
}
export function ChoiceRow({ className, ...props }: LabelHTMLAttributes<HTMLLabelElement>) {
  return <label {...props} className={cn("dls-choice-row", className)} />;
}
export function Callout({
  tone = "info",
  className,
  ...props
}: HTMLAttributes<HTMLDivElement> & { tone?: "info" | "warning" }) {
  return <div {...props} data-tone={tone} className={cn("dls-callout", className)} />;
}
/** React owns the message text and semantic tone. */
export function StatusMessage({ className, ...props }: HTMLAttributes<HTMLParagraphElement>) {
  return (
    <p
      role="status"
      data-dls-message=""
      {...props}
      className={cn("dls-status-message", className)}
    />
  );
}
export function DataTable({ className, ...props }: TableHTMLAttributes<HTMLTableElement>) {
  return (
    <div
      className="dls-table-scroll"
      tabIndex={0}
      role="region"
      aria-label={props["aria-label"] || "Data table"}
    >
      <table {...props} className={cn("dls-data-table", className)} />
    </div>
  );
}

/** A spaced form group retaining native disabled semantics. */
export function FormFieldset({
  legend,
  className,
  children,
  ...props
}: FieldsetHTMLAttributes<HTMLFieldSetElement> & { legend: ReactNode }) {
  return (
    <fieldset {...props} className={cn("dls-form-stack", className)}>
      <legend className="sr-only">{legend}</legend>
      {children}
    </fieldset>
  );
}
