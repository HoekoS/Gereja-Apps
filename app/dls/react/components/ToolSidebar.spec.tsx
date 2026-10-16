import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { it, expect, afterEach } from "vitest";
import { ToolSidebar } from "./ToolSidebar";
import { Button } from "./index";
afterEach(() => localStorage.clear());
it("should remember collapse per tool and retain child state when expanded again", async () => {
  const user = userEvent.setup();
  function Child() {
    const [count, setCount] = useState(0);
    return <Button onClick={() => setCount(count + 1)}>Count {count}</Button>;
  }
  const view = render(
    <ToolSidebar name="Test settings" storageKey="test">
      <Child />
    </ToolSidebar>,
  );
  await user.click(screen.getByRole("button", { name: "Count 0" }));
  await user.click(screen.getByRole("button", { name: "Collapse Test settings" }));
  expect(screen.queryByRole("button", { name: "Count 1" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Expand Test settings" })).toHaveAttribute(
    "aria-expanded",
    "false",
  );
  await user.click(screen.getByRole("button", { name: "Expand Test settings" }));
  expect(screen.getByRole("button", { name: "Count 1" })).toBeVisible();
  await user.click(screen.getByRole("button", { name: "Collapse Test settings" }));
  view.unmount();
  render(
    <ToolSidebar name="Test settings" storageKey="test">
      <Child />
    </ToolSidebar>,
  );
  expect(screen.getByRole("button", { name: "Expand Test settings" })).toBeVisible();
});
