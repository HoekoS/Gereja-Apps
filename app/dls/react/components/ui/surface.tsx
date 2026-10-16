import { Children, type HTMLAttributes, type ElementType } from "react";
import { cn } from "../../lib/utils";
export function Surface({
  as: Tag = "section",
  className,
  ...props
}: HTMLAttributes<HTMLElement> & { as?: ElementType }) {
  return (
    <Tag
      className={cn(
        "dls-panel rounded-card border-none bg-card p-6 text-card-foreground shadow-soft-card",
        className,
      )}
      {...props}
    />
  );
}
export function PageHeader({ className, children, ...props }: HTMLAttributes<HTMLElement>) {
  const [title, ...actions] = Children.toArray(children);
  return (
    <header className={cn("dls-page-header", className)} {...props}>
      {title}
      <div className="dls-page-header-actions">{actions}</div>
    </header>
  );
}
