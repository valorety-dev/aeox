import * as THREE from "three";

const bridge = window.chrome?.webview ?? null;
const send = (type, data) => bridge?.postMessage({ type, data });
const $ = (id) => document.getElementById(id);
const DEG = Math.PI / 180;

const GAMES = {
  valorant: { name: "valorant", yaw: 0.07, fov: 103, unit: "sensitivity" },
  cs2: { name: "counter-strike 2", yaw: 0.022, fov: 106.26, unit: "sensitivity" },
  apex: { name: "apex legends", yaw: 0.022, fov: 106.26, unit: "sensitivity" },
  fortnite: { name: "fortnite", yaw: 0.005555, fov: 80, unit: "sensitivity %" },
  overwatch: { name: "overwatch 2", yaw: 0.0066, fov: 103, unit: "sensitivity" },
  cod: { name: "call of duty", yaw: 0.0066, fov: 80, unit: "sensitivity" },
  custom: { name: "any game (cm/360)", yaw: null, fov: 103, unit: "" }
};

const SIZE = { small: [0.9, 1.3], medium: [1.5, 2.1], large: [2.4, 3.2] };
const DIST = { near: [4, 10], mid: [10, 24], far: [24, 48] };
const SPEED = { slow: [18, 32], fast: [40, 70] };

const PRESETS = [
  { id: "gridshot", name: "gridshot", note: "three big targets, fast clicks", kind: "flick", count: 3, duration: 60,
    size: { small: 0, medium: 1, large: 3 }, dist: { near: 3, mid: 2, far: 0 }, dir: { left: 1, right: 1, up: 1, down: 1 } },
  { id: "precision", name: "precision", note: "small and medium targets close together", kind: "flick", count: 3, duration: 60,
    size: { small: 2, medium: 2, large: 0 }, dist: { near: 2, mid: 2, far: 0 }, dir: { left: 1, right: 1, up: 1, down: 1 } },
  { id: "flick", name: "flick", note: "one target, long flicks", kind: "flick", count: 1, duration: 60,
    size: { small: 1, medium: 2, large: 1 }, dist: { near: 0, mid: 2, far: 3 }, dir: { left: 1, right: 1, up: 1, down: 1 } },
  { id: "microflick", name: "microflick", note: "tiny targets right next to you", kind: "flick", count: 1, duration: 60,
    size: { small: 3, medium: 1, large: 0 }, dist: { near: 3, mid: 1, far: 0 }, dir: { left: 1, right: 1, up: 1, down: 1 } },
  { id: "switching", name: "switching", note: "spread out targets, switch fast", kind: "flick", count: 4, duration: 60,
    size: { small: 1, medium: 2, large: 0 }, dist: { near: 0, mid: 2, far: 2 }, dir: { left: 1, right: 1, up: 1, down: 1 } },
  { id: "tracking", name: "tracking", note: "stay on a strafing target", kind: "tracking", count: 1, duration: 45,
    size: { small: 0, medium: 2, large: 1 }, speed: { slow: 1, fast: 1 }, axis: { h: 3, v: 1 } }
];

const defaults = () => ({
  version: 1,
  game: "valorant",
  sens: { valorant: 0.4, cs2: 1.25, apex: 1.5, fortnite: 6.4, overwatch: 4.5, cod: 6, custom: 0 },
  fov: {},
  cm360: 35,
  dpi: 800,
  flick: {},
  track: {},
  runs: [],
  maps: []
});

let profile = defaults();
let imports = [];
let saveTimer = 0;

function store() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    if (bridge) send("save", profile);
    else try { localStorage.setItem("aeox-aim", JSON.stringify(profile)); } catch { }
  }, 300);
}

function load(data) {
  const base = defaults();
  if (data && typeof data === "object") profile = { ...base, ...data, sens: { ...base.sens, ...(data.sens ?? {}) }, fov: { ...(data.fov ?? {}) } };
}

function degPerCount() {
  if (profile.game === "custom") return 360 / ((profile.cm360 / 2.54) * profile.dpi);
  const g = GAMES[profile.game];
  const imp = imports.find((i) => i.game === profile.game && i.yaw);
  return profile.sens[profile.game] * (imp?.yaw ?? g.yaw);
}

function hfov() {
  return profile.fov[profile.game] ?? GAMES[profile.game].fov;
}

const canvas = $("scene");
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: "high-performance" });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMappingExposure = 1.35;

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x1d1d20);
scene.fog = new THREE.Fog(0x1d1d20, 45, 95);

