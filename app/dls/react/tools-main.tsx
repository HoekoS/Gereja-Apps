import { createRoot } from "react-dom/client";
import { toolLayouts, type ToolName } from "./Tools";
import { installReactAppearance } from "./appearance";
installReactAppearance();
const node = document.getElementById("root");
const tool = document.body.dataset.tool as ToolName;
if (!node || !toolLayouts[tool]) throw Error("Unknown tool.");
const Layout = toolLayouts[tool];
createRoot(node).render(<Layout />);
