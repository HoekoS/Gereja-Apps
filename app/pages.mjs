const toolsEntry = "/dls/react/tools-main.tsx";
const toolNoscript = "Enable JavaScript to use this tool.";

const tool = (id, title, options = {}) => ({
  path: `tools/${id}/index.html`,
  title,
  entry: toolsEntry,
  body: { class: options.bodyClass ?? "rover-tool", "data-tool": id },
  noscript: toolNoscript,
  ...options,
});

export const pages = [
  {
    path: "index.html",
    title: "Gereja Apps",
    entry: "/dls/react/shell-main.tsx",
    noscript: "Enable JavaScript to use Gereja Apps.",
  },
  tool("bible", "Alkitab", {
    styles: ["react-layout.css"],
    bodyClass: "rover-tool tool-bible",
    noscript: "Enable JavaScript to look up a passage.",
  }),
];

const escapeHtml = (text) =>
  text.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll('"', "&quot;");

export function renderPage(page) {
  const id = page.path.replace(/\/?index\.html$/, "").replaceAll("/", "-") || "shell";
  const styles = [...(page.styles ?? []), `/dls/react/styles.css?page=${id}`]
    .map((href) => `<link rel="stylesheet" href="${href}">`)
    .join("");
  const body = Object.entries(page.body ?? {})
    .map(([name, value]) => ` ${name}="${escapeHtml(value)}"`)
    .join("");
  return [
    "<!doctype html>",
    '<html lang="en" data-theme="light"><head><meta charset="utf-8">',
    '<meta name="viewport" content="width=device-width,initial-scale=1">',
    `<title>${escapeHtml(page.title)}</title>`,
    page.head ? `<script>${page.head}</script>` : "",
    styles,
    `<script type="module" src="${page.entry}"></script>`,
    `</head><body${body}><div id="root"></div>`,
    page.noscript ? `<noscript>${escapeHtml(page.noscript)}</noscript>` : "",
    "</body></html>",
  ].join("");
}
