"use client";



import Link from "next/link";

import { usePathname } from "next/navigation";

import { useState } from "react";

import { ThemeToggle } from "@/components/ThemeToggle";



const nav = [

  { href: "/", label: "Úvod", icon: "⌂" },

  { href: "/customers", label: "Zákazníci", icon: "◎" },

  { href: "/filaments", label: "Filamenty a sklad", icon: "◈" },

  { href: "/printers", label: "Tiskárny", icon: "▣" },

  { href: "/models", label: "Modely", icon: "◇" },

  { href: "/calculations", label: "Kalkulace", icon: "∑" },

  { href: "/quotes", label: "Nabídky", icon: "N" },

  { href: "/orders", label: "Zakázky", icon: "Z" },

  { href: "/invoices", label: "Faktury", icon: "F" },

  { href: "/purchase-invoices", label: "Přijaté FA", icon: "↓" },

  { href: "/statistics", label: "Statistiky", icon: "▤" },

  { href: "/settings", label: "Nastavení", icon: "⚙" },

  { href: "/about", label: "O aplikaci", icon: "i" },

];



export function AppChrome({ children }: { children: React.ReactNode }) {

  const pathname = usePathname();

  const [mobileOpen, setMobileOpen] = useState(false);



  const NavLinks = ({ onNavigate }: { onNavigate?: () => void }) => (

    <nav className="flex flex-col gap-0.5">

      {nav.map((item) => {

        const active =

          item.href === "/"

            ? pathname === "/"

            : pathname === item.href || pathname.startsWith(`${item.href}/`);

        return (

          <Link

            key={item.href}

            href={item.href}

            onClick={onNavigate}

            className={`group flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-all ${

              active

                ? "bg-zinc-200/90 text-zinc-900 shadow-sm ring-1 ring-amber-500/25 dark:bg-zinc-800/90 dark:text-zinc-50"

                : "text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800/50 dark:hover:text-zinc-100"

            }`}

          >

            <span

              className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-sm font-semibold ${

                active

                  ? "bg-amber-500/15 text-amber-600 dark:text-amber-400"

                  : "bg-zinc-200/80 text-zinc-500 group-hover:text-zinc-600 dark:bg-zinc-800/60 dark:text-zinc-500 dark:group-hover:text-zinc-400"

              }`}

              aria-hidden

            >

              {item.icon}

            </span>

            <span className="leading-snug">{item.label}</span>

            {active && (

              <span

                className="ml-auto h-6 w-1 rounded-full bg-amber-500"

                aria-hidden

              />

            )}

          </Link>

        );

      })}

    </nav>

  );



  return (

    <div className="flex min-h-screen flex-col md:flex-row">

      <header className="sticky top-0 z-40 flex items-center justify-between border-b border-zinc-200 bg-white/95 px-4 py-3 backdrop-blur-md dark:border-zinc-800/80 dark:bg-zinc-950/95 md:hidden">

        <Link

          href="/"

          className="font-semibold tracking-tight text-amber-600 dark:text-amber-400"

          onClick={() => setMobileOpen(false)}

        >

          PrintCalc

        </Link>

        <div className="flex items-center gap-2">

          <ThemeToggle />

          <button

            type="button"

            className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:bg-zinc-100 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800"

            aria-expanded={mobileOpen}

            aria-label="Menu"

            onClick={() => setMobileOpen((o) => !o)}

          >

            {mobileOpen ? "Zavřít" : "Menu"}

          </button>

        </div>

      </header>



      {mobileOpen && (

        <div

          className="fixed inset-0 z-30 bg-zinc-900/40 dark:bg-black/60 md:hidden"

          aria-hidden

          onClick={() => setMobileOpen(false)}

        />

      )}



      <aside

        className={`fixed inset-y-0 left-0 z-40 w-72 transform border-r border-zinc-200 bg-gradient-to-b from-zinc-50 to-zinc-100 transition-transform duration-200 ease-out dark:border-zinc-800/80 dark:from-zinc-950 dark:to-zinc-900 md:static md:z-0 md:flex md:w-64 md:translate-x-0 md:flex-col md:border-r ${

          mobileOpen ? "translate-x-0 shadow-2xl" : "-translate-x-full md:translate-x-0"

        }`}

      >

        <div className="flex h-full flex-col p-4 pt-16 md:pt-4">

          <div className="mb-6 hidden items-center justify-between gap-2 md:flex">

            <Link

              href="/"

              className="flex items-center gap-2"

              onClick={() => setMobileOpen(false)}

            >

              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 to-amber-600 text-lg font-bold text-zinc-950 shadow-lg shadow-amber-900/20 dark:shadow-amber-900/40">

                3D

              </span>

              <div>

                <div className="text-lg font-bold tracking-tight text-zinc-900 dark:text-zinc-50">

                  PrintCalc

                </div>

                <div className="text-[10px] font-medium uppercase tracking-widest text-amber-600/90 dark:text-amber-500/90">

                  web

                </div>

              </div>

            </Link>

            <ThemeToggle className="shrink-0 rounded-lg border border-zinc-300 bg-white px-2 py-1.5 text-xs text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:bg-zinc-800" />

          </div>



          <NavLinks onNavigate={() => setMobileOpen(false)} />



          <div className="mt-auto hidden border-t border-zinc-200 pt-4 dark:border-zinc-800/80 md:block">

            <p className="text-[11px] leading-relaxed text-zinc-500 dark:text-zinc-600">

              Lokální provoz · sdílená DB s WPF

            </p>

            <p className="mt-1 text-[10px] text-zinc-400 dark:text-zinc-700">

              Spuštění: <code className="text-zinc-600 dark:text-zinc-500">npm run dev</code> v kořeni

            </p>

          </div>

        </div>

      </aside>



      <main className="relative min-w-0 flex-1 bg-zinc-50 md:bg-gradient-to-br md:from-zinc-50 md:via-zinc-100/80 md:to-zinc-50 dark:bg-zinc-950 dark:md:from-zinc-950 dark:md:via-zinc-900/80 dark:md:to-zinc-950">

        <div

          className="pointer-events-none absolute inset-0 opacity-60 md:opacity-100"

          style={{

            backgroundImage:

              "radial-gradient(ellipse 80% 50% at 50% -20%, rgba(245, 158, 11, 0.06), transparent)",

          }}

        />

        <div className="relative z-10 mx-auto max-w-6xl p-4 sm:p-6 lg:p-8">

          <div className="app-glow min-h-[calc(100vh-8rem)] rounded-2xl border border-zinc-200/80 bg-white/90 p-4 shadow-xl shadow-zinc-300/30 backdrop-blur-sm dark:border-zinc-800/60 dark:bg-zinc-900/40 dark:shadow-2xl dark:shadow-black/40 sm:p-6 md:min-h-[calc(100vh-4rem)]">

            {children}

          </div>

        </div>

      </main>

    </div>

  );

}


