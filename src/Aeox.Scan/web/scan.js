import * as THREE from "three";
import { EffectComposer } from "three/addons/postprocessing/EffectComposer.js";
import { RenderPass } from "three/addons/postprocessing/RenderPass.js";
import { ShaderPass } from "three/addons/postprocessing/ShaderPass.js";
import { OutputPass } from "three/addons/postprocessing/OutputPass.js";

const host = window.chrome && window.chrome.webview;
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

const DitherShader = {
  uniforms: {
    tDiffuse: { value: null },
    uInk: { value: new THREE.Color("#e8e6e1") },
    uBg: { value: new THREE.Color("#0b0b0c") },
    uAccent: { value: new THREE.Color("#7c7aff") },
    uWarn: { value: new THREE.Color("#f2c46b") }
  },
  vertexShader: `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,
  fragmentShader: `
    uniform sampler2D tDiffuse;
    uniform vec3 uInk;
    uniform vec3 uBg;
    uniform vec3 uAccent;
    uniform vec3 uWarn;
    varying vec2 vUv;
    float bayer(vec2 p) {
      int x = int(mod(p.x, 4.0));
      int y = int(mod(p.y, 4.0));
      int i = x + y * 4;
      float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
      return (m[i] + 0.5) / 16.0;
    }
    void main() {
      vec3 c = texture2D(tDiffuse, vUv).rgb;
      float l = dot(c, vec3(0.299, 0.587, 0.114));
      float t = bayer(gl_FragCoord.xy);
      vec3 on = uInk;
      if (c.b - c.r > 0.05) on = uAccent;
      else if (c.r - c.b > 0.05) on = uWarn;
      gl_FragColor = vec4(l * 1.5 > t ? on : uBg, 1.0);
    }
  `
};

const GREY = new THREE.Color("#9a9a9a");
const LIMIT = new THREE.Color("#4a4cff");
const WARN = new THREE.Color("#b07a1a");

function mat(status) {
  const color = status === "limit" ? LIMIT : status === "warn" ? WARN : GREY;
  return new THREE.MeshLambertMaterial({ color });
}

function box(w, h, d, m) {
  return new THREE.Mesh(new THREE.BoxGeometry(w, h, d), m);
}

function buildPart(kind, status) {
  const g = new THREE.Group();
  const m = mat(status);
  const dark = new THREE.MeshLambertMaterial({ color: status === "limit" ? LIMIT.clone().multiplyScalar(0.5) : status === "warn" ? WARN.clone().multiplyScalar(0.5) : new THREE.Color("#4a4a4a") });
  if (kind === "monitor") {
    g.add(box(4.2, 2.5, 0.12, m));
    const screen = box(3.95, 2.25, 0.02, dark);
    screen.position.z = 0.07;
    g.add(screen);
    const neck = box(0.18, 0.9, 0.12, m);
    neck.position.set(0, -1.6, -0.05);
    g.add(neck);
    const foot = box(1.4, 0.06, 0.7, m);
    foot.position.set(0, -2.05, 0.05);
    g.add(foot);
  } else if (kind === "gpu") {
    g.add(box(3.0, 0.55, 1.15, m));
    for (const x of [-0.95, 0, 0.95]) {
      const fan = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.42, 0.08, 24), dark);
      fan.rotation.x = Math.PI / 2;
      fan.position.set(x, 0, 0.6);
      g.add(fan);
    }
    const bracket = box(0.06, 0.9, 1.15, m);
    bracket.position.set(-1.55, 0.15, 0);
    g.add(bracket);
  } else if (kind === "cpu") {
    g.add(box(1.3, 0.12, 1.3, m));
    const lid = box(0.95, 0.1, 0.95, dark);
    lid.position.y = 0.11;
    g.add(lid);
    for (let i = -4; i <= 4; i++) {
      const pin = box(0.04, 0.06, 0.04, dark);
      pin.position.set(i * 0.14, -0.09, 0.62);
      g.add(pin);
    }
  } else if (kind === "ram") {
    for (let i = 0; i < 4; i++) {
      const stick = box(0.12, 1.1, 2.2, m);
      stick.position.x = i * 0.32 - 0.48;
      g.add(stick);
      const chip = box(0.14, 0.3, 1.8, dark);
      chip.position.set(i * 0.32 - 0.48, 0.1, 0);
      g.add(chip);
    }
  } else if (kind === "ssd") {
    g.add(box(1.8, 0.08, 0.5, m));
    for (const x of [-0.45, 0.15, 0.6]) {
      const chip = box(0.4, 0.06, 0.38, dark);
      chip.position.set(x, 0.07, 0);
      g.add(chip);
    }
  } else if (kind === "network") {
    g.add(box(1.6, 0.35, 1.0, m));
    for (const x of [-0.55, 0.55]) {
      const ant = box(0.06, 1.0, 0.06, m);
      ant.position.set(x, 0.65, -0.4);
      g.add(ant);
    }
    for (let i = 1; i <= 3; i++) {
      const arc = new THREE.Mesh(new THREE.TorusGeometry(0.35 * i, 0.03, 6, 32, Math.PI / 2), dark);
      arc.rotation.z = Math.PI / 4;
      arc.position.set(0, 1.2, -0.4);
      g.add(arc);
    }
  }
  return g;
}

const LAYOUT = {
  monitor: { pos: [0, 1.9, -2.4], label: [0, 3.35, -2.4] },
  gpu: { pos: [-2.6, 0.1, 0.4], label: [-2.6, 0.9, 0.4] },
  cpu: { pos: [0, 0, 0.8], label: [0, 0.6, 0.8] },
  ram: { pos: [1.9, 0.4, 0.3], label: [1.9, 1.25, 0.3] },
  ssd: { pos: [3.6, 0, 1.2], label: [3.6, 0.45, 1.2] },
  network: { pos: [-4.4, 0.1, -0.8], label: [-4.4, 1.9, -0.8] }
};

const canvas = document.getElementById("scene");
const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, preserveDrawingBuffer: true });
renderer.setPixelRatio(1 / 2.5);
const scene = new THREE.Scene();
scene.background = new THREE.Color("#0b0b0c");
scene.add(new THREE.AmbientLight(0xffffff, 0.55));
const sun = new THREE.DirectionalLight(0xffffff, 1.6);
sun.position.set(3, 6, 5);
scene.add(sun);

const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 100);
camera.position.set(0, 3.2, 11);
camera.lookAt(0, 0.6, 0);

const floor = new THREE.GridHelper(16, 32, 0x3a3a3a, 0x222222);
floor.position.y = -0.35;
scene.add(floor);

const rig = new THREE.Group();
scene.add(rig);

const composer = new EffectComposer(renderer);
composer.addPass(new RenderPass(scene, camera));
composer.addPass(new ShaderPass(DitherShader));
composer.addPass(new OutputPass());

const labelsEl = document.getElementById("labels");
let parts = [];
let hovered = null;
let lastData = null;
const TAG = { ok: "ok", warn: "weak", limit: "bottleneck" };

function render(data) {
  rig.clear();
  labelsEl.innerHTML = "";
  parts = [];
  lastData = data;
  document.getElementById("verdict").textContent = data.verdict;
  const used = data.matchesUsed > 0 ? `based on your last ${data.matchesUsed} matches` + (data.avgFps ? ` · ${Math.round(data.avgFps)} fps average` : "") : "no match data yet";
  document.getElementById("sub").textContent = used;

  const list = document.getElementById("parts");
  list.innerHTML = "";
  for (const p of data.parts) {
    const spot = LAYOUT[p.kind];
    if (!spot) continue;
    const group = buildPart(p.kind, p.status);
    group.position.fromArray(spot.pos);
    group.userData = p;
    rig.add(group);

    const label = document.createElement("div");
    label.className = `label ${p.status}`;
    label.innerHTML = `<b>[${p.kind}]</b> ${escapeHtml(p.title)}`;
    labelsEl.appendChild(label);

    const li = document.createElement("li");
    li.className = p.status;
    li.innerHTML = `<span class="tag">[${TAG[p.status] ?? "ok"}]</span><span class="title">${escapeHtml(p.kind)} · ${escapeHtml(p.title)}</span><span class="line">${escapeHtml(p.detail)}</span>`;
    li.addEventListener("mouseenter", () => select(group));
    li.addEventListener("mouseleave", () => select(null));
    list.appendChild(li);

    parts.push({ group, label, li, anchor: new THREE.Vector3().fromArray(spot.label) });
  }
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
}

function select(group) {
  hovered = group;
  const detail = document.getElementById("detail");
  for (const p of parts) p.li.classList.toggle("active", p.group === group);
  if (!group) {
    detail.hidden = true;
    return;
  }
  const d = group.userData;
  document.getElementById("detailKind").textContent = `[${d.kind}]`;
  document.getElementById("detailTitle").textContent = d.title;
  document.getElementById("detailLine").textContent = d.detail;
  document.getElementById("detailNote").textContent = d.note;
  detail.hidden = false;
}

const raycaster = new THREE.Raycaster();
const ndc = new THREE.Vector2();
canvas.addEventListener("pointermove", (e) => {
  const r = canvas.getBoundingClientRect();
  ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
  raycaster.setFromCamera(ndc, camera);
  const hit = raycaster.intersectObjects(rig.children, true)[0];
  let g = hit ? hit.object : null;
  while (g && g.parent !== rig) g = g.parent;
  if (g !== hovered) select(g);
  pointer.tx = ndc.x;
});
canvas.addEventListener("pointerleave", () => select(null));

const pointer = { x: 0, tx: 0 };

function resize() {
  const w = window.innerWidth;
  const h = window.innerHeight;
  renderer.setSize(w, h, false);
  composer.setSize(w, h);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
}
window.addEventListener("resize", resize);
resize();

const v = new THREE.Vector3();
function frame(time) {
  pointer.x += (pointer.tx - pointer.x) * 0.05;
  rig.rotation.y = reduceMotion ? 0 : Math.sin(time * 0.00025) * 0.18 + pointer.x * 0.12;
  for (const p of parts) {
    const target = p.group === hovered ? 1.12 : 1;
    const s = p.group.scale.x + (target - p.group.scale.x) * 0.15;
    p.group.scale.setScalar(s);
    if (!reduceMotion) p.group.position.y = LAYOUT[p.group.userData.kind].pos[1] + Math.sin(time * 0.0012 + p.anchor.x) * 0.05;
    v.copy(p.anchor).applyMatrix4(rig.matrixWorld).project(camera);
    p.label.style.left = `${(v.x * 0.5 + 0.5) * window.innerWidth}px`;
    p.label.style.top = `${(-v.y * 0.5 + 0.5) * window.innerHeight}px`;
  }
  composer.render();
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

function makeCard() {
  const w = 1200;
  const h = 630;
  const out = document.createElement("canvas");
  out.width = w;
  out.height = h;
  const ctx = out.getContext("2d");
  ctx.fillStyle = "#0b0b0c";
  ctx.fillRect(0, 0, w, h);
  ctx.imageSmoothingEnabled = false;
  const sw = 660;
  const sh = 600;
  const spin = rig.rotation.y;
  rig.rotation.y = -0.12;
  for (const p of parts) p.group.scale.setScalar(1);
  renderer.setSize(sw, sh, false);
  composer.setSize(sw, sh);
  camera.aspect = sw / sh;
  camera.updateProjectionMatrix();
  composer.render();
  ctx.drawImage(canvas, 530, 15, sw, sh);
  rig.rotation.y = spin;
  resize();
  ctx.fillStyle = "#e8e6e1";
  ctx.font = "bold 22px Cascadia Mono, Consolas, monospace";
  ctx.fillText("AEOX  scan", 36, 56);
  ctx.font = "14px Cascadia Mono, Consolas, monospace";
  let y = 104;
  for (const p of parts) {
    const d = p.group.userData;
    ctx.fillStyle = d.status === "limit" ? "#7c7aff" : d.status === "warn" ? "#f2c46b" : "#9be38f";
    ctx.fillText(`[${TAG[d.status] ?? "ok"}]`, 36, y);
    ctx.fillStyle = "#e8e6e1";
    ctx.fillText(`${d.kind} · ${d.title}`.slice(0, 36), 156, y);
    ctx.fillStyle = "#8f8c86";
    ctx.fillText(d.detail.slice(0, 46), 156, y + 20);
    y += 56;
  }
  if (lastData) {
    ctx.fillStyle = "#e8e6e1";
    y += 10;
    for (const line of wrap(lastData.verdict, 46)) {
      ctx.fillText(line, 36, y);
      y += 20;
    }
    if (lastData.avgFps) {
      ctx.fillStyle = "#8f8c86";
      ctx.fillText(`${Math.round(lastData.avgFps)} fps average over ${lastData.matchesUsed} matches`, 36, y + 6);
    }
  }
  ctx.fillStyle = "#55534f";
  ctx.fillText("made by valorety", 36, h - 32);
  return out.toDataURL("image/png");
}

function wrap(text, max) {
  const lines = [];
  let cur = "";
  for (const word of String(text).split(" ")) {
    if (cur && (cur + " " + word).length > max) {
      lines.push(cur);
      cur = word;
    } else cur = cur ? cur + " " + word : word;
  }
  if (cur) lines.push(cur);
  return lines;
}

if (host) {
  host.addEventListener("message", (e) => {
    const msg = e.data;
    if (msg && msg.type === "scan") render(msg.data);
    if (msg && msg.type === "card") host.postMessage({ type: "card", data: makeCard() });
  });
  host.postMessage({ type: "ready" });
} else {
  render({
    verdict: "your gpu sets the pace at 4.6 ms per frame. lower render scale or resolution for more fps.",
    matchesUsed: 10,
    avgFps: 246,
    bottleneck: "gpu",
    parts: [
      { kind: "cpu", title: "amd ryzen 9 7900x3d 12-core", detail: "12 cores / 24 threads · 4.4 ghz · 3d v-cache", status: "ok", note: "3.9 ms per frame (game 3.6, render 3.9)" },
      { kind: "gpu", title: "geforce rtx 4080 super", detail: "16 gb vram", status: "limit", note: "4.6 ms per frame" },
      { kind: "ram", title: "64 gb", detail: "4 sticks · 6000 mt/s", status: "ok", note: "running at rated speed" },
      { kind: "ssd", title: "samsung ssd 990 pro", detail: "nvme ssd", status: "ok", note: "fast enough for streaming assets" },
      { kind: "monitor", title: "1920×1080", detail: "239 hz", status: "ok", note: "246 fps average vs 239 hz" },
      { kind: "network", title: "wi-fi", detail: "144 mbps", status: "warn", note: "4.1 ms jitter in your matches" }
    ]
  });
  window.aeoxCard = makeCard;
}
