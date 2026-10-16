import type { LiHTMLAttributes } from "react";
import { cn } from "../../lib/utils";

/** Entity selection belongs to the whole row, including trailing actions. */
export function ListRow({
  selected = false,
  className,
  ...props
}: LiHTMLAttributes<HTMLLIElement> & { selected?: boolean }) {
  return (
    <li
      {...props}
      data-dls-list-row=""
      data-selected={selected}
      className={cn(
        "flex min-w-0 items-center gap-2 rounded-xl border border-transparent p-1",
        className,
      )}
    />
  );
}
