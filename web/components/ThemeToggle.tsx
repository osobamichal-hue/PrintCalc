"use client";

import { useTheme } from "@/components/ThemeProvider";

export function ThemeToggle({ className }: { className?: string }) {
  const { theme, toggleTheme } = useTheme();

  return (
    <button
      type="button"
      onClick={toggleTheme}
      className={
        className ??
        "rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:bg-zinc-800"
      }
      aria-label={theme === "dark" ? "Světlý režim" : "Tmavý režim"}
      title={theme === "dark" ? "Světlý režim" : "Tmavý režim"}
    >
      {theme === "dark" ? "☀ Světlý" : "☾ Tmavý"}
    </button>
  );
}
