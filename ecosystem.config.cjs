/** PM2: PrintCalc API + web (bash wrappery kvůli PATH a env). */
const path = require("path");

const root = __dirname;

module.exports = {
  apps: [
    {
      name: "printcalc-api",
      cwd: root,
      script: "start-api.sh",
      interpreter: "/bin/bash",
      env: {
        ASPNETCORE_ENVIRONMENT: "Production",
        ASPNETCORE_URLS: "http://0.0.0.0:5281",
      },
      autorestart: true,
      max_restarts: 15,
      min_uptime: "10s",
      restart_delay: 3000,
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
      restart_delay: 5000,
    },
  ],
};
