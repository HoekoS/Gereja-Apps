import test from "node:test";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import { buildSync } from "esbuild";

// Color calculation is framework-independent. Execute the real TypeScript
// module; the fixture only supplies browser storage and CSSStyleDeclaration.
const { outputFiles } = buildSync({
  entryPoints: [fileURLToPath(new URL("./appearance.ts", import.meta.url))],
  bundle: true,
  write: false,
  format: "esm",
  target: "es2022",
});
const code = outputFiles[0].text;
const theme = await import(`data:text/javascript;base64,${Buffer.from(code).toString("base64")}`);
function luminance(value) {
  const [h, s, l] = value.match(/[\d.]+/g).map(Number);
  const sat = s / 100,
    light = l / 100;
  const chroma = (1 - Math.abs(2 * light - 1)) * sat,
    x = chroma * (1 - Math.abs(((h / 60) % 2) - 1)),
    m = light - chroma / 2;
  const rgb =
    h < 60
      ? [chroma, x, 0]
      : h < 120
        ? [x, chroma, 0]
        : h < 180
          ? [0, chroma, x]
          : h < 240
            ? [0, x, chroma]
            : h < 300
              ? [x, 0, chroma]
              : [chroma, 0, x];
  return rgb
    .map((v) => v + m)
    .map((v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
    .reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
}

test("every built-in palette and accent keeps text readable on interaction surfaces", (t) => {
  const old = {
    document: globalThis.document,
    window: globalThis.window,
    localStorage: globalThis.localStorage,
  };
  const tokens = new Map(),
    storage = new Map();
  globalThis.document = {
    documentElement: {
      dataset: {},
      style: { setProperty: (k, v) => tokens.set(k, v), removeProperty: (k) => tokens.delete(k) },
    },
  };
  globalThis.window = { matchMedia: () => ({ matches: false }) };
  globalThis.localStorage = {
    getItem: (k) => storage.get(k) ?? null,
    setItem: (k, v) => storage.set(k, v),
  };
  t.after(() => {
    for (const [key, value] of Object.entries(old)) {
      if (value === undefined) delete globalThis[key];
      else globalThis[key] = value;
    }
  });
  for (const palette of ["workbench", "robotics", "nord", "dracula"]) {
    for (const appearance of ["light", "dark"]) {
      for (const accent of ["theme", "green", "teal", "blue", "violet", "amber"]) {
        storage.set(theme.paletteKey, palette);
        storage.set(theme.appearanceKey, appearance);
        storage.set(theme.customizationKey, JSON.stringify({ accent }));
        theme.applyReactAppearance();
        for (const [fill, text] of [
          ["primary", "primary-foreground"],
          ["accent", "accent-foreground"],
          ["secondary", "secondary-foreground"],
          ["card", "card-foreground"],
          ["card", "muted-foreground"],
        ]) {
          const a = luminance(tokens.get(`--rover-${fill}`)),
            b = luminance(tokens.get(`--rover-${text}`));
          const ratio = (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
          assert.ok(
            ratio >= 4.5,
            `${palette}/${appearance}/${accent}/${fill}: ${ratio.toFixed(2)}:1`,
          );
        }
      }
    }
  }
});
