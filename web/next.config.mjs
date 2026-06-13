/** @type {import('next').NextConfig} */
const apiOrigin =
  process.env.PRINTCALC_API_ORIGIN?.replace(/\/$/, "") ||
  "http://localhost:5281";

const nextConfig = {
  /** Požadavky na /api/* přesměruje na běžící PrintCalc.Api (stejný port jako ve WPF stacku). */
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${apiOrigin}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
