import { execFileSync } from "node:child_process";
import { buildSync } from "esbuild";
import { cpSync, existsSync, readFileSync, writeFileSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { enforceReactComponents } from "../dls/react/component-policy.mjs";
import { pages, renderPage } from "../pages.mjs";

export const appRoot = fileURLToPath(new URL("../", import.meta.url));

export function htmlPages() {
  return pages.map((page) => page.path);
}

export function copiedAtRuntime(path) {
  if (/^tools\/[^/]+\/apps-script(?:\/|$)/.test(path)) return false;
  if (/(?:^|\/)(?:node_modules|tests|verify-dashboard|bin|obj)(?:\/|$)/.test(path)) return false;
  if (/^tools\/[^/]+\/react(?:\/|$)/.test(path) || /^dls\/react(?:\/|$)/.test(path)) return false;
  if (/\.(?:test|spec)\./.test(path)) return false;
  return !/\.(?:html|tsx?|cjs|cs|csproj|md|ps1|py)$/.test(path);
}

function packageRoot(id) {
  const at = id.lastIndexOf("node_modules/");
  const name = id.slice(at + "node_modules/".length).split("/");
  return name.slice(0, name[0].startsWith("@") ? 2 : 1).join("/");
}

function notices(root, packages) {
  return [...packages]
    .sort()
    .map((name) => {
      const license = [
        ...["LICENSE", "LICENSE.md", "LICENSE.txt", "license", "license.md"].map((file) =>
          join(root, "node_modules", name, file),
        ),
        join(root, "dls/react/licenses", `${name.replaceAll("/", "-")}.txt`),
      ].find(existsSync);
      if (!license) throw Error(`Bundled dependency ${name} has no license file.`);
      return `node_modules/${name}\n${readFileSync(license, "utf8")}`;
    })
    .join("\n\n--------------------\n\n");
}

function lastCommit(root) {
  try {
    const [commit, author, date] = execFileSync("git", ["log", "-1", "--format=%H%x1f%an%x1f%cI"], {
      cwd: root,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    })
      .trim()
      .split("");
    return { commit, author, date };
  } catch {
    return {};
  }
}

export function buildInfo(root = appRoot, env = process.env) {
  const local = env.CI_COMMIT_SHA ? {} : lastCommit(root);
  const { version } = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
  return {
    version: env.CI_COMMIT_TAG || version || null,
    commit: env.CI_COMMIT_SHA || local.commit || null,
    author: env.CI_COMMIT_AUTHOR?.replace(/\s*<[^>]*>\s*$/, "") || local.author || null,
    date: env.CI_COMMIT_TIMESTAMP || local.date || null,
    repository: env.CI_PROJECT_URL || null,
  };
}

export function app({ root = appRoot } = {}) {
  const build = buildInfo(root);
  const bootstrap = buildSync({
    entryPoints: [join(root, "dls/react/theme-bootstrap.ts")],
    bundle: true,
    write: false,
    minify: true,
    format: "iife",
    target: "es2022",
  }).outputFiles[0].text;
  let outDir = join(root, "public");
  const byId = new Map(pages.map((page) => [join(root, page.path).replaceAll("\\", "/"), page]));
  const byUrl = new Map(
    pages.flatMap((page) => {
      const url = `/${page.path}`;
      return [[url, page], ...(url.endsWith("/index.html") ? [[url.slice(0, -10), page]] : [])];
    }),
  );
  return {
    name: "matsu",
    config() {
      return { define: { __ROVER_BUILD__: JSON.stringify(build) } };
    },
    resolveId(id) {
      const path = id.replaceAll("\\", "/");
      return byId.has(path) ? path : null;
    },
    load(id) {
      const page = byId.get(id.replaceAll("\\", "/"));
      return page ? renderPage(page) : null;
    },
    configureServer(server) {
      server.middlewares.use(async (request, response, next) => {
        const url = (request.url ?? "/").split(/[?#]/)[0];
        const page =
          byUrl.get(url === "" ? "/" : url) ?? (url === "/" ? byUrl.get("/index.html") : undefined);
        if (!page) return next();
        try {
          const html = await server.transformIndexHtml(
            `/${page.path}`,
            renderPage(page),
            request.originalUrl,
          );
          response.setHeader("Content-Type", "text/html");
          response.end(html);
        } catch (error) {
          next(error);
        }
      });
    },
    configResolved(config) {
      outDir = config.build.outDir;
    },
    buildStart() {
      enforceReactComponents(pathToFileURL(join(root, "/")));
    },
    transformIndexHtml() {
      return [{ tag: "script", children: bootstrap, injectTo: "head-prepend" }];
    },
    generateBundle(_, bundle) {
      const packages = new Set();
      for (const chunk of Object.values(bundle))
        for (const id of Object.keys(chunk.modules ?? {}))
          if (id.replaceAll("\\", "/").includes("node_modules/"))
            packages.add(packageRoot(id.replaceAll("\\", "/")));
      this.emitFile({
        type: "asset",
        fileName: "THIRD-PARTY-NOTICES.txt",
        source: notices(root, packages),
      });
    },
    closeBundle() {
      for (const top of ["assets", "dls", "tools"])
        cpSync(join(root, top), join(outDir, top), {
          recursive: true,
          force: false,
          filter: (source) => copiedAtRuntime(relative(root, source).split(sep).join("/")),
        });
      writeFileSync(join(outDir, "build.json"), JSON.stringify(build, null, 2));
    },
  };
}
