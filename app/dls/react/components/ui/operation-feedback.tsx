import { CheckCircle2, CirclePause, CircleX, LoaderCircle, OctagonX } from "lucide-react";
import { useId } from "react";
import { cn } from "../../lib/utils";

export type OperationState = "idle" | "pending" | "success" | "error" | "cancelled" | "paused";
/** State remains visible without animation; percentages are real measured work only. */
export function OperationFeedback({
  state,
  label,
  detail,
  progress,
  className,
  id: elementId,
}: {
  state: OperationState;
  label: string;
  detail?: string;
  progress?: number;
  className?: string;
  id?: string;
}) {
  const id = useId();
  const Icon = {
    idle: null,
    pending: LoaderCircle,
    success: CheckCircle2,
    error: CircleX,
    cancelled: OctagonX,
    paused: CirclePause,
  }[state];
  const value =
    progress === undefined || !Number.isFinite(progress)
      ? undefined
      : Math.min(100, Math.max(0, progress));
  return (
    <div
      id={elementId}
      className={cn("dls-operation-feedback", className)}
      data-state={state}
      data-determinate={value !== undefined || undefined}
    >
      <div
        role={state === "error" ? "alert" : "status"}
        aria-atomic="true"
        className="dls-operation-heading"
      >
        {Icon && (
          <Icon
            aria-hidden="true"
            className={state === "pending" ? "dls-pending-spinner" : undefined}
          />
        )}
        <span>
          <strong id={id}>{label}</strong>
          {detail && <span className="dls-operation-detail">{detail}</span>}
        </span>
      </div>
      {value !== undefined && (state === "pending" || state === "paused") && (
        <span className="dls-operation-percent" aria-hidden="true">
          {Math.round(value)}%
        </span>
      )}
      {(state === "pending" || state === "paused") && (
        <div
          className="dls-operation-progress"
          role="progressbar"
          aria-labelledby={id}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={value}
          aria-valuetext={value === undefined ? label : `${Math.round(value)}%`}
        >
          <span
            data-indeterminate={value === undefined || undefined}
            style={{ width: value === undefined ? "35%" : `${value}%` }}
          />
        </div>
      )}
    </div>
  );
}