const camera = new THREE.PerspectiveCamera(70, 1, 0.05, 200);
camera.position.set(0, 1.7, 7);
camera.rotation.order = "YXZ";
let yaw = 0;
let pitch = 0;

function gridTexture(size, cells, base, line, repeatX, repeatY) {
  const c = document.createElement("canvas");
  c.width = c.height = size;
  const x = c.getContext("2d");
  x.fillStyle = base;
  x.fillRect(0, 0, size, size);
  x.strokeStyle = line;
  x.lineWidth = 2;
  const step = size / cells;
  for (let i = 0; i <= cells; i++) {
    x.beginPath(); x.moveTo(i * step, 0); x.lineTo(i * step, size); x.stroke();
    x.beginPath(); x.moveTo(0, i * step); x.lineTo(size, i * step); x.stroke();
  }
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.repeat.set(repeatX, repeatY);
  t.anisotropy = 8;
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

function surface(w, h, base, line, cell) {
  return new THREE.MeshStandardMaterial({ map: gridTexture(512, 4, base, line, w / (cell * 4), h / (cell * 4)), roughness: 0.92, metalness: 0 });
}

function room() {
  const g = new THREE.Group();
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(80, 80), surface(80, 80, "#3a3a40", "#5b5b65", 2.5));
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  g.add(floor);
  const wall = (w, h, x, y, z, ry) => {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), surface(w, h, "#38383e", "#4d4d56", 1.3));
    m.position.set(x, y, z);
    m.rotation.y = ry;
    m.receiveShadow = true;
    g.add(m);
  };
  wall(30, 14, 0, 7, -22, 0);
  wall(32, 14, -15, 7, -6, Math.PI / 2);
  wall(32, 14, 15, 7, -6, -Math.PI / 2);
  wall(12, 14, -21, 7, -22, 0);
  wall(12, 14, 21, 7, -22, 0);
  const block = (w, h, d, x, z) => {
    const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), surface(Math.max(w, d), h, "#3b3b42", "#52525b", 1.3));
    m.position.set(x, h / 2, z);
    m.castShadow = m.receiveShadow = true;
    g.add(m);
  };
  block(6, 4, 10, -12, -16);
  block(6, 2.5, 8, 12, -12);
  block(3, 1, 3, -6, -19);
  return g;
}
scene.add(room());

scene.add(new THREE.HemisphereLight(0xffffff, 0x3a3a42, 1.5));
scene.add(new THREE.AmbientLight(0xffffff, 0.25));
const sun = new THREE.DirectionalLight(0xffffff, 1.3);
sun.position.set(6, 18, 10);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -30; sun.shadow.camera.right = 30; sun.shadow.camera.top = 30; sun.shadow.camera.bottom = -30;
sun.shadow.radius = 6;
scene.add(sun);

const sphereGeo = new THREE.SphereGeometry(1, 48, 32);
const whiteMat = new THREE.MeshStandardMaterial({ color: 0xf2f1ee, roughness: 0.38, metalness: 0 });
const accentMat = new THREE.MeshStandardMaterial({ color: 0x9b8cff, roughness: 0.32, metalness: 0, emissive: 0x2a1f66, emissiveIntensity: 0.35 });

function resize() {
  const w = window.innerWidth;
  const h = window.innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  const v = 2 * Math.atan(Math.tan((hfov() * DEG) / 2) / (16 / 9));
  camera.fov = v / DEG;
  camera.updateProjectionMatrix();
}
window.addEventListener("resize", resize);

function forward() {
  return new THREE.Vector3(0, 0, -1).applyEuler(new THREE.Euler(pitch, yaw, 0, "YXZ"));
}

function angleTo(dir, pos) {
  const v = pos.clone().sub(camera.position).normalize();
  return Math.acos(THREE.MathUtils.clamp(dir.dot(v), -1, 1)) / DEG;
}

function pick(weights) {
  const entries = Object.entries(weights).filter(([, w]) => w > 0);
  if (entries.length === 0) return Object.keys(weights)[0];
  let r = Math.random() * entries.reduce((s, [, w]) => s + w, 0);
  for (const [k, w] of entries) { r -= w; if (r <= 0) return k; }
  return entries[entries.length - 1][0];
}

const rand = ([a, b]) => a + Math.random() * (b - a);

let run = null;

