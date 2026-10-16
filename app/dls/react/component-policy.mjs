import { existsSync, readdirSync, readFileSync } from "node:fs";

export function enforceReactComponents(root) {
  const manifest = JSON.parse(readFileSync(new URL("package.json", root), "utf8"));
  if (manifest.dependencies?.["@awesome.me/webawesome"] || manifest.scripts?.["build:dls"])
    throw Error("Retired widget dependency/build reintroduced");
  const walk = (directory) =>
    readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
      const path = new URL(`${entry.name}${entry.isDirectory() ? "/" : ""}`, directory);
      return entry.isDirectory() ? walk(path) : [path];
    });
  for (const file of [...walk(new URL("tools/", root)), ...walk(new URL("dls/react/", root))]) {
    if (!file.pathname.endsWith(".tsx") || /\.(spec|test)\.tsx$/.test(file.pathname)) continue;
    if (file.pathname.includes("/dls/react/components/ui/")) continue;
    const source = readFileSync(file, "utf8");
    if (/<(?:button|input|select|textarea)\b/.test(source))
      throw Error(`Use shared React controls: ${file.pathname}`);
  }
  for (const entry of ["styles.css"]) {
    const source = readFileSync(new URL(`dls/react/${entry}`, root), "utf8");
    if (/legacy-adapter|runtime-components|wa-number-input/.test(source))
      throw Error(`Retired control skin in ${entry}`);
  }
}
