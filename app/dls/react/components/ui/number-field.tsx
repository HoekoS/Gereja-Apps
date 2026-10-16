import { useRef, useImperativeHandle, forwardRef } from "react";
import { Minus, Plus } from "lucide-react";
import { Input } from "./input";
import { Button } from "./button";
type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: string;
};
/** Uncontrolled exact value; buttons use native stepping and notify controller listeners. */
export const NumberField = forwardRef<HTMLInputElement, Props>(
  ({ label, ...props }, forwardedRef) => {
    const input = useRef<HTMLInputElement>(null);
    useImperativeHandle(forwardedRef, () => input.current!);
    function step(direction: number) {
      const field = input.current;
      if (!field || field.disabled || field.readOnly) return;
      const before = field.value;
      if (direction > 0) field.stepUp();
      else field.stepDown();
      if (field.value !== before) {
        field.dispatchEvent(new Event("input", { bubbles: true }));
        field.dispatchEvent(new Event("change", { bubbles: true }));
      }
    }
    return (
      <div className="dls-number-field">
        <Button
          variant="secondary"
          aria-label={`Decrease ${label}`}
          disabled={props.disabled || props.readOnly}
          onClick={(e) => {
            e.preventDefault();
            step(-1);
          }}
        >
          <Minus aria-hidden="true" className="size-4" />
        </Button>
        <Input {...props} ref={input} type="number" />
        <Button
          variant="secondary"
          aria-label={`Increase ${label}`}
          disabled={props.disabled || props.readOnly}
          onClick={(e) => {
            e.preventDefault();
            step(1);
          }}
        >
          <Plus aria-hidden="true" className="size-4" />
        </Button>
      </div>
    );
  },
);
NumberField.displayName = "NumberField";
