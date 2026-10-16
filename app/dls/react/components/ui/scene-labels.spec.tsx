import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { SceneLabels } from "./scene-labels";
it("renders scene data as literal themed text and removes expired labels", () => {
  const view = render(
    <SceneLabels
      above
      labels={[
        {
          id: "rover-a",
          text: "<img src=x>",
          x: 25,
          y: 50,
          status: "Maintenance",
          warning: true,
          emphasis: true,
          stale: true,
        },
      ]}
    />,
  );
  const chip = screen.getByText("<img src=x>").parentElement!;
  expect(chip).toHaveClass(
    "text-popover-foreground",
    "ring-primary",
    "opacity-60",
    "-translate-y-full",
  );
  expect(chip).toHaveStyle({ left: "25px", top: "50px" });
  expect(chip.style.borderColor).toBe("var(--dls-warning)");
  expect(screen.getByText("Maintenance")).toHaveClass("text-muted-foreground");
  expect(document.querySelector("img")).toBeNull();
  view.rerender(<SceneLabels labels={[]} />);
  expect(screen.queryByText("Maintenance")).toBeNull();
});
