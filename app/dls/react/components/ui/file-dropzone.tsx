import { useState } from "react";
import { useDropzone } from "react-dropzone";
import { UploadCloud, FileCheck2 } from "lucide-react";
import { cn } from "../../lib/utils";

export function FileDropzone({
  id,
  accept = "",
  multiple = false,
  disabled = false,
  directory = false,
  onFiles,
  className,
  ...labels
}: {
  id?: string;
  directory?: boolean;
  accept?: string;
  multiple?: boolean;
  disabled?: boolean;
  onFiles: (files: File[]) => void;
  className?: string;
  "aria-label"?: string;
  "aria-labelledby"?: string;
  "aria-describedby"?: string;
}) {
  const [names, setNames] = useState<string[]>([]),
    [error, setError] = useState("");
  const tokens = accept
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
  const extensions = tokens.filter((item) => item.startsWith("."));
  const types = tokens.filter((item) => !item.startsWith("."));
  const formatLabel = [
    ...new Set(
      tokens.map((item) =>
        (item.startsWith(".") ? item.slice(1) : item.split("/")[1]).toUpperCase(),
      ),
    ),
  ].join(" / ");
  const formats = Object.fromEntries(
    (types.length ? types : ["application/octet-stream"]).map((type) => [type, extensions]),
  );
  const zone = useDropzone({
    disabled,
    multiple,
    accept: tokens.length ? formats : undefined,
    onDrop: (files, rejected) => {
      setError(rejected.length ? "File not accepted. Check the format and number of files." : "");
      if (files.length) {
        setNames(files.map((file) => file.name));
        onFiles(files);
      }
    },
  });
  const Icon = names.length ? FileCheck2 : UploadCloud;
  return (
    <div className={cn("dls-dropzone-wrap", className)}>
      <div
        {...zone.getRootProps({
          role: "button",
          "aria-label": labels["aria-label"] || "Browse files or drop them here",
          "aria-labelledby": labels["aria-labelledby"],
          "aria-describedby": labels["aria-describedby"],
          className: cn("dls-dropzone", zone.isDragActive && "is-dragging"),
          "aria-disabled": disabled,
        })}
      >
        <input
          {...zone.getInputProps({
            id,
            ...(directory ? { webkitdirectory: "", directory: "" } : {}),
            ...labels,
            "aria-hidden": true,
            tabIndex: -1,
          })}
        />
        <Icon aria-hidden="true" />
        <span className="grid gap-1 min-w-0">
          <strong>
            {zone.isDragActive
              ? "Drop files here"
              : names.length
                ? names.join(", ")
                : "Drop files or browse"}
          </strong>
          <span className="text-sm text-muted-foreground">
            {names.length
              ? "Choose files again to replace"
              : `${multiple ? "Multiple files" : "One file"}${formatLabel ? ` · ${formatLabel}` : ""}`}
          </span>
        </span>
      </div>
      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
