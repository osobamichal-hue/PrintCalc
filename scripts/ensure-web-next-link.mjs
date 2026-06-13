/**
 * Program Files blokuje zápis — zajistí web/.next a pokusí se o oprávnění k zápisu.
 */
import fs from "fs";
import path from "path";
import { execSync } from "child_process";
import { fileURLToPath } from "url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const webDir = path.join(root, "web");
const nextDir = path.join(webDir, ".next");
const linkPath = nextDir;

if (fs.existsSync(linkPath)) {
  try {
    const stat = fs.lstatSync(linkPath);
    if (stat.isSymbolicLink()) {
      fs.unlinkSync(linkPath);
    }
  } catch {
    /* ignore */
  }
}

fs.mkdirSync(nextDir, { recursive: true });

const user = process.env.USERNAME;
if (user) {
  try {
    execSync(`icacls "${webDir}" /grant "${user}:(OI)(CI)M" /T /C`, { stdio: "pipe" });
    console.log(`[ensure-web-next] Oprávnění k zápisu nastavena pro ${user}`);
  } catch {
    console.warn("[ensure-web-next] icacls selhalo — spusťte terminál jako správce nebo přesuňte projekt mimo Program Files.");
  }
}
