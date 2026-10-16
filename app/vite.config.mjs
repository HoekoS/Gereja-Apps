import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "tailwindcss";
import { appRoot, htmlPages, app } from "./scripts/vite-app.mjs";
export default defineConfig({
  base: "./",
  publicDir: false,
  plugins: [app(), react()],
  build: {
    outDir: "public",
    emptyOutDir: true,
    assetsDir: "_app",
    target: "es2022",
    rollupOptions: { input: htmlPages().map((page) => `${appRoot}${page}`.replaceAll("\\", "/")) },
    chunkSizeWarningLimit: 4000,
  },
  server: {
    host: "127.0.0.1",
    port: 8937,
    strictPort: true,
    watch: { ignored: ["**/.local-tools/**", "**/target/**", "**/public/**"] },
    proxy: {
      "/api": { target: "http://127.0.0.1:8942", ws: true },
      "/health": { target: "http://127.0.0.1:8942" },
    },
  },
  css: { postcss: { plugins: [tailwindcss("./dls/react/tailwind.config.js")] } },
  optimizeDeps: {
    include: ["react", "react-dom/client"],
  },
});
