export default function AboutPage() {
  return (
    <div className="max-w-xl space-y-3 text-sm text-zinc-500 dark:text-zinc-400">
      <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-100">O aplikaci</h1>
      <p>
        PrintCalc slouží k evidenci 3D tisku, skladu filamentů, kalkulaci cen a
        dokladům (nabídky, zakázky, faktury). Webová verze sdílí databázi s
        desktopovou aplikací přes backend{" "}
        <code className="rounded bg-zinc-800 px-1">PrintCalc.Api</code>.
      </p>
    </div>
  );
}
