import { useRef, useState } from "react";
import { X } from "lucide-react";
import { Input } from "./input";
import { Button } from "./button";
/** Ordered named values delivered directly to React consumers. */
export function ChipInput({
  id,
  label,
  description,
  value,
  onChange,
}: {
  id: string;
  label: string;
  description: string;
  value?: string[];
  onChange?: (values: string[]) => void;
}) {
  const [localValues, setLocalValues] = useState<string[]>([]);
  const values = value ?? localValues;
  function setValues(update: string[] | ((previous: string[]) => string[])) {
    const next = typeof update === "function" ? update(values) : update;
    if (value === undefined) setLocalValues(next);
    onChange?.(next);
  }
  const [draft, setDraft] = useState("");
  const [error, setError] = useState("");
  const entry = useRef<HTMLInputElement>(null);
  function add() {
    const value = draft.trim();
    if (!value) return;
    if (value.includes(",")) {
      setError("Add one vertex at a time.");
      return;
    }
    if (values.includes(value)) {
      setError("This vertex is already selected.");
      return;
    }
    setValues((previous) => [...previous, value]);
    setDraft("");
    setError("");
    entry.current?.focus();
  }
  return (
    <div className="dls-chip-input">
      <label htmlFor={`${id}-entry`}>{label}</label>
      <ul aria-label={`Selected ${label}`} className="flex flex-wrap gap-2">
        {values.map((value) => (
          <li
            key={value}
            className="flex min-h-11 items-center gap-2 rounded-pill bg-secondary pl-3"
          >
            <span>{value}</span>
            <Button
              variant="ghost"
              aria-label={`Remove ${value}`}
              onClick={() => {
                setValues((previous) => previous.filter((item) => item !== value));
                entry.current?.focus();
              }}
            >
              <X aria-hidden="true" className="size-4" />
            </Button>
          </li>
        ))}
      </ul>
      <div className="flex gap-2">
        <Input
          id={`${id}-entry`}
          ref={entry}
          value={draft}
          aria-label={label}
          aria-describedby={`${id}-help ${id}-error`}
          aria-invalid={!!error}
          onChange={(e) => {
            setDraft(e.target.value);
            setError("");
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              add();
            }
          }}
        />
        <Button variant="secondary" disabled={!draft.trim()} onClick={add}>
          Add
        </Button>
      </div>
      <p id={`${id}-help`} className="text-sm text-muted-foreground">
        {description}
      </p>
      <p id={`${id}-error`} role="alert" hidden={!error}>
        {error}
      </p>
    </div>
  );
}
