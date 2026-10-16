import { spawn } from "node:child_process";
import { access, mkdir, open } from "node:fs/promises";
import { createConnection } from "node:net";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const port = Number(process.env.ROVER_TOOLS_PORT || 8937);
if (!Number.isInteger(port) || port < 1024 || port > 65535)
  throw Error("ROVER_TOOLS_PORT must be between 1024 and 65535.");
const occupied = await new Promise((resolve) => {
  const socket = createConnection({ host: "127.0.0.1", port });
  const finish = (value) => {
    socket.destroy();
    resolve(value);
  };
  socket.once("connect", () => finish(true));
  socket.once("error", () => finish(false));
  socket.setTimeout(1000, () => finish(false));
});
if (occupied) {
  console.log(
    `Port ${port} already has a listener. Open http://127.0.0.1:${port}/; no second server was started.`,
  );
} else {
  const vite = join(root, "node_modules", "vite", "bin", "vite.js");
  await access(vite);
  const logs = join(tmpdir(), `matsu-preview-${port}`);
  await mkdir(logs, { recursive: true });
  const out = await open(join(logs, "server.log"), "a"),
    err = await open(join(logs, "errors.log"), "a");
  try {
    const child = spawn(
      process.execPath,
      [
        vite,
        "--config",
        join(root, "vite.config.mjs"),
        "--host",
        "127.0.0.1",
        "--port",
        String(port),
      ],
      { cwd: root, detached: true, windowsHide: true, stdio: ["ignore", out.fd, err.fd] },
    );
    await new Promise((resolve, reject) => {
      child.once("spawn", resolve);
      child.once("error", reject);
    });
    child.unref();
    console.log(
      `Vite starting: http://127.0.0.1:${port}/ (PID ${child.pid}). Closing this terminal keeps it running.`,
    );
    console.log(`Logs: ${logs}`);
  } finally {
    await out.close();
    await err.close();
  }
}
