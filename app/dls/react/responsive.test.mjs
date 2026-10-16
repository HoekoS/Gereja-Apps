import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const screens = require("./breakpoints.cjs");
const read = (name) => readFileSync(new URL(name, import.meta.url), "utf8");

test("shared layout media queries use named rem breakpoints aligned with Tailwind", () => {
  const allowed = new Set(Object.values(screens));
  for (const file of ["globals.css", "components.css", "responsive.css", "../layout.css"]) {
    for (const [, value] of read(file).matchAll(
      /@media\s*\(\s*(?:max|min)-width\s*:\s*([\d.]+(?:px|rem))/g,
    )) {
      assert.ok(allowed.has(value), `${file}: unregistered breakpoint ${value}`);
    }
  }
  assert.equal(require("./tailwind.config.js").theme.screens, screens);
});
