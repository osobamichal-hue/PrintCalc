/** PM2: PrintCalc API + web (dva oddělené procesy — spolehlivější než jeden bash wrapper). */
const path = require("path");

const root = __dirname;

module.exports = {
  apps: [
    {
      name: "printcalc-api",
      cwd: root,
      script: "dotnet",
      args: "src/PrintCalc.Api/bin/Release/net8.0/PrintCalc.Api.dll",
      env: {
        ASPNETCORE_ENVIRONMENT: "Production",
        ASPNETCORE_URLS: "http://0.0.0.0:5281",
      },
      autorestart: true,
      max_restarts: 10,
      min_uptime: "15s",
      kill_timeout: 8000,
    },
    {
      name: "printcalc-web",
      cwd: root,
      script: "start-web.sh",
      interpreter: "/bin/bash",
      env: {
        WEB_PORT: "3001",
      },
      autorestart: true,
      max_restarts: 20,
      min_uptime: "10s",
    },
  ],
};
