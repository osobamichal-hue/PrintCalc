type Props = {
  title: string;
  description?: string;
  children?: React.ReactNode;
};

export function PageHeader({ title, description, children }: Props) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">{title}</h1>
        {description && <p className="mt-1 text-sm text-zinc-500">{description}</p>}
      </div>
      {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
    </div>
  );
}
