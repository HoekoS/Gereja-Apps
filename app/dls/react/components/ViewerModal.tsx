import { useEffect, useId, useRef, useState, type HTMLAttributes } from "react";
import { IconButton } from "./ui/icon-button";
import { OverlayHost } from "./ui/overlay-host";
import { cn } from "../lib/utils";

/** Native modal keeps controls and the underlying live scene mounted. */
export function ViewerModal({
  name,
  triggerLabel,
  reveal = false,
  open: controlledOpen,
  onOpenChange,
  className,
  children,
  ...props
}: HTMLAttributes<HTMLDialogElement> & {
  name: string;
  triggerLabel?: string;
  reveal?: boolean;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}) {
  const titleId = useId();
  const [host, setHost] = useState<HTMLDialogElement | null>(null);
  const [localOpen, setLocalOpen] = useState(false);
  const open = controlledOpen ?? localOpen;
  const setOpen = (value: boolean) => {
    setLocalOpen(value);
    onOpenChange?.(value);
  };
  const trigger = useRef<HTMLButtonElement>(null);
  const outsideDown = useRef(false);
  useEffect(() => {
    if (reveal) setOpen(true);
  }, [reveal]);
  useEffect(() => {
    if (!host) return;
    if (open && !host.open) host.showModal();
    else if (!open && host.open) {
      host.close();
      trigger.current?.focus();
    }
  }, [host, open]);
  function outside(event: React.MouseEvent<HTMLDialogElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    return (
      event.target === event.currentTarget &&
      (event.clientX < rect.left ||
        event.clientX > rect.right ||
        event.clientY < rect.top ||
        event.clientY > rect.bottom)
    );
  }
  return (
    <div className={cn("dls-viewer-modal-anchor", triggerLabel && "dls-viewer-modal-inline")}>
      <IconButton
        ref={trigger}
        variant="secondary"
        label={triggerLabel || `Open ${name}`}
        title={triggerLabel || `Open ${name}`}
        aria-haspopup="dialog"
        aria-expanded={open}
        onClick={() => setOpen(true)}
      >
        <img
          src="../../assets/tool-icons/settings.svg"
          width="28"
          height="28"
          alt=""
          aria-hidden="true"
        />
      </IconButton>
      <dialog
        {...props}
        ref={setHost}
        className="dls-viewer-modal"
        aria-labelledby={titleId}
        onCancel={(event) => {
          event.preventDefault();
          setOpen(false);
        }}
        onClose={() => setOpen(false)}
        onMouseDown={(event) => {
          outsideDown.current = outside(event);
        }}
        onClick={(event) => {
          if (outsideDown.current && outside(event)) setOpen(false);
          outsideDown.current = false;
        }}
      >
        <header className="dls-viewer-modal-header">
          <h2 id={titleId}>{name}</h2>
        </header>
        <OverlayHost.Provider value={host}>
          <div className={cn("dls-viewer-modal-body", className)}>{children}</div>
        </OverlayHost.Provider>
      </dialog>
    </div>
  );
}
