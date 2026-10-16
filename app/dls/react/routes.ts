import { BookOpenText } from "lucide-react";
// Add a tool: register it here, in Tools.tsx and in pages.mjs, and add assets/tool-icons/<id>.svg.
export const routes = [
  { id: "bible", label: "Alkitab", group: "Perpustakaan", icon: BookOpenText },
] as const;
export type RouteId = (typeof routes)[number]["id"];
// Groups shown in the sidebar, in order.
export const navigationGroups: string[] = ["Perpustakaan"];
export function routeForHash(hash: string) {
  return routes.find((route) => `#${route.id}` === hash)?.id ?? "bible";
}
