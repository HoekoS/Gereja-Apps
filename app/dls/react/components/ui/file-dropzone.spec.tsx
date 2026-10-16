import { expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { FileDropzone } from "./file-dropzone";
it("delivers accepted files to the existing parser and shows their names", async () => {
  const onFiles = vi.fn(),
    user = userEvent.setup();
  render(<FileDropzone aria-label="Map JSON" accept=".json" onFiles={onFiles} />);
  const file = new File(["{}"], "map.json", { type: "application/json" });
  await user.upload(screen.getByLabelText("Map JSON", { selector: "input" }), file);
  await waitFor(() => expect(onFiles).toHaveBeenCalledWith([file]));
  expect(screen.getByText("map.json")).toBeInTheDocument();
});
it("rejects unsupported formats without invoking the parser", async () => {
  const onFiles = vi.fn(),
    user = userEvent.setup({ applyAccept: false });
  render(<FileDropzone aria-label="Map JSON" accept=".json" onFiles={onFiles} />);
  await user.upload(
    screen.getByLabelText("Map JSON", { selector: "input" }),
    new File(["x"], "wrong.png", { type: "image/png" }),
  );
  expect(await screen.findByRole("alert")).toBeInTheDocument();
  expect(onFiles).not.toHaveBeenCalled();
});

it("exposes one named keyboard interaction rather than a nested file button", async () => {
  const user = userEvent.setup();
  render(<FileDropzone aria-label="Map JSON" accept=".json" onFiles={() => {}} />);
  expect(screen.getAllByRole("button")).toHaveLength(1);
  const zone = screen.getByRole("button", { name: "Map JSON" });
  await user.tab();
  expect(zone).toHaveFocus();
});
