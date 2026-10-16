import { defineConfig } from "vitest/config";
export default defineConfig({
  oxc: { jsx: { runtime: "automatic" } },
  test: {
    pool: "vmThreads",
    maxWorkers: 1,
    testTimeout: 15000,
    environment: "jsdom",
    include: ["tools/*/react/**/*.spec.tsx", "dls/react/**/*.spec.ts", "dls/react/**/*.spec.tsx"],
    setupFiles: ["dls/react/rtl-setup.ts"],
    environmentOptions: { jsdom: { url: "http://localhost/" } },
  },
});
