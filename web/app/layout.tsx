import type { Metadata } from "next";
import localFont from "next/font/local";
import { AppChrome } from "@/components/AppChrome";
import { ThemeProvider } from "@/components/ThemeProvider";
import "./globals.css";

const geistSans = localFont({
  src: "./fonts/GeistVF.woff",
  variable: "--font-geist-sans",
  weight: "100 900",
});
const geistMono = localFont({
  src: "./fonts/GeistMonoVF.woff",
  variable: "--font-geist-mono",
  weight: "100 900",
});

export const metadata: Metadata = {
  title: "PrintCalc",
  description: "3D tisk – kalkulace, sklad, nabídky, zakázky, faktury (web)",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="cs" suppressHydrationWarning>
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `(function(){try{var t=localStorage.getItem("printcalc-theme");if(t==="dark")document.documentElement.classList.add("dark");}catch(e){}})();`,
          }}
        />
      </head>
      <body
        className={`${geistSans.variable} ${geistMono.variable} font-[family-name:var(--font-geist-sans)]`}
      >
        <ThemeProvider>
          <AppChrome>{children}</AppChrome>
        </ThemeProvider>
      </body>
    </html>
  );
}
