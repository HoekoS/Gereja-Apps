import { useEffect, useState } from "react";
import { LayoutGroup, motion } from "motion/react";
import { MotionPolicy } from "./components/ui/motion";
import { useLayoutTransition } from "./hooks/useLayoutTransition";
import { ChevronLeft, ChevronRight, Menu } from "lucide-react";
import { Button } from "./components/ui/button";
import { cn } from "./lib/utils";
import { navigationGroups, routes, routeForHash, type RouteId } from "./routes";

// Adapted from Matsu's AppShell.tsx: the nav/iframe-per-tool shell is kept,
// but PreferencesMenu/AccountChip are dropped along with it — there is no
// identity/session context ported yet (see AGENTS.md), so there is nothing
// for an account menu to show.
const sidebarKey = "gereja-apps.sidebar";
function initialCollapsed() {
  try {
    return localStorage.getItem(sidebarKey) === "collapsed";
  } catch {
    return false;
  }
}

export default function AppShell() {
  const transitionLayout = useLayoutTransition();
  const [selected, setSelected] = useState<RouteId>(() => routeForHash(location.hash));
  const [visited, setVisited] = useState<Set<RouteId>>(
    () => new Set([routeForHash(location.hash)]),
  );
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  useEffect(() => {
    const navigate = () => {
      const id = routeForHash(location.hash);
      setSelected(id);
      setMobileMenuOpen(false);
      setVisited((previous) => new Set([...previous, id]));
    };
    const toggle = () => transitionLayout(() => setCollapsed((previous) => !previous));
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMobileMenuOpen(false);
    };
    window.addEventListener("hashchange", navigate);
    window.addEventListener("dls-nav-toggle", toggle);
    window.addEventListener("keydown", closeOnEscape);
    return () => {
      window.removeEventListener("hashchange", navigate);
      window.removeEventListener("dls-nav-toggle", toggle);
      window.removeEventListener("keydown", closeOnEscape);
    };
  }, [transitionLayout]);

  useEffect(() => {
    try {
      localStorage.setItem(sidebarKey, collapsed ? "collapsed" : "expanded");
    } catch {}
  }, [collapsed]);

  useEffect(() => {
    document.title = `Gereja Apps · ${routes.find((route) => route.id === selected)?.label}`;
    const frame = document.getElementById(selected) as HTMLIFrameElement | null;
    frame?.contentWindow?.dispatchEvent(new Event("resize"));
    frame?.contentWindow?.dispatchEvent(new Event("support-tool-active"));
  }, [selected]);

  const ToggleIcon = collapsed ? ChevronRight : ChevronLeft;
  return (
    <div
      data-mobile-menu-open={mobileMenuOpen}
      className={cn("rover-shell", collapsed && "rover-shell-collapsed")}
    >
      <aside className="rover-navigation" aria-label="Application navigation">
        <div className="flex min-w-0 items-center justify-between gap-2">
          {!collapsed && (
            <span className="rover-brand flex min-w-0 items-center gap-2 font-semibold">
              <span className="rover-brand-mark">G</span>
              <span className="rover-brand-name">Gereja Apps</span>
            </span>
          )}
          <Button
            variant="ghost"
            className="rover-desktop-toggle shrink-0 px-3"
            onClick={() => transitionLayout(() => setCollapsed((previous) => !previous))}
            aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            aria-controls="app-nav"
            aria-expanded={!collapsed}
            aria-keyshortcuts="Control+B"
          >
            <ToggleIcon aria-hidden="true" />
          </Button>
          <Button
            variant="secondary"
            className="rover-mobile-menu"
            aria-label="Tools menu"
            aria-controls="app-nav"
            aria-expanded={mobileMenuOpen}
            onClick={() => setMobileMenuOpen((previous) => !previous)}
          >
            <Menu aria-hidden="true" />
            Tools
          </Button>
        </div>
        <MotionPolicy>
          <LayoutGroup id="support-navigation">
            <nav id="app-nav" className="rover-route-list" aria-label="Tools">
              {navigationGroups.map((group) => (
                <div key={group} className="rover-route-group grid gap-1">
                  {!collapsed && (
                    <h2 className="px-3 pb-2 text-xs font-medium uppercase tracking-wider text-muted-foreground">
                      {group}
                    </h2>
                  )}
                  {routes
                    .filter((route) => route.group === group)
                    .map((route) => (
                      <a
                        key={route.id}
                        href={`#${route.id}`}
                        onClick={() => setMobileMenuOpen(false)}
                        aria-current={selected === route.id ? "page" : undefined}
                        aria-label={route.label}
                        title={collapsed ? route.label : undefined}
                        className={cn(
                          "flex min-h-11 items-center gap-3 rounded-lg px-3 py-2 text-sm text-muted-foreground hover:bg-secondary hover:text-foreground",
                          selected === route.id && "bg-secondary font-medium text-foreground",
                          collapsed && "justify-center",
                        )}
                      >
                        {selected === route.id && (
                          <motion.span
                            layoutId="active-tool"
                            className="rover-route-selection"
                            aria-hidden="true"
                          />
                        )}
                        <span className="rover-route-icon">
                          <route.icon aria-hidden="true" width={20} height={20} />
                        </span>
                        <span className={collapsed ? "sr-only" : ""}>{route.label}</span>
                      </a>
                    ))}
                </div>
              ))}
            </nav>
          </LayoutGroup>
        </MotionPolicy>
      </aside>
      <main className="rover-content" aria-label="Selected tool">
        {routes.map((route) => (
          <iframe
            key={route.id}
            id={route.id}
            title={route.label}
            src={visited.has(route.id) ? `tools/${route.id}/index.html` : undefined}
            hidden={selected !== route.id}
          />
        ))}
      </main>
    </div>
  );
}
