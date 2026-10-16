import { forwardRef } from "react";
import { Button, type ButtonProps } from "./button";
export const BarcodeDot = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className = "", ...props }, ref) => (
    <Button {...props} ref={ref} variant="secondary" className={`dls-barcode-dot ${className}`} />
  ),
);
BarcodeDot.displayName = "BarcodeDot";