function spawnFlick(map, highlight) {
  const sizeKey = pick(map.size);
  const radiusDeg = rand(SIZE[sizeKey]);
  const distance = 11 + Math.random() * 5;
  let ty = 0, tp = 0;
  for (let attempt = 0; attempt < 14; attempt++) {
    const distKey = pick(map.dist);
    const dirKey = pick(map.dir);
    const offset = rand(DIST[distKey]);
    const spread = (Math.random() - 0.5) * 0.9;
    let dx, dy;
    if (dirKey === "left" || dirKey === "right") { dx = (dirKey === "left" ? 1 : -1) * offset; dy = offset * spread * 0.6; }
    else { dy = (dirKey === "up" ? 1 : -1) * offset * 0.75; dx = offset * spread; }
    ty = yaw / DEG + dx;
    tp = pitch / DEG + dy;
    if (Math.abs(ty) > 60) ty = yaw / DEG - dx;
    if (tp > 30 || tp < -4) tp = pitch / DEG - dy;
    ty = THREE.MathUtils.clamp(ty, -60, 60);
    tp = THREE.MathUtils.clamp(tp, -4, 30);
    const dir = new THREE.Vector3(0, 0, -1).applyEuler(new THREE.Euler(tp * DEG, ty * DEG, 0, "YXZ"));
    const clear = (run?.targets ?? []).every((t) => {
      const v = t.position.clone().sub(camera.position).normalize();
      return Math.acos(THREE.MathUtils.clamp(dir.dot(v), -1, 1)) / DEG > (radiusDeg + t.userData.radiusDeg) * 1.6;
    });
    if (clear) break;
  }
  const dir = new THREE.Vector3(0, 0, -1).applyEuler(new THREE.Euler(tp * DEG, ty * DEG, 0, "YXZ"));
  const pos = camera.position.clone().add(dir.multiplyScalar(distance));
  const r = distance * Math.tan(radiusDeg * DEG);
  pos.y = Math.max(pos.y, r + 0.3);
  const mesh = new THREE.Mesh(sphereGeo, highlight ? accentMat : whiteMat);
  mesh.scale.setScalar(r);
  mesh.position.copy(pos);
  mesh.castShadow = true;
  mesh.userData = { sizeKey, radiusDeg, born: performance.now() };
  scene.add(mesh);
  return mesh;
}
function spawnTracker(map) {
  const sizeKey = pick(map.size);
  const radiusDeg = rand(SIZE[sizeKey]);
  const distance = 13;
  const r = distance * Math.tan(radiusDeg * DEG);
  const mesh = new THREE.Mesh(sphereGeo, accentMat.clone());
  mesh.scale.setScalar(r);
  mesh.castShadow = true;
  mesh.userData = { sizeKey, radiusDeg, ty: 0, tp: 4, vy: 0, vp: 0, next: 0, distance, speedKey: "slow", axisKey: "h" };
  scene.add(mesh);
  retarget(mesh, map);
  placeTracker(mesh);
  return mesh;
}

function retarget(mesh, map) {
  const u = mesh.userData;
  u.speedKey = pick(map.speed ?? { slow: 1, fast: 1 });
  u.axisKey = pick(map.axis ?? { h: 3, v: 1 });
  const s = rand(SPEED[u.speedKey]) * (Math.random() < 0.5 ? -1 : 1);
  if (u.axisKey === "h") { u.vy = s; u.vp = s * (Math.random() - 0.5) * 0.3; }
  else { u.vp = s * 0.7; u.vy = s * (Math.random() - 0.5) * 0.4; }
  u.next = performance.now() + 350 + Math.random() * 900;
}

function placeTracker(mesh) {
  const u = mesh.userData;
  const dir = new THREE.Vector3(0, 0, -1).applyEuler(new THREE.Euler(u.tp * DEG, u.ty * DEG, 0, "YXZ"));
  mesh.position.copy(camera.position).add(dir.multiplyScalar(u.distance));
}

function updateTracker(mesh, dt, map) {
  const u = mesh.userData;
  if (performance.now() > u.next) retarget(mesh, map);
  u.ty += u.vy * dt;
  u.tp += u.vp * dt;
  if (u.ty > 45 || u.ty < -45) { u.vy = -u.vy; u.ty = THREE.MathUtils.clamp(u.ty, -45, 45); }
  if (u.tp > 24 || u.tp < -6) { u.vp = -u.vp; u.tp = THREE.MathUtils.clamp(u.tp, -6, 24); }
  placeTracker(mesh);
}

function bucketAdd(table, key, field, amount) {
  table[key] ??= { shots: 0, hits: 0, bits: 0, time: 0, on: 0, total: 0 };
  table[key][field] += amount;
}

