import { afterEach, expect, it, vi } from "vitest";
import { createUuid } from "./uuid";
afterEach(() => vi.unstubAllGlobals());
it("generates distinct v4 IDs when LAN HTTP has no randomUUID", () => {
  const cryptoProvider = globalThis.crypto;
  vi.stubGlobal("crypto", { getRandomValues: cryptoProvider.getRandomValues.bind(cryptoProvider) });
  const first = createUuid();
  expect(first).toMatch(/^[a-f0-9]{8}-[a-f0-9]{4}-4[a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}$/);
  expect(createUuid()).not.toBe(first);
});
it("uses native UUID on secure origins", () => {
  vi.stubGlobal("crypto", { randomUUID: () => "native-id" });
  expect(createUuid()).toBe("native-id");
});
