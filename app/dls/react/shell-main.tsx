import { createRoot } from "react-dom/client";
import AppShell from "./AppShell";
import { installReactAppearance } from "./appearance";
installReactAppearance();
const root = document.getElementById("root");
if (!root) throw Error("Rover shell root is missing.");
createRoot(root).render(<AppShell />);