function flickKeys(u, fromDir, pos) {
  const v = pos.clone().sub(camera.position).normalize();
  const fromYaw = Math.atan2(-fromDir.x, -fromDir.z);
  const toYaw = Math.atan2(-v.x, -v.z);
  const dx = ((toYaw - fromYaw + Math.PI * 3) % (Math.PI * 2) - Math.PI) / DEG;
  const dy = (Math.asin(v.y) - Math.asin(fromDir.y)) / DEG;
  const dist = Math.hypot(dx, dy);
  const dir = Math.abs(dx) >= Math.abs(dy) ? (dx > 0 ? "left" : "right") : (dy > 0 ? "up" : "down");
  const distKey = dist < 10 ? "near" : dist < 24 ? "mid" : "far";
  return { size: "size:" + u.sizeKey, dist: "dist:" + distKey, dir: "dir:" + dir, distance: dist };
}

function startRun(map) {
  clearTargets();
  yaw = 0; pitch = 0;
  run = {
    map, started: 0, ends: 0, targets: [], shots: 0, hits: 0, times: [], lastDir: forward(), lastShot: 0,
    flick: {}, track: {}, onTime: 0, totalTime: 0, paused: false, last: performance.now()
  };
  $("mapLabel").textContent = `${map.kind === "tracking" ? "track" : "grid"} / ${map.name}`;
  if (map.kind === "tracking") run.targets.push(spawnTracker(map));
  else for (let i = 0; i < map.count; i++) run.targets.push(spawnFlick(map, false));
  markAccent();
  showGate(map.name);
}

function markAccent() {
  if (!run || run.map.kind === "tracking") return;
  run.targets.forEach((t, i) => (t.material = run.map.count > 1 && i === 0 ? accentMat : whiteMat));
}

function clearTargets() {
  for (const t of run?.targets ?? []) scene.remove(t);
}

function begin() {
  if (!run) return;
  const now = performance.now();
  if (!run.started) {
    yaw = 0;
    pitch = 0;
    run.started = now;
    run.ends = now + run.map.duration * 1000;
    run.lastShot = now;
    for (const t of run.targets) t.userData.born = now;
  } else if (run.paused) {
    const gap = now - run.pausedAt;
    run.ends += gap;
    run.lastShot += gap;
    for (const t of run.targets) t.userData.born += gap;
  }
  run.paused = false;
  run.last = now;
  document.body.classList.add("playing");
  hide("gate"); hide("pause");
}

function lock() {
  const p = canvas.requestPointerLock({ unadjustedMovement: true });
  if (p && p.catch) p.catch(() => canvas.requestPointerLock());
}

document.addEventListener("pointerlockchange", () => {
  if (document.pointerLockElement === canvas) begin();
  else if (run && run.started && !run.done) { run.paused = true; run.pausedAt = performance.now(); show("pause"); }
});

document.addEventListener("mousemove", (e) => {
  if (document.pointerLockElement !== canvas || !run || run.paused) return;
  const k = degPerCount() * DEG;
  yaw -= e.movementX * k;
  pitch -= e.movementY * k;
  pitch = THREE.MathUtils.clamp(pitch, -89 * DEG, 89 * DEG);
});

const ray = new THREE.Raycaster();

document.addEventListener("mousedown", (e) => {
  if (e.button !== 0 || document.pointerLockElement !== canvas || !run || run.paused || run.map.kind === "tracking") return;
  const now = performance.now();
  run.shots++;
  camera.rotation.set(pitch, yaw, 0);
  camera.updateMatrixWorld();
  ray.setFromCamera(new THREE.Vector2(0, 0), camera);
  const hit = ray.intersectObjects(run.targets, false)[0]?.object;
  const dir = forward();
  const target = hit ?? run.targets.reduce((best, t) => (!best || angleTo(dir, t.position) < angleTo(dir, best.position) ? t : best), null);
  if (!target) return;
  const keys = flickKeys(target.userData, run.lastDir, target.position);
  for (const k of [keys.size, keys.dist, keys.dir]) bucketAdd(run.flick, k, "shots", 1);
  if (hit) {
    const ms = now - Math.max(hit.userData.born, run.lastShot);
    const id = Math.log2(1 + keys.distance / (2 * hit.userData.radiusDeg));
    const bits = ms > 40 ? id / (ms / 1000) : 0;
    for (const k of [keys.size, keys.dist, keys.dir]) {
      bucketAdd(run.flick, k, "hits", 1);
      bucketAdd(run.flick, k, "time", ms);
      bucketAdd(run.flick, k, "bits", bits);
    }
    run.hits++;
    run.times.push(ms);
    scene.remove(hit);
    run.targets = run.targets.filter((t) => t !== hit);
    run.targets.push(spawnFlick(run.map, false));
    markAccent();
  }
  run.lastDir = dir;
  run.lastShot = now;
});

