import { Button } from "./button";
import { ChevronDown } from "lucide-react";
import * as React from "react";
import * as Primitive from "@radix-ui/react-popover";
import { OverlayHost } from "./overlay-host";
import { cn } from "../../lib/utils";
export const Popover = Primitive.Root;
export const PopoverTrigger = Primitive.Trigger;
export const PopoverContent = React.forwardRef<
  React.ElementRef<typeof Primitive.Content>,
  React.ComponentPropsWithoutRef<typeof Primitive.Content>
>(({ className, sideOffset = 8, collisionPadding = 16, ...props }, ref) => {
  const host = React.useContext(OverlayHost);
  return (
    <Primitive.Portal container={host}>
      <Primitive.Content
        ref={ref}
        sideOffset={sideOffset}
        collisionPadding={collisionPadding}
        className={cn("dls-popover", className)}
        {...props}
      />
    </Primitive.Portal>
  );
});
PopoverContent.displayName = "PopoverContent";

/** Small action collection, with shared trigger geometry and dismiss behavior. */
export function ActionMenu({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="secondary">
          {label}
          <ChevronDown aria-hidden="true" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="dls-action-popover" aria-label={label}>
        {children}
      </PopoverContent>
    </Popover>
  );
}
