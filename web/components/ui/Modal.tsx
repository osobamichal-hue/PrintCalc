"use client";

import { useCallback, useEffect, useId, useRef, useState } from "react";

type Props = {
  open: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  size?: "md" | "lg" | "xl";
};

const SIZE_DEFAULTS = {
  md: { w: 480, h: 420, minW: 320, minH: 240 },
  lg: { w: 672, h: 520, minW: 400, minH: 280 },
  xl: { w: 768, h: 600, minW: 480, minH: 320 },
} as const;

type DragState = {
  pointerId: number;
  startX: number;
  startY: number;
  origX: number;
  origY: number;
};

type ResizeState = {
  pointerId: number;
  startX: number;
  startY: number;
  origW: number;
  origH: number;
};

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

export function Modal({ open, onClose, title, children, footer, size = "lg" }: Props) {
  const titleId = useId();
  const cfg = SIZE_DEFAULTS[size];
  const panelRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef<DragState | null>(null);
  const resizeRef = useRef<ResizeState | null>(null);

  const [pos, setPos] = useState({ x: 80, y: 48 });
  const [dim, setDim] = useState({ w: cfg.w, h: cfg.h });
  const [session, setSession] = useState(0);

  useEffect(() => {
    if (!open) return;
    const w = cfg.w;
    const h = cfg.h;
    setDim({ w, h });
    setPos({
      x: clamp(Math.round((window.innerWidth - w) / 2), 8, Math.max(8, window.innerWidth - w - 8)),
      y: clamp(Math.round((window.innerHeight - h) / 2), 8, Math.max(8, window.innerHeight - h - 8)),
    });
    setSession((s) => s + 1);
  }, [open, cfg.w, cfg.h]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  const onDragMove = useCallback(
    (e: PointerEvent) => {
      const drag = dragRef.current;
      if (!drag || e.pointerId !== drag.pointerId) return;
      const maxX = window.innerWidth - dim.w - 8;
      const maxY = window.innerHeight - 48;
      setPos({
        x: clamp(drag.origX + (e.clientX - drag.startX), 8, Math.max(8, maxX)),
        y: clamp(drag.origY + (e.clientY - drag.startY), 8, Math.max(8, maxY)),
      });
    },
    [dim.w]
  );

  const onDragEnd = useCallback(
    (e: PointerEvent) => {
      if (dragRef.current?.pointerId === e.pointerId) {
        dragRef.current = null;
        window.removeEventListener("pointermove", onDragMove);
        window.removeEventListener("pointerup", onDragEnd);
        window.removeEventListener("pointercancel", onDragEnd);
      }
    },
    [onDragMove]
  );

  const startDrag = (e: React.PointerEvent) => {
    if ((e.target as HTMLElement).closest("button")) return;
    e.preventDefault();
    dragRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      origX: pos.x,
      origY: pos.y,
    };
    window.addEventListener("pointermove", onDragMove);
    window.addEventListener("pointerup", onDragEnd);
    window.addEventListener("pointercancel", onDragEnd);
  };

  const onResizeMove = useCallback(
    (e: PointerEvent) => {
      const resize = resizeRef.current;
      if (!resize || e.pointerId !== resize.pointerId) return;
      const maxW = window.innerWidth - pos.x - 8;
      const maxH = window.innerHeight - pos.y - 8;
      setDim({
        w: clamp(resize.origW + (e.clientX - resize.startX), cfg.minW, Math.max(cfg.minW, maxW)),
        h: clamp(resize.origH + (e.clientY - resize.startY), cfg.minH, Math.max(cfg.minH, maxH)),
      });
    },
    [cfg.minH, cfg.minW, pos.x, pos.y]
  );

  const onResizeEnd = useCallback(
    (e: PointerEvent) => {
      if (resizeRef.current?.pointerId === e.pointerId) {
        resizeRef.current = null;
        window.removeEventListener("pointermove", onResizeMove);
        window.removeEventListener("pointerup", onResizeEnd);
        window.removeEventListener("pointercancel", onResizeEnd);
      }
    },
    [onResizeMove]
  );

  const startResize = (e: React.PointerEvent) => {
    e.preventDefault();
    e.stopPropagation();
    resizeRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      origW: dim.w,
      origH: dim.h,
    };
    window.addEventListener("pointermove", onResizeMove);
    window.addEventListener("pointerup", onResizeEnd);
    window.addEventListener("pointercancel", onResizeEnd);
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 pointer-events-none">
      <div
        className="absolute inset-0 bg-zinc-900/25 dark:bg-black/40 pointer-events-auto"
        aria-hidden
        onClick={onClose}
      />
      <div
        key={session}
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="pointer-events-auto fixed flex flex-col overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-2xl dark:border-zinc-600 dark:bg-zinc-900"
        style={{ left: pos.x, top: pos.y, width: dim.w, height: dim.h }}
        onClick={(e) => e.stopPropagation()}
      >
        <div
          className="flex shrink-0 cursor-grab items-start justify-between gap-3 border-b border-zinc-200 bg-zinc-50 px-4 py-3 active:cursor-grabbing dark:border-zinc-700 dark:bg-zinc-800/80"
          onPointerDown={startDrag}
        >
          <div className="flex min-w-0 items-center gap-2 select-none">
            <span className="text-zinc-400" aria-hidden>
              ⠿
            </span>
            <h2 id={titleId} className="truncate text-lg font-medium text-zinc-900 dark:text-zinc-100">
              {title}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 rounded-lg border border-zinc-300 px-2 py-1 text-sm text-zinc-500 hover:bg-zinc-100 dark:border-zinc-600 dark:text-zinc-400 dark:hover:bg-zinc-700"
            aria-label="Zavřít"
          >
            ✕
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-4">{children}</div>

        {footer && (
          <div className="flex shrink-0 flex-wrap justify-end gap-2 border-t border-zinc-200 bg-zinc-50/80 px-4 py-3 dark:border-zinc-700 dark:bg-zinc-900/80">
            {footer}
          </div>
        )}

        <div
          role="presentation"
          className="absolute right-0 bottom-0 h-4 w-4 cursor-nwse-resize"
          onPointerDown={startResize}
          aria-hidden
        >
          <svg viewBox="0 0 16 16" className="h-full w-full text-zinc-400 dark:text-zinc-500">
            <path fill="currentColor" d="M14 14L8 14L14 8Z" />
            <path fill="currentColor" opacity="0.6" d="M14 14L11 14L14 11Z" />
          </svg>
        </div>
      </div>
    </div>
  );
}