function frame() {
  const now = performance.now();
  if (run && run.started && !run.paused && !run.done) {
    const dt = Math.min(0.05, (now - run.last) / 1000);
    run.last = now;
    camera.rotation.set(pitch, yaw, 0);
    if (run.map.kind === "tracking") {
      const t = run.targets[0];
      updateTracker(t, dt, run.map);
      camera.updateMatrixWorld();
      const on = angleTo(forward(), t.position) <= t.userData.radiusDeg;
      run.onTime += on ? dt : 0;
      run.totalTime += dt;
      for (const k of ["speed:" + t.userData.speedKey, "axis:" + t.userData.axisKey, "size:" + t.userData.sizeKey]) {
        bucketAdd(run.track, k, "total", dt);
        if (on) bucketAdd(run.track, k, "on", dt);
      }
      t.material.emissiveIntensity = on ? 0.9 : 0.35;
    }
    const left = Math.max(0, run.ends - now);
    $("timer").textContent = clock(left);
    $("acc").textContent = accuracyText();
    if (left <= 0) finish();
  } else {
    camera.rotation.set(pitch, yaw, 0);
    if (!run || !run.started) yaw = Math.sin(now / 9000) * 0.12;
  }
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}

const clock = (ms) => {
  const s = Math.ceil(ms / 1000);
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
};

function accuracyText() {
  if (!run) return "--";
  if (run.map.kind === "tracking") return run.totalTime > 0 ? `${(100 * run.onTime / run.totalTime).toFixed(1)}%` : "--";
  return run.shots ? `${(100 * run.hits / run.shots).toFixed(1)}%` : "--";
}

function merge(target, source, decay) {
  for (const v of Object.values(target)) for (const f of Object.keys(v)) v[f] *= decay;
  for (const [k, v] of Object.entries(source)) {
    target[k] ??= { shots: 0, hits: 0, bits: 0, time: 0, on: 0, total: 0 };
    for (const f of Object.keys(v)) target[k][f] += v[f];
  }
}

function finish() {
  run.done = true;
  document.exitPointerLock?.();
  document.body.classList.remove("playing");
  const tracking = run.map.kind === "tracking";
  const acc = tracking ? (run.totalTime ? run.onTime / run.totalTime : 0) : (run.shots ? run.hits / run.shots : 0);
  const avg = run.times.length ? run.times.reduce((a, b) => a + b, 0) / run.times.length : 0;
  const score = tracking ? Math.round(acc * 10000) : Math.round(run.hits * 100 * acc);
  if (tracking) merge(profile.track, run.track, 0.85);
  else merge(profile.flick, run.flick, 0.85);
  profile.runs.unshift({ map: run.map.name, kind: run.map.kind, score, acc: +(acc * 100).toFixed(1), hits: run.hits, ms: Math.round(avg), at: Date.now() });
  profile.runs = profile.runs.slice(0, 60);
  store();

  $("resMap").textContent = run.map.name;
  $("resScore").textContent = score.toLocaleString("en-US");
  const best = Math.max(...profile.runs.filter((r) => r.map === run.map.name).map((r) => r.score));
  const stats = tracking
    ? [["on target", `${(acc * 100).toFixed(1)}%`], ["best", best.toLocaleString("en-US")], ["time", `${run.map.duration}s`], ["mode", "tracking"]]
    : [["accuracy", `${(acc * 100).toFixed(1)}%`], ["hits", run.hits], ["avg time to hit", `${Math.round(avg)} ms`], ["best", best.toLocaleString("en-US")]];
  $("resStats").innerHTML = stats.map(([k, v]) => `<div><span>${k}</span><b>${v}</b></div>`).join("");
  renderWeak($("resWeak"), 3);
  renderMenu();
  show("results");
}

