import { readableColor } from "./lib/color-contrast";
export type Appearance = "light" | "dark" | "system";
export type ResolvedMode = "light" | "dark";
export function resolveMode(appearance: Appearance): ResolvedMode {
  return appearance === "dark" ||
    (appearance === "system" && !!window.matchMedia?.("(prefers-color-scheme: dark)").matches)
    ? "dark"
    : "light";
}
type Customization = {
  accent?: string;
  font?: string;
  background?: string;
  foreground?: string;
  motion?: string;
  colors?: Partial<Record<ResolvedMode, { background?: string; foreground?: string }>>;
};
function readCustomization(): Customization {
  try {
    const value = JSON.parse(localStorage.getItem(customizationKey) || "{}");
    return value && typeof value === "object" && !Array.isArray(value) ? value : {};
  } catch {
    return {};
  }
}
export type Palette = "workbench" | "robotics" | "nord" | "dracula";
export const appearanceKey = "matsu.theme";
export type Accent = "theme" | "green" | "teal" | "blue" | "violet" | "amber";
export type FontChoice = "system" | "roboto";
export const customizationKey = "matsu.appearance-customization";
export const accents = {
  green: { light: "#237a32", dark: "#67c76e" },
  teal: { light: "#0b7065", dark: "#70dec9" },
  blue: { light: "#185abd", dark: "#8cbaff" },
  violet: { light: "#7138b5", dark: "#cfabff" },
  amber: { light: "#875600", dark: "#f4c46d" },
};
export const paletteKey = "matsu.palette";
export function readPreferences() {
  let appearance = "system",
    palette = "workbench";
  try {
    appearance = localStorage.getItem(appearanceKey) || appearance;
    palette = localStorage.getItem(paletteKey) || palette;
  } catch {
    /* Defaults remain usable without storage. */
  }
  const customization = readCustomization();
  const mode = resolveMode(
    (["light", "dark", "system"].includes(appearance) ? appearance : "system") as Appearance,
  );
  const colors = customization.colors ? customization.colors[mode] || {} : customization;
  const color = (value?: string) =>
    typeof value === "string" && /^#[0-9a-f]{6}$/i.test(value) ? value : "";
  return {
    motion: (customization.motion === "reduce" ? "reduce" : "system") as "reduce" | "system",
    background: color(colors.background),
    foreground: color(colors.foreground),
    accent: (["theme", "green", "teal", "blue", "violet", "amber"].includes(
      customization.accent || "",
    )
      ? customization.accent
      : "theme") as Accent,
    font: (customization.font === "roboto" ? "roboto" : "system") as FontChoice,
    appearance: (["light", "dark", "system"].includes(appearance)
      ? appearance
      : "system") as Appearance,
    palette: (["workbench", "robotics", "nord", "dracula"].includes(palette)
      ? palette
      : "workbench") as Palette,
  };
}
function tint(surface: string, accent: string, amount: number) {
  return (
    "#" +
    [1, 3, 5]
      .map((start) =>
        Math.round(
          parseInt(surface.slice(start, start + 2), 16) * (1 - amount) +
            parseInt(accent.slice(start, start + 2), 16) * amount,
        )
          .toString(16)
          .padStart(2, "0"),
      )
      .join("")
  );
}
function hsl(hex: string) {
  const [r, g, b] = [1, 3, 5].map((start) => parseInt(hex.slice(start, start + 2), 16) / 255);
  const max = Math.max(r, g, b),
    min = Math.min(r, g, b),
    lightness = (max + min) / 2,
    delta = max - min;
  let hue = 0,
    saturation = 0;
  if (delta) {
    saturation = delta / (1 - Math.abs(2 * lightness - 1));
    hue =
      max === r
        ? (g - b) / delta + (g < b ? 6 : 0)
        : max === g
          ? (b - r) / delta + 2
          : (r - g) / delta + 4;
    hue *= 60;
  }
  return `${hue.toFixed(2)} ${(saturation * 100).toFixed(2)}% ${(lightness * 100).toFixed(2)}%`;
}
const variants = {
  workbench: {
    light: ["#ebe7dc", "#fffdf6", "#282a25", "#62665c", "#8a877a", "#ad4313", "#f2eee4"],
    dark: ["#24231f", "#302f29", "#f6f0df", "#c5bfad", "#888474", "#f0b87a", "#3d3b32"],
  },
  nord: {
    light: ["#eceff4", "#f8fafc", "#2e3440", "#4c566a", "#c5cdd8", "#356576", "#d8e7ed"],
    dark: ["#2e3440", "#3b4252", "#eceff4", "#d8dee9", "#59667d", "#93c6d4", "#434c5e"],
  },
  dracula: {
    light: ["#f5f3fa", "#ffffff", "#282a36", "#626078", "#d4d0e0", "#6d3ab4", "#eae2fa"],
    dark: ["#282a36", "#303241", "#f8f8f2", "#b7b7cc", "#55586d", "#caa9fa", "#44475a"],
  },
};
const tokenNames = [
  "background",
  "foreground",
  "card",
  "card-foreground",
  "popover",
  "popover-foreground",
  "primary",
  "primary-foreground",
  "secondary",
  "secondary-foreground",
  "muted",
  "muted-foreground",
  "accent",
  "accent-foreground",
  "border",
  "input",
  "ring",
];
export function applyReactAppearance() {
  const preferences = readPreferences();
  const dark = resolveMode(preferences.appearance) === "dark";
  const root = document.documentElement;
  root.dataset.theme = dark ? "dark" : "light";
  root.dataset.palette = preferences.palette;
  root.style.setProperty("--rover-radius", preferences.palette === "workbench" ? "0.5rem" : "1rem");
  root.dataset.motion =
    preferences.motion === "reduce" ||
    window.matchMedia?.("(prefers-reduced-motion: reduce)").matches
      ? "reduce"
      : "normal";
  root.style.colorScheme = dark ? "dark" : "light";
  for (const name of tokenNames) root.style.removeProperty(`--rover-${name}`);
  const values =
    preferences.palette !== "robotics"
      ? [...variants[preferences.palette][dark ? "dark" : "light"]]
      : dark
        ? ["#101b20", "#1b2b32", "#edf7f4", "#b0c2c5", "#385059", "#67c76e", "#253941"]
        : ["#edf4f2", "#ffffff", "#18352e", "#526d65", "#c8dcd4", "#237a32", "#dfece7"];
  if (values) {
    if (preferences.background) values[0] = preferences.background;
    if (preferences.foreground) values[2] = preferences.foreground;
    values[2] = readableColor(values[2], [values[0]]);
    if (preferences.background) {
      values[1] = tint(values[0], values[2], 0.05);
      values[6] = tint(values[0], values[2], 0.1);
      values[4] = tint(values[0], values[2], 0.25);
      values[3] = tint(values[0], values[2], 0.72);
    }
    if (preferences.accent !== "theme")
      values[5] = accents[preferences.accent][dark ? "dark" : "light"];
    const surfaces = [values[0], values[1], values[6]];
    values[2] = readableColor(values[2], surfaces);
    values[3] = readableColor(values[3], surfaces);
    values[5] = readableColor(values[5], surfaces);
    const [background, surface, text, muted, border, primary, secondary] = values;
    const colors: Record<string, string> = {
      background,
      foreground: text,
      card: surface,
      "card-foreground": text,
      popover: surface,
      "popover-foreground": text,
      primary,
      "primary-foreground": readableColor(dark ? "#101820" : "#ffffff", [primary]),
      secondary,
      "secondary-foreground": text,
      muted: secondary,
      "muted-foreground": muted,
      accent: tint(surface, primary, dark ? 0.18 : 0.1),
      "accent-foreground": text,
      border,
      input: border,
      ring: primary,
    };
    for (const [name, color] of Object.entries(colors))
      root.style.setProperty(`--rover-${name}`, hsl(color));
  }
  root.style.setProperty(
    "--dls-font-body",
    preferences.font === "roboto"
      ? "Roboto, Arial, sans-serif"
      : "system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
  );
  root.style.setProperty("--dls-font-display", "Montserrat, var(--dls-font-body)");
}
function broadcast(target: Window) {
  try {
    target.dispatchEvent(new Event("dls-theme-changed"));
    for (let index = 0; index < target.frames.length; index++) broadcast(target.frames[index]);
  } catch {
    /* Ignore inaccessible child windows. */
  }
}
export function savePreferences(
  preferences: Pick<ReturnType<typeof readPreferences>, "appearance" | "palette"> &
    Partial<
      Pick<
        ReturnType<typeof readPreferences>,
        "accent" | "font" | "background" | "foreground" | "motion"
      >
    >,
) {
  const current = readPreferences();
  const stored = readCustomization();
  const mode = resolveMode(preferences.appearance);
  const currentMode = resolveMode(current.appearance);
  const colors =
    stored.colors && typeof stored.colors === "object"
      ? { ...stored.colors }
      : { [currentMode]: { background: current.background, foreground: current.foreground } };
  if (preferences.palette !== current.palette) {
    colors.light = {};
    colors.dark = {};
  }
  const previousColors = colors[mode] || {};
  colors[mode] = {
    background: preferences.background ?? previousColors.background ?? "",
    foreground: preferences.foreground ?? previousColors.foreground ?? "",
  };
  try {
    localStorage.setItem(appearanceKey, preferences.appearance);
    localStorage.setItem(paletteKey, preferences.palette);
    localStorage.setItem(
      customizationKey,
      JSON.stringify({
        version: 2,
        colors,
        accent: preferences.accent ?? current.accent,
        font: preferences.font ?? current.font,
        motion: preferences.motion ?? current.motion,
      }),
    );
  } catch {
    /* Still allow the current page to display its settings. */
  }
  broadcast(window.top || window);
}
export function captureAppearance() {
  return { ...readPreferences(), customization: readCustomization() };
}
export function restoreAppearance(preferences: ReturnType<typeof captureAppearance>) {
  savePreferences(preferences);
  if (preferences.customization && typeof preferences.customization === "object") {
    localStorage.setItem(customizationKey, JSON.stringify(preferences.customization));
    broadcast(window.top || window);
  }
}
export function resetPreferences() {
  savePreferences({
    appearance: "system",
    palette: "workbench",
    accent: "theme",
    font: "system",
    background: "",
    foreground: "",
    motion: "system",
  });
  try {
    localStorage.removeItem(customizationKey);
  } catch {}
  broadcast(window.top || window);
}
export function installReactAppearance() {
  applyReactAppearance();
  const storage = (event: StorageEvent) => {
    if (
      event.key === appearanceKey ||
      event.key === paletteKey ||
      event.key === customizationKey ||
      event.key === null
    )
      applyReactAppearance();
  };
  const shortcut = (event: KeyboardEvent) => {
    if (
      (event.ctrlKey || event.metaKey) &&
      !event.altKey &&
      !event.shiftKey &&
      event.key.toLowerCase() === "b"
    ) {
      event.preventDefault();
      (window.top || window).dispatchEvent(new Event("dls-nav-toggle"));
    }
  };
  const query = window.matchMedia?.("(prefers-color-scheme: dark)");
  const motionQuery = window.matchMedia?.("(prefers-reduced-motion: reduce)");
  window.addEventListener("dls-theme-changed", applyReactAppearance);
  window.addEventListener("storage", storage);
  document.addEventListener("keydown", shortcut);
  const systemChange = () => broadcast(window.top || window);
  query?.addEventListener("change", systemChange);
  motionQuery?.addEventListener("change", applyReactAppearance);
  return () => {
    window.removeEventListener("dls-theme-changed", applyReactAppearance);
    window.removeEventListener("storage", storage);
    document.removeEventListener("keydown", shortcut);
    query?.removeEventListener("change", systemChange);
    motionQuery?.removeEventListener("change", applyReactAppearance);
  };
}
