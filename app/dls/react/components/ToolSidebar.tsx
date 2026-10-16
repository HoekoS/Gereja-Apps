import { useEffect, useId, useState, type HTMLAttributes } from "react";
import { motion } from "motion/react";
import { useMotionPreference } from "../hooks/useMotionPreference";
import { useLayoutTransition } from "../hooks/useLayoutTransition";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { IconButton } from "./ui/icon-button";
import { cn } from "../lib/utils";

/** Collapsing hides controls without unmounting a running tool. */
export function ToolSidebar({
  name,
  storageKey,
  toggleSide = "left",
  className,
  children,
  ...props
}: HTMLAttributes<HTMLElement> & {
  name: string;
  storageKey: string;
  toggleSide?: "left" | "right";
}) {
  const id = useId(),
    key = `matsu.panel.${storageKey}`;
  const reduced = useMotionPreference();
  const transition = useLayoutTransition();
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(key) === "collapsed";
    } catch {
      return false;
    }
  });
  useEffect(() => {
    try {
      localStorage.setItem(key, collapsed ? "collapsed" : "expanded");
    } catch {
      /* Storage is optional. */
    }
    window.dispatchEvent(new Event("resize"));
  }, [collapsed, key]);
  return (
    <aside
      {...props}
      className="dls-tool-sidebar"
      aria-label={name}
      data-dls-sidebar={name}
      data-toggle-side={toggleSide}
      data-collapsed={collapsed ? "" : undefined}
    >
      <IconButton
        className="dls-tool-sidebar-toggle"
        label={`${collapsed ? "Expand" : "Collapse"} ${name}`}
        title={`${collapsed ? "Expand" : "Collapse"} ${name}`}
        aria-expanded={!collapsed}
        aria-controls={id}
        onClick={() => transition(() => setCollapsed((value) => !value))}
      >
        {collapsed ? <ChevronLeft aria-hidden="true" /> : <ChevronRight aria-hidden="true" />}
      </IconButton>
      <motion.div
        id={id}
        hidden={collapsed}
        initial={false}
        animate={{ opacity: collapsed ? 0 : 1, x: collapsed || reduced ? 0 : [8, 0] }}
        transition={{ duration: reduced ? 0 : 0.2, ease: [0.16, 1, 0.3, 1] }}
        className={cn("dls-sidebar-content", className)}
      >
        {children}
      </motion.div>
    </aside>
  );
}