function analyse() {
  const list = [];
  const groups = [
    ["size", ["small", "medium", "large"], { small: "small targets", medium: "medium targets", large: "big targets" }],
    ["dist", ["near", "mid", "far"], { near: "short flicks", mid: "medium flicks", far: "long flicks" }],
    ["dir", ["left", "right", "up", "down"], { left: "flicks to the left", right: "flicks to the right", up: "flicks up", down: "flicks down" }]
  ];
  for (const [g, keys, labels] of groups) {
    const rows = keys.map((k) => ({ k, b: profile.flick[`${g}:${k}`] })).filter((r) => r.b && r.b.shots >= 6);
    if (rows.length < 2 || rows.reduce((s, r) => s + r.b.hits, 0) < 8 || rows.some((r) => r.b.hits < 2)) continue;
    const perf = rows.map((r) => ({ ...r, hit: r.b.hits / r.b.shots, bits: r.b.hits ? r.b.bits / r.b.hits : 0, ms: r.b.hits ? r.b.time / r.b.hits : 0 }))
      .map((r) => ({ ...r, value: r.hit * r.bits }));
    const mean = perf.reduce((s, r) => s + r.value, 0) / perf.length;
    for (const r of perf) {
      const deficit = mean > 0 ? (mean - r.value) / mean : 0;
      list.push({ group: g, key: r.k, label: labels[r.k], deficit, line: `${Math.round(r.hit * 100)}% hits  ·  ${Math.round(r.ms)} ms` });
    }
  }
  const trackGroups = [["speed", ["slow", "fast"], { slow: "slow tracking", fast: "fast tracking" }], ["axis", ["h", "v"], { h: "side to side tracking", v: "up and down tracking" }]];
  for (const [g, keys, labels] of trackGroups) {
    const rows = keys.map((k) => ({ k, b: profile.track[`${g}:${k}`] })).filter((r) => r.b && r.b.total >= 4);
    if (rows.length === 0) continue;
    for (const r of rows) {
      const on = r.b.on / r.b.total;
      list.push({ group: g, key: r.k, label: labels[r.k], deficit: Math.max(0, 0.7 - on) / 0.7, line: `${Math.round(on * 100)}% on target` });
    }
  }
  return list.sort((a, b) => b.deficit - a.deficit);
}

function renderWeak(el, max) {
  const list = analyse();
  if (list.length === 0) {
    el.innerHTML = `<p class="note">play a few maps first. aeox measures every shot and finds where you lose time.</p>`;
    return list;
  }
  el.innerHTML = list.slice(0, max).map((w) => {
    const ok = w.deficit <= 0.05;
    const width = Math.round(Math.max(0.06, Math.min(1, 1 - w.deficit)) * 100);
    return `<div class="weak-row"><b>${w.label}</b><p>${w.line}${ok ? "  ·  fine" : ""}</p><div class="meter${ok ? " ok" : ""}"><i style="width:${width}%"></i></div></div>`;
  }).join("");
  return list;
}

function buildFromWeakness() {
  const weak = analyse().filter((w) => w.deficit > 0.05);
  const trackWeak = weak.filter((w) => w.group === "speed" || w.group === "axis");
  const flickWeak = weak.filter((w) => !trackWeak.includes(w));
  const top = weak[0];
  if (top && trackWeak.includes(top)) {
    const map = { id: `my-${Date.now()}`, name: "my map", kind: "tracking", count: 1, duration: 45, size: { small: 1, medium: 2, large: 0 }, speed: { slow: 1, fast: 1 }, axis: { h: 1, v: 1 } };
    for (const w of trackWeak) map[w.group][w.key] = 1 + Math.round(w.deficit * 6);
    map.name = "my map · " + trackWeak.slice(0, 2).map((w) => w.label.replace(" tracking", "")).join(", ");
    return map;
  }
  const map = { id: `my-${Date.now()}`, name: "my map", kind: "flick", count: 2, duration: 60,
    size: { small: 1, medium: 1, large: 1 }, dist: { near: 1, mid: 1, far: 1 }, dir: { left: 1, right: 1, up: 1, down: 1 } };
  for (const w of flickWeak) map[w.group][w.key] = Math.min(4, 1 + Math.round(w.deficit * 8));
  if (flickWeak.length) map.name = "my map · " + flickWeak.slice(0, 2).map((w) => w.label).join(", ");
  return map;
}

let editing = null;

