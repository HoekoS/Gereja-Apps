import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { OverlayHost } from "./overlay-host";
import { cn } from "../../lib/utils";
export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;
export const DialogClose = DialogPrimitive.Close;
export const DialogTitle = DialogPrimitive.Title;
export const DialogDescription = DialogPrimitive.Description;
export const DialogContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content>
>(({ className, children, ...props }, ref) => {
  const host = React.useContext(OverlayHost);
  return (
    <DialogPrimitive.Portal container={host}>
      <DialogPrimitive.Overlay className="dls-dialog-overlay fixed inset-0 z-40" />
      <DialogPrimitive.Content
        ref={ref}
        className={cn(
          "dls-dialog-content fixed left-1/2 top-1/2 z-50 max-h-[90dvh] w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-card bg-card p-6 shadow-soft-card",
          className,
        )}
        {...props}
      >
        {children}
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
});
DialogContent.displayName = "DialogContent";

/** Shared structure keeps title and actions visually separate from form content. */
export function DialogHeader({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("dls-dialog-header", className)} {...props} />;
}
export function DialogFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("dls-dialog-footer", className)} {...props} />;
}
