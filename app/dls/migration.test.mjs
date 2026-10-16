import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { pages, renderPage } from "../pages.mjs";
const root = new URL("../", import.meta.url);
const read = (path) => readFileSync(new URL(path, root), "utf8");

test("every tool and the shell use their shared DLS entry without legacy theme overrides", () => {
  for (const page of pages) {
    const { path } = page;
    const html = renderPage(page);
    assert.match(html, /href="\/dls\/react\/styles\.css\?page=[a-z-]+"/, path);
    assert.doesNotMatch(html, /\?v=|react\/dist\//, path);
    assert.doesNotMatch(html, /href="(?:\.\.\/\.\.\/)?theme\.css/, path);
  }
});

test("the DLS stylesheet imports local sources without cache tokens", () => {
  const imports = [...read("dls/react/styles.css").matchAll(/@import "([^"]+)";/g)];
  assert.ok(imports.length > 0);
  for (const [, path] of imports) {
    assert.doesNotMatch(path, /\?v=/, path);
    assert.ok(existsSync(new URL(path, new URL("dls/react/", root))), path);
  }
});

test("bundled fonts have literal font-face names and existing local resources", () => {
  const css = read("dls/foundations.css");
  assert.doesNotMatch(css, /@font-face\s*\{[^}]*font-family:\s*var\(/);
  const fonts = [
    ...css.matchAll(/@font-face\s*\{\s*font-family:\s*([^;]+);\s*src:\s*url\(["']([^"']+)["']\)/g),
  ];
  assert.equal(fonts.length, 2);
  for (const [, family, url] of fonts) {
    assert.ok(["Roboto", "Montserrat"].includes(family));
    assert.ok(existsSync(new URL(url, new URL("dls/", root))), url);
  }
});
