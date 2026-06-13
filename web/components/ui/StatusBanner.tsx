type Props = {
  message: string;
  variant?: "warning" | "error" | "info";
};

const styles = {
  warning:
    "border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200",
  error:
    "border-red-300 bg-red-50 text-red-900 dark:border-red-900/60 dark:bg-red-950/40 dark:text-red-200",
  info: "border-zinc-300 bg-zinc-100 text-zinc-700 dark:border-zinc-700 dark:bg-zinc-900/80 dark:text-zinc-300",
};

export function StatusBanner({ message, variant = "warning" }: Props) {
  return (
    <div className={`rounded border px-3 py-2 text-sm ${styles[variant]}`}>{message}</div>
  );
}