function openBuilder(map) {
  editing = JSON.parse(JSON.stringify(map));
  $("mapName").value = editing.name;
  const fields = [];
  const slider = (path, label, min, max, step) => {
    const [a, b] = path.split(".");
    const value = b ? editing[a][b] : editing[a];
    fields.push(`<label class="slider"><div><span>${label}</span><b id="v-${path}">${value}</b></div><input type="range" min="${min}" max="${max}" step="${step}" value="${value}" data-path="${path}"></label>`);
  };
  fields.push(`<p class="group">round</p>`);
  slider("duration", "seconds", 15, 120, 15);
  if (editing.kind === "flick") slider("count", "targets at once", 1, 6, 1);
  else fields.push(`<span></span>`);
  fields.push(`<p class="group">target size</p>`);
  for (const k of ["small", "medium", "large"]) slider(`size.${k}`, k, 0, 4, 1);
  fields.push(`<span></span>`);
  if (editing.kind === "flick") {
    fields.push(`<p class="group">flick distance</p>`);
    for (const k of ["near", "mid", "far"]) slider(`dist.${k}`, k, 0, 4, 1);
    fields.push(`<span></span>`);
    fields.push(`<p class="group">flick direction</p>`);
    for (const k of ["left", "right", "up", "down"]) slider(`dir.${k}`, k, 0, 4, 1);
  } else {
    fields.push(`<p class="group">movement</p>`);
    slider("speed.slow", "slow strafes", 0, 4, 1);
    slider("speed.fast", "fast strafes", 0, 4, 1);
    slider("axis.h", "side to side", 0, 4, 1);
    slider("axis.v", "up and down", 0, 4, 1);
  }
  $("builderFields").innerHTML = fields.join("");
  $("builderFields").querySelectorAll("input[type=range]").forEach((inp) => inp.addEventListener("input", () => {
    const [a, b] = inp.dataset.path.split(".");
    const v = Number(inp.value);
    if (b) editing[a][b] = v; else editing[a] = v;
    $(`v-${inp.dataset.path}`).textContent = v;
  }));
  $("deleteMap").hidden = !profile.maps.some((m) => m.id === editing.id);
  show("builder");
}

function mapCard(map, mine) {
  const el = document.createElement("div");
  el.className = "map" + (mine ? " mine" : "");
  const best = profile.runs.filter((r) => r.map === map.name).reduce((m, r) => Math.max(m, r.score), 0);
  const note = map.note ?? describe(map);
  el.innerHTML = `<div><b>${map.name}</b><p>${note}</p></div><small><span>${map.duration}s${best ? `  ·  best ${best.toLocaleString("en-US")}` : ""}</span>${mine ? `<span class="edit">edit</span>` : ""}</small>`;
  el.addEventListener("click", (e) => {
    if (e.target.classList.contains("edit")) { openBuilder(map); return; }
    startRun(map);
  });
  return el;
}

function describe(map) {
  if (map.kind === "tracking") return `tracking  ·  ${Object.entries(map.axis).filter(([, v]) => v > 0).map(([k]) => (k === "h" ? "side to side" : "up and down")).join(", ")}`;
  const top = (o) => Object.entries(o).filter(([, v]) => v > 1).map(([k]) => k);
  const parts = [...top(map.size), ...top(map.dist), ...top(map.dir)];
  return `${map.count} at once${parts.length ? "  ·  " + parts.join(", ") : ""}`;
}

function renderMenu() {
  const maps = $("maps");
  maps.innerHTML = "";
  for (const p of PRESETS) maps.appendChild(mapCard(p, false));
  const mine = $("myMaps");
  mine.innerHTML = "";
  for (const m of profile.maps) mine.appendChild(mapCard(m, true));
  $("myLabel").hidden = profile.maps.length === 0;
  const weak = renderWeak($("weak"), 5);
  $("buildNote").textContent = weak.length ? "turns your weak spots into a map you can tune and keep." : "";
  $("runs").innerHTML = profile.runs.slice(0, 8).map((r) => `<li><b>${r.map}</b><span>${r.score.toLocaleString("en-US")}  ·  ${r.acc}%</span></li>`).join("");
  renderSettings();
}

const tidy = (v) => String(+Number(v).toFixed(4));

function renderSettings() {
  const g = GAMES[profile.game];
  $("game").value = profile.game;
  const custom = profile.game === "custom";
  $("sens").parentElement.hidden = custom;
  $("cmRow").hidden = !custom;
  $("sensLabel").textContent = g.unit;
  $("sens").value = tidy(profile.sens[profile.game]);
  $("cm360").value = tidy(profile.cm360);
  $("fov").value = tidy(hfov());
  $("dpi").value = tidy(profile.dpi);
  const dpc = degPerCount();
  const cm = (360 / dpc / profile.dpi) * 2.54;
  $("readout").innerHTML = `${dpc.toFixed(5)}° per mouse count<br>${cm.toFixed(1)} cm per 360 at ${profile.dpi} dpi`;
  const imp = imports.find((i) => i.game === profile.game);
  $("source").textContent = imp
    ? `read from your ${imp.source}. same feel as in game.`
    : custom ? "type your cm/360 from any game. aeox matches it exactly."
    : profile.game === "fortnite" || profile.game === "overwatch"
      ? `${g.name} keeps sensitivity in your online account, so type it once. fov is horizontal on 16:9.`
      : "not found on this pc, type your in game value. fov is horizontal on 16:9.";
  resize();
}

