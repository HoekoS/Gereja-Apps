import { createContext } from "react";
/** Nested Radix overlays stay inside a native modal top layer. */
export const OverlayHost = createContext<HTMLElement | null>(null);
