import { useState } from "react";
import { Plus, X } from "lucide-react";
import { Input } from "./input";
import { Button } from "./button";
import { IconButton } from "./icon-button";
/** Ordered entries with single creation and multiline paste, retaining duplicates. */
export function CreatableList({
  id,
  label,
  values,
  onChange,
  disabled,
  maxItems = 100,
}: {
  id: string;
  label: string;
  values: string[];
  onChange: (values: string[]) => void;
  disabled?: boolean;
  maxItems?: number;
}) {
  const [draft, setDraft] = useState("");
  const [error, setError] = useState("");
  function add(text: string) {
    const additions = text
      .split(/\r?\n/)
      .map((value) => value.trim())
      .filter(Boolean);
    if (!additions.length) return;
    if (
      values.length + additions.length > maxItems ||
      [...values, ...additions].join("\n").length > 8192
    ) {
      setError(`Use up to ${maxItems} entries and 8192 characters.`);
      return;
    }
    onChange([...values, ...additions]);
    setDraft("");
    setError("");
  }
  return (
    <div className="dls-form-stack">
      <label htmlFor={id} className="text-sm font-semibold">
        {label}{" "}
        <span className="text-muted-foreground">
          {values.length} / {maxItems}
        </span>
      </label>
      <div className="flex gap-2">
        <Input
          id={id}
          aria-label={label}
          placeholder="Add a name"
          value={draft}
          disabled={disabled}
          aria-describedby={`${id}-help ${id}-error`}
          aria-invalid={!!error}
          onChange={(event) => {
            setDraft(event.target.value);
            setError("");
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              add(draft);
            }
          }}
          onPaste={(event) => {
            const text = event.clipboardData.getData("text");
            if (/\r?\n/.test(text)) {
              event.preventDefault();
              add(text);
            }
          }}
        />
        <Button
          variant="secondary"
          disabled={disabled || !draft.trim()}
          onClick={() => add(draft)}
          aria-label="Add participant"
        >
          <Plus aria-hidden="true" />
          Add
        </Button>
      </div>
      <p id={`${id}-help`} className="text-sm text-muted-foreground">
        Press Enter to add, or paste one name per line.
      </p>
      <p id={`${id}-error`} role="alert" hidden={!error}>
        {error}
      </p>
      <ol aria-label="Participants" className="dls-creatable-entries">
        {values.map((value, index) => (
          <li
            key={index}
            className="flex min-w-0 items-center gap-3 rounded-lg bg-secondary px-3 py-1"
          >
            <span className="text-sm text-muted-foreground">{index + 1}</span>
            <span className="min-w-0 flex-1 break-words">{value}</span>
            <IconButton
              label={`Remove ${value}, participant ${index + 1}`}
              disabled={disabled}
              onClick={() => onChange(values.filter((_, position) => position !== index))}
            >
              <X aria-hidden="true" />
            </IconButton>
          </li>
        ))}
      </ol>
    </div>
  );
}