function setupSettings() {
  const sel = $("game");
  sel.innerHTML = Object.entries(GAMES).map(([k, g]) => `<option value="${k}">${g.name}</option>`).join("");
  sel.addEventListener("change", () => { profile.game = sel.value; store(); renderSettings(); });
  const num = (id, apply) => $(id).addEventListener("input", () => {
    const v = Number($(id).value.replace(",", "."));
    if (!Number.isFinite(v) || v <= 0) return;
    apply(v);
    store();
    renderSettingsReadout();
  });
  num("sens", (v) => (profile.sens[profile.game] = v));
  num("cm360", (v) => (profile.cm360 = v));
  num("fov", (v) => { profile.fov[profile.game] = v; resize(); });
  num("dpi", (v) => (profile.dpi = v));
}

function renderSettingsReadout() {
  const dpc = degPerCount();
  const cm = (360 / dpc / profile.dpi) * 2.54;
  $("readout").innerHTML = `${dpc.toFixed(5)}° per mouse count<br>${cm.toFixed(1)} cm per 360 at ${profile.dpi} dpi`;
}

const show = (id) => ($(id).hidden = false);
const hide = (id) => ($(id).hidden = true);

function showGate(name) {
  $("gateTitle").textContent = name;
  hide("results"); hide("builder"); hide("pause");
  document.body.classList.add("playing");
  $("timer").textContent = clock(run.map.duration * 1000);
  $("acc").textContent = "--";
  show("gate");
}

function toMenu() {
  if (document.pointerLockElement) document.exitPointerLock();
  clearTargets();
  run = null;
  document.body.classList.remove("playing");
  hide("pause"); hide("results"); hide("gate");
  $("timer").textContent = "00:00";
  $("acc").textContent = "--";
  $("mapLabel").textContent = "";
  renderMenu();
}

$("gate").addEventListener("click", lock);
$("resume").addEventListener("click", lock);
$("restart").addEventListener("click", () => { const m = run.map; clearTargets(); hide("pause"); startRun(m); });
$("quit").addEventListener("click", toMenu);
$("again").addEventListener("click", () => startRun(run.map));
$("toMenu").addEventListener("click", toMenu);
$("trainWeak").addEventListener("click", () => { hide("results"); openBuilder(buildFromWeakness()); });
$("build").addEventListener("click", () => openBuilder(buildFromWeakness()));
$("closeBuilder").addEventListener("click", () => hide("builder"));
$("deleteMap").addEventListener("click", () => {
  profile.maps = profile.maps.filter((m) => m.id !== editing.id);
  store(); hide("builder"); renderMenu();
});
$("saveMap").addEventListener("click", () => {
  editing.name = $("mapName").value.trim() || "my map";
  const i = profile.maps.findIndex((m) => m.id === editing.id);
  if (i >= 0) profile.maps[i] = editing; else profile.maps.unshift(editing);
  store();
  hide("builder");
  renderMenu();
  startRun(editing);
});

$("bar").addEventListener("mousedown", (e) => {
  if (e.button !== 0 || e.target.closest("button") || document.body.classList.contains("playing")) return;
  send("drag");
});
$("bar").addEventListener("dblclick", (e) => { if (!e.target.closest("button")) send("max"); });
document.querySelectorAll("[data-win]").forEach((b) => b.addEventListener("click", () => send(b.dataset.win)));

function init(data) {
  load(data?.profile);
  imports = data?.imports ?? [];
  for (const i of imports) {
    if (!profile.imported?.includes(i.game)) {
      profile.sens[i.game] = +i.sens;
      if (i.hFov) profile.fov[i.game] = +i.hFov;
      profile.imported = [...(profile.imported ?? []), i.game];
    } else if (profile.sens[i.game] !== i.sens) {
      profile.sens[i.game] = +i.sens;
    }
  }
  if (!data?.profile && imports.length) profile.game = imports[0].game;
  store();
  setupSettings();
  renderMenu();
}

if (bridge) {
  bridge.addEventListener("message", (e) => { if (e.data?.type === "init") init(e.data); });
  send("ready");
} else {
  let saved = null;
  try { saved = JSON.parse(localStorage.getItem("aeox-aim") ?? "null"); } catch { }
  document.querySelector(".win").hidden = true;
  init({ profile: saved, imports: [] });
}

resize();
requestAnimationFrame(frame);
