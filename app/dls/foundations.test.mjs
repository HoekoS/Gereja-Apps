// Guards the DLS foundations (see ../AGENTS.md): tools take type, colour and
// page width from shared React DLS tokens instead of hard-coding them.
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync, existsSync } from "node:fs";

const root = new URL("../", import.meta.url);
const read = (path) => readFileSync(new URL(path, root), "utf8");

// Illustration and 3D-overlay rules keep fixed values: race scenery and sprites,
// labels drawn over the always-dark 3D canvas, legend swatches that must match
// scene colours, and the printed barcode sheet. Add a selector here only for
// art; UI chrome uses tokens.
const ART =
  /lab-|track-|floor-|garage-|\.rover|podium|event-label|maintenance|shutdown|legend-barcode|live-dot|race-top|#leader|\.clock|lane-|race-mark|empty-grid|status-led|#viewport|#scene|follow-hint|rover-label|rover-status|\.swatch|\.tooltip|\.tt-|drop-hint|vertex-label|edge-|#decode-grid|\.key\b|\.amber|\.blue\b|#preview svg/;

function sources() {
  const out = [];
  for (const tool of readdirSync(new URL("tools/", root))) {
    const css = `tools/${tool}/style.css`,
      html = `tools/${tool}/index.html`;
    if (existsSync(new URL(css, root))) out.push([css, read(css)]);
    const layout = `tools/${tool}/react-layout.css`;
    if (existsSync(new URL(layout, root))) out.push([layout, read(layout)]);
    if (existsSync(new URL(html, root))) {
      const inline = [...read(html).matchAll(/<style>([\s\S]*?)<\/style>/g)]
        .map((m) => m[1])
        .join("\n");
      if (inline.trim()) out.push([`${html} <style>`, inline]);
    }
  }
  return out;
}

function declarations() {
  const found = [];
  for (const [file, css] of sources()) {
    const clean = css.replace(/\/\*[\s\S]*?\*\//g, "");
    for (const [, selector, body] of clean.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
      if (ART.test(selector)) continue;
      for (const decl of body.split(";")) {
        const at = decl.indexOf(":");
        if (at < 0) continue;
        found.push({
          file,
          selector: selector.trim().replace(/\s+/g, " "),
          prop: decl.slice(0, at).trim().toLowerCase(),
          value: decl.slice(at + 1).trim(),
        });
      }
    }
  }
  return found;
}

const where = (d) => `${d.file}: ${d.selector} { ${d.prop}: ${d.value} }`;

test("tools size text with --dls-text-* tokens only", () => {
  const bad = declarations().filter(
    (d) =>
      (d.prop === "font-size" &&
        !/^(var\(--dls-text-(xs|sm|md|lg|xl)\)|inherit|1em|100%)(\s*!important)?$/.test(d.value)) ||
      (d.prop === "font" && /\d(px|rem|pt)\b/.test(d.value)),
  );
  assert.deepEqual(bad.map(where), []);
});

test("tools name fonts with --dls-font-* tokens only", () => {
  const bad = declarations().filter(
    (d) =>
      d.prop === "font-family" && !/^(var\(--dls-font-(body|display|mono)\)|inherit)/.test(d.value),
  );
  assert.deepEqual(bad.map(where), []);
});

test("tools colour text, surfaces and borders with theme tokens only", () => {
  const fixed = /#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\(|\b(white|black)\b/i;
  const bad = declarations().filter(
    (d) =>
      /^(color|background(-color)?|border(-(top|right|bottom|left))?(-color)?|outline(-color)?)$/.test(
        d.prop,
      ) && fixed.test(d.value),
  );
  assert.deepEqual(bad.map(where), []);
});

test("tools leave page width to the DLS page frame", () => {
  const bad = declarations().filter(
    (d) => d.prop === "max-width" && /\b([6-9]\d{2}|\d{4,})px\b/.test(d.value),
  );
  assert.deepEqual(bad.map(where), []);
});
