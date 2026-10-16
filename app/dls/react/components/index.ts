/** Public DLS API, ported from Matsu. Product layouts import here, never
 * underlying primitives. ui/ holds source-owned shadcn-style primitives and
 * native controller adapters.
 *
 * Trimmed from Matsu's own barrel: no AccountChip, PreferencesMenu or
 * SettingsDialog export here — there is no identity/session context ported
 * yet (AGENTS.md), so there is nothing for those to show.
 */
export { Button, buttonVariants } from "./ui/button";
export { OperationFeedback } from "./ui/operation-feedback";
export { MotionList, MotionItem, MotionPolicy } from "./ui/motion";
export type { OperationState } from "./ui/operation-feedback";
export type { ButtonProps } from "./ui/button";
export { Card } from "./ui/card";
export { Input } from "./ui/input";
export { Badge } from "./ui/badge";
export { Select } from "./ui/select";
export { Textarea } from "./ui/textarea";
export { Field } from "./ui/field";
export { Tabs, TabsList, TabsTrigger, TabsContent } from "./ui/tabs";
export {
  Dialog,
  DialogTrigger,
  DialogClose,
  DialogTitle,
  DialogDescription,
  DialogHeader,
  DialogFooter,
  DialogContent,
} from "./ui/dialog";
export { ChoiceInput, RangeInput } from "./ui/choice";
export { SwitchInput } from "./ui/switch";
export { NumberField } from "./ui/number-field";
export { ChipInput } from "./ui/chip-input";
export { Disclosure, SegmentedChoice } from "./ui/disclosure";
export { EntityCard } from "./ui/entity-card";
export { Surface, PageHeader } from "./ui/surface";
export {
  FormField,
  FormFieldset,
  FieldGroup,
  ActionBar,
  ChoiceRow,
  Callout,
  StatusMessage,
  DataTable,
} from "./ui/patterns";
export { IconButton } from "./ui/icon-button";

export { AppearanceRow, ModePicker, ColorReadout, ColorPicker } from "./ui/appearance";
export { ListRow } from "./ui/list-row";

export { CreatableList } from "./ui/creatable-list";

export { ToolSidebar } from "./ToolSidebar";

export { ActionMenu } from "./ui/popover";

export { ViewerModal } from "./ViewerModal";
