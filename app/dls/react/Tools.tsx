import BiblePage from "../../tools/bible/react/BiblePage";
export const toolLayouts = {
  bible: BiblePage,
};
export type ToolName = keyof typeof toolLayouts;
