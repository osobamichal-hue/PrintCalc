/**
 * Výchozí: relativní cesta přes Next.js rewrite → PrintCalc.Api (localhost:5281).
 * Volitelně NEXT_PUBLIC_API_BASE_URL pro přímé volání API (bez proxy).
 */
export function apiUrl(path: string): string {
  const p = path.startsWith("/") ? path : `/${path}`;
  const base = process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
  if (base) return `${base}${p}`;
  return p;
}

function parseApiError(statusText: string, body: string): string {
  const trimmed = body.trim();
  if (trimmed.startsWith("{")) {
    try {
      const j = JSON.parse(trimmed) as { error?: string; title?: string };
      if (j.error) return j.error;
      if (j.title) return j.title;
    } catch {
      /* ignore */
    }
  }
  if (
    trimmed.includes("FOREIGN KEY") ||
    trimmed.includes("DbUpdateException") ||
    trimmed.includes("SQLite Error 19")
  ) {
    return "Operaci nelze dokončit — záznam je navázán na jiná data.";
  }
  if (trimmed.startsWith("<") || trimmed.length > 280) {
    return `${statusText || "Chyba serveru"} (${trimmed.slice(0, 120)}…)`;
  }
  return trimmed || statusText;
}

export async function apiJson<T>(path: string, init?: RequestInit): Promise<T> {
  const r = await fetch(apiUrl(path), {
    ...init,
    headers: {
      Accept: "application/json",
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
  });
  if (!r.ok) {
    const t = await r.text();
    throw new Error(parseApiError(r.statusText, t));
  }
  if (r.status === 204) return undefined as T;
  return r.json() as Promise<T>;
}

export async function apiVoid(path: string, init?: RequestInit): Promise<void> {
  const r = await fetch(apiUrl(path), {
    ...init,
    headers: {
      Accept: "application/json",
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
  });
  if (!r.ok) {
    const t = await r.text();
    throw new Error(parseApiError(r.statusText, t));
  }
}

export function downloadUrl(path: string): string {
  return apiUrl(path);
}

export async function apiForm<T>(path: string, formData: FormData): Promise<T> {
  const r = await fetch(apiUrl(path), {
    method: "POST",
    body: formData,
  });
  if (!r.ok) {
    const t = await r.text();
    throw new Error(parseApiError(r.statusText, t));
  }
  return r.json() as Promise<T>;
}
