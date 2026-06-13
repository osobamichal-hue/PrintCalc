"use client";

import { useEffect, useRef } from "react";
import * as THREE from "three";
import { STLLoader } from "three/examples/jsm/loaders/STLLoader.js";
import { apiUrl } from "@/lib/api";

type Props = {
  modelId: number | null;
  fileType?: string | null;
  className?: string;
};

export function ModelPreview3D({ modelId, fileType, className }: Props) {
  const mountRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const mount = mountRef.current;
    if (!mount || !modelId) return;

    const type = (fileType ?? "").toUpperCase();
    if (type !== "STL") {
      mount.innerHTML = "";
      return;
    }

    let disposed = false;
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1a1a2e);

    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 10000);
    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    mount.appendChild(renderer.domElement);

    const ambient = new THREE.AmbientLight(0xffffff, 0.6);
    const dir = new THREE.DirectionalLight(0xffffff, 0.8);
    dir.position.set(1, 1, 1);
    scene.add(ambient, dir);

    const resize = () => {
      const w = mount.clientWidth;
      const h = Math.max(200, mount.clientHeight || 240);
      renderer.setSize(w, h);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
    };

    const ro = new ResizeObserver(resize);
    ro.observe(mount);
    resize();

    const loader = new STLLoader();
    loader.load(
      apiUrl(`/api/print-models/${modelId}/file`),
      (geometry) => {
        if (disposed) return;
        geometry.computeBoundingBox();
        geometry.center();
        const mat = new THREE.MeshStandardMaterial({
          color: 0xf59e0b,
          metalness: 0.1,
          roughness: 0.65,
        });
        const mesh = new THREE.Mesh(geometry, mat);
        scene.add(mesh);

        const box = geometry.boundingBox;
        if (box) {
          const size = new THREE.Vector3();
          box.getSize(size);
          const maxDim = Math.max(size.x, size.y, size.z, 1);
          camera.position.set(maxDim * 1.6, maxDim * 1.2, maxDim * 1.6);
          camera.lookAt(0, 0, 0);
        }
      },
      undefined,
      () => {
        if (!disposed) mount.textContent = "Náhled STL se nepodařilo načíst.";
      },
    );

    let frame = 0;
    const animate = () => {
      if (disposed) return;
      frame = requestAnimationFrame(animate);
      scene.rotation.y += 0.004;
      renderer.render(scene, camera);
    };
    animate();

    return () => {
      disposed = true;
      cancelAnimationFrame(frame);
      ro.disconnect();
      renderer.dispose();
      mount.innerHTML = "";
    };
  }, [modelId, fileType]);

  if (!modelId) return null;

  const type = (fileType ?? "").toUpperCase();
  if (type !== "STL") {
    return (
      <p className="text-xs text-zinc-500">
        3D náhled je dostupný pro STL modely (vybraný typ: {type || "—"}).
      </p>
    );
  }

  return (
    <div
      ref={mountRef}
      className={className ?? "h-60 w-full overflow-hidden rounded border border-zinc-300 dark:border-zinc-700"}
    />
  );
}
