"use client";

type Customer = { id: number; name: string };

type Props = {
  customers: Customer[];
  value: number | "";
  onChange: (id: number | "") => void;
  className?: string;
};

export function CustomerSelect({ customers, value, onChange, className }: Props) {
  return (
    <select
      className={
        className ??
        "w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100"
      }
      value={value}
      onChange={(e) =>
        onChange(e.target.value ? parseInt(e.target.value, 10) : "")
      }
    >
      <option value="">— vyberte zákazníka —</option>
      {customers.map((c) => (
        <option key={c.id} value={c.id}>
          {c.name}
        </option>
      ))}
    </select>
  );
}
