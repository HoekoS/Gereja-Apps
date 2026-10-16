import test from "node:test";
import { enforceReactComponents } from "./component-policy.mjs";

test("product pages use shared React controls without retired control skins", () => {
  enforceReactComponents(new URL("../../", import.meta.url));
});
