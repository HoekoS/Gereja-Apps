import { Popover, PopoverTrigger, PopoverContent } from "./popover";
import { useEffect, useState, type ReactNode } from "react";
import { Input } from "./input";
import { Check } from "lucide-react";
import type { Appearance, Palette } from "../../appearance";
export function AppearanceRow({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <div className="dls-appearance-row">
      {htmlFor ? <label htmlFor={htmlFor}>{label}</label> : <span>{label}</span>}
      <div className="dls-appearance-value">{children}</div>
    </div>
  );
}
export function ModePicker({
  value,
  onChange,
}: {
  value: Appearance;
  onChange: (value: Appearance) => void;
}) {
  return (
    <fieldset className="dls-mode-picker">
      <legend className="sr-only">Mode</legend>
      {(["system", "light", "dark"] as const).map((mode) => (
        <label key={mode} className="dls-mode-option">
          <input
            type="radio"
            name="appearance-mode"
            value={mode}
            checked={value === mode}
            onChange={() => onChange(mode)}
          />
          <span className={`dls-mode-preview dls-mode-preview-${mode}`} aria-hidden="true">
            <i />
            <span>
              <b />
              <b />
              <b />
            </span>
          </span>
          <span className="dls-mode-caption">
            {mode === "system" ? "System" : mode === "light" ? "Light" : "Dark"}
          </span>
        </label>
      ))}
    </fieldset>
  );
}
function hexColor(value: string) {
  const [h, s, l] = value.match(/[\d.]+/g)?.map(Number) || [];
  if (h === undefined) return "";
  const saturation = s / 100,
    lightness = l / 100;
  const a = saturation * Math.min(lightness, 1 - lightness);
  return (
    "#" +
    [0, 8, 4]
      .map((n) => {
        const k = (n + h / 30) % 12;
        const channel = lightness - a * Math.max(-1, Math.min(k - 3, 9 - k, 1));
        return Math.round(255 * channel)
          .toString(16)
          .padStart(2, "0");
      })
      .join("")
      .toUpperCase()
  );
}
export function ColorReadout({
  token,
  label,
}: {
  token: "background" | "foreground";
  label: string;
}) {
  const color = document.documentElement.style.getPropertyValue(`--rover-${token}`);
  return (
    <output className="dls-color-readout" aria-label={label}>
      <span aria-hidden="true" style={{ backgroundColor: `hsl(${color})` }} />
      {hexColor(color)}
    </output>
  );
}

export function ColorPicker({
  token,
  label,
  onChange,
}: {
  token: "background" | "foreground";
  label: string;
  onChange: (value: string) => void;
}) {
  const color = hexColor(document.documentElement.style.getPropertyValue(`--rover-${token}`));
  const [draft, setDraft] = useState(color);
  useEffect(() => setDraft(color), [color]);
  return (
    <Popover>
      <PopoverTrigger className="dls-color-picker" aria-label={`Choose ${label.toLowerCase()}`}>
        <span className="dls-color-dot" aria-hidden="true" style={{ backgroundColor: color }} />
        <span>{color}</span>
      </PopoverTrigger>
      <PopoverContent align="end" className="dls-color-picker-panel" aria-label={`${label} picker`}>
        <div className="dls-color-presets" role="group" aria-label={`${label} presets`}>
          {[
            ["Green", "#163b28"],
            ["Teal", "#0b7065"],
            ["Blue", "#185abd"],
            ["Violet", "#7138b5"],
            ["Black", "#101820"],
            ["White", "#ffffff"],
          ].map(([name, value]) => (
            <button
              key={name}
              type="button"
              aria-label={`${label}: ${name}`}
              aria-pressed={color.toLowerCase() === value}
              style={{ backgroundColor: value }}
              onClick={() => onChange(value)}
            />
          ))}
        </div>
        <label className="dls-color-hex-label">
          Hex color
          <Input
            aria-label={`${label} hex`}
            value={draft}
            maxLength={7}
            spellCheck={false}
            onBlur={() => setDraft(color)}
            onChange={(event) => {
              setDraft(event.target.value);
              if (/^#[0-9a-f]{6}$/i.test(event.target.value)) onChange(event.target.value);
            }}
          />
        </label>
        <label className="dls-color-native-label">
          Custom color
          <input
            type="color"
            aria-label={label}
            value={color || "#000000"}
            onChange={(event) => onChange(event.target.value)}
          />
        </label>
      </PopoverContent>
    </Popover>
  );
}

export function ThemePicker({
  value,
  onChange,
}: {
  value: Palette;
  onChange: (value: Palette) => void;
}) {
  return (
    <fieldset className="dls-theme-picker">
      <legend>Theme</legend>
      <div className="dls-theme-grid">
        {(
          [
            { id: "workbench", label: "Warm Workbench" },
            { id: "robotics", label: "Rover" },
            { id: "nord", label: "Nord" },
            { id: "dracula", label: "Dracula" },
          ] as const
        ).map((theme) => (
          <label key={theme.id} className="dls-theme-option" data-palette={theme.id}>
            <input
              type="radio"
              name="appearance-palette"
              value={theme.id}
              checked={value === theme.id}
              onChange={() => onChange(theme.id)}
            />
            <span className="dls-theme-thumbnail" aria-hidden="true">
              <span className="dls-theme-mini-nav">
                <i />
                <i />
                <i />
              </span>
              <span className="dls-theme-mini-page">
                <b />
                <span />
                <span />
                <i />
              </span>
            </span>
            <span className="dls-theme-caption">
              {theme.label}
              <Check aria-hidden="true" />
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}
