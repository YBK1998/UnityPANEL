using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace A11Sim
{
    using K = WireKind;

    /// Builds the merged E1–E4 schematic, runs the circuit every frame and renders energized wires with a glow.
    public class SchematicView : MonoBehaviour
    {
        const float FontScale = 10.5f;
        const int OrderGlow = 0, OrderSym = 10, OrderCaution = 12, OrderWire = 20, OrderTerm = 25, OrderDynGlow = 28, OrderDyn = 30, OrderText = 40;

        public Circuit circuit;
        public A11Board a11;
        public Vfd vfd;
        public Camera cam;
        public CameraRig rig;
        public Hud hud;
        public Panel3D panel;
        public Multimeter meter;
        public Commissioning proc;
        public BuildGuide guide;
        AudioSource beeper;
        Terminal hoverTerm;

        public bool showNumbers = true, showFlow = true, showCautions = true;
        public string tooltip;
        public Vector2? simScreen; // simulated pointer for automated screenshots

        Material coreMat, glowMat, dynGlowMat;
        TMP_FontAsset font;
        Transform root;

        class WireVis
        {
            public Wire w;
            public MeshRenderer core, glow;
            public readonly MaterialPropertyBlock cb = new(), gb = new();
            public Color col = Pal.WireOff, gcol = Color.black;
        }

        readonly List<WireVis> vis = new();

        class Lbl
        {
            public TextMeshPro t;
            public Labels.Item item;
            public string last;
            public bool wireNo, caution;
        }

        readonly List<Lbl> lbls = new();
        readonly List<Hotspot> spots = new();
        readonly Gfx dyn = new(), dynGlow = new();
        Mesh dynMesh, dynGlowMesh;
        MeshRenderer cautionRenderer;
        Hotspot pressed, hoverSpot;
        Wire hoverWire;
        CautionMark hoverCaution;
        float lastWpp = -1;
        bool lastNumbers = true, lastCautions = true;

        static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;

        public static void SetBorderless(bool on)
        {
            if (on) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }

        void Start()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 240;
            // Borderless full screen at the monitor's native resolution unless a window was asked for
            // on the command line (Unity also remembers the last mode, so set it explicitly).
            if (!Application.isEditor && !HasArg("-screen-fullscreen") && !HasArg("-windowed"))
                SetBorderless(true);
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            circuit = A11Layout.Build();
            a11 = circuit.Get<A11Board>("A1");
            vfd = circuit.Get<Vfd>("U1");

            coreMat = new Material(Shader.Find("A11/LineCore"));
            glowMat = new Material(Shader.Find("A11/LineGlow"));
            glowMat.SetFloat("_WidthMul", 8f);
            glowMat.SetFloat("_MinPx", 7f);
            glowMat.SetFloat("_FlowFreq", 0.16f);
            coreMat.SetFloat("_FlowFreq", 0.16f);
            glowMat.SetFloat("_MaxW", 2.2f); // junction dots must not get a bigger halo than the wire
            dynGlowMat = new Material(Shader.Find("A11/LineGlow"));
            dynGlowMat.SetFloat("_WidthMul", 1f);
            dynGlowMat.SetFloat("_MinPx", 0f);
            font = TMP_Settings.defaultFontAsset;

            root = new GameObject("Schematic").transform;
            root.SetParent(transform, false);

            BuildStatic();
            BuildWires();
            dynMesh = new Mesh { name = "dynamic" };
            dynGlowMesh = new Mesh { name = "dynamic glow" };
            dynMesh.MarkDynamic();
            dynGlowMesh.MarkDynamic();
            MakeRenderer("dynamic", dynMesh, coreMat, OrderDyn, root);
            MakeRenderer("dynamic glow", dynGlowMesh, dynGlowMat, OrderDynGlow, root);
            foreach (var d in circuit.devices) spots.AddRange(d.Hotspots());

            // TryGetComponent: in the Editor a missing component is a "fake null" object, so ?? does not work.
            if (!cam.TryGetComponent(out rig)) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.content = circuit.bounds;
            if (!TryGetComponent(out hud)) hud = gameObject.AddComponent<Hud>();
            hud.view = this;
            rig.blocked = p => hud.IsOver(p);
            rig.freeRect = hud.FreeScreenRect;
            rig.Fit();
            Shader.SetGlobalFloat("_WorldPerPixel", 2f * cam.orthographicSize / Mathf.Max(1, Screen.height));

            meter = new Multimeter(circuit);
            proc = new Commissioning(circuit, meter);
            panel = gameObject.AddComponent<Panel3D>();
            panel.view = this;
            panel.Build();
            guide = new BuildGuide(circuit, panel.Cabinet);
            SetupBeeper();

            circuit.Solve();

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11shots") StartCoroutine(AutoShots(args[i + 1]));
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11quick") StartCoroutine(QuickCheck(args[i + 1]));
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11test3d") StartCoroutine(Test3D(args[i + 1]));
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11tools") StartCoroutine(ToolsTest(args[i + 1]));
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11perf") StartCoroutine(PerfTest(args[i + 1]));
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11views") StartCoroutine(EnclosureViews(args[i + 1]));
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-a11lengths")
                {
                    File.WriteAllText(args[i + 1], panel.Cabinet.LengthReport());
                    Application.Quit();
                }
        }

        int lastW, lastH;

        void LateUpdate()
        {
            // Re-fit after the window changes size (e.g. switching borderless ↔ windowed).
            if (Screen.width != lastW || Screen.height != lastH)
            {
                bool first = lastW == 0;
                lastW = Screen.width;
                lastH = Screen.height;
                if (!first) rig.Fit();
            }
        }

        MeshRenderer MakeRenderer(string name, Mesh mesh, Material mat, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        TextMeshPro MakeText(string s, Vector2 pos, float size, Color col, TextAlignmentOptions align, float rot, bool bold)
        {
            var go = new GameObject("t");
            go.transform.SetParent(root, false);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = s;
            t.fontSize = size * FontScale;
            t.color = col;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.rectTransform.sizeDelta = new Vector2(0.01f, 0.01f);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0);
            go.transform.localRotation = Quaternion.Euler(0, 0, rot);
            t.GetComponent<MeshRenderer>().sortingOrder = OrderText;
            return t;
        }

        void BuildStatic()
        {
            var g = new Gfx();
            var lb = new Labels();
            foreach (var d in circuit.devices)
            {
                g.color = Pal.Sym;
                g.width = 0.16f;
                d.DrawStatic(g, lb);
            }
            MakeRenderer("symbols", g.Build(new Mesh { name = "symbols" }), coreMat, OrderSym, root);

            // Terminals sit on top of the wires: a background disc + ring.
            var tg = new Gfx { color = Pal.Sym, width = 0.15f };
            foreach (var t in circuit.terminals)
            {
                if (t.hidden) continue;
                tg.Disc(t.pos, Sym.TermR, Pal.Bg);
                Sym.Terminal(tg, t.pos);
            }
            MakeRenderer("terminals", tg.Build(new Mesh { name = "terminals" }), coreMat, OrderTerm, root);

            var cg = new Gfx { width = 0.16f };
            foreach (var c in circuit.cautions)
            {
                Sym.CautionTriangle(cg, c.pos, 1.8f, Pal.Caution);
                lbls.Add(new Lbl { t = MakeText(c.id, c.pos + new Vector2(1.15f, 0), 0.8f, Pal.Caution, TextAlignmentOptions.Left, 0, true), caution = true });
            }
            cautionRenderer = MakeRenderer("cautions", cg.Build(new Mesh { name = "cautions" }), coreMat, OrderCaution, root);

            foreach (var it in lb.items)
                lbls.Add(new Lbl { t = MakeText(it.text, it.pos, it.size, it.color, it.align, it.rot, it.bold), item = it });
            var titleCol = new Color(0.55f, 0.66f, 0.82f);
            foreach (var (text, pos, size) in circuit.titles)
                lbls.Add(new Lbl { t = MakeText(text, pos, size, titleCol, TextAlignmentOptions.Left, 0, size > 1.2f) });
        }

        void BuildWires()
        {
            var termKeys = new HashSet<Vector2Int>();
            foreach (var t in circuit.terminals) termKeys.Add(Key(t.pos));
            var dots = JunctionDots(termKeys);
            var dotted = new HashSet<Vector2Int>();

            var wroot = new GameObject("wires").transform;
            wroot.SetParent(root, false);
            foreach (var w in circuit.wires)
            {
                var g = new Gfx { color = Color.white, width = w.isBus ? 0.30f : 0.24f };
                if (w.notConnected)
                {
                    for (int i = 0; i < w.pts.Length - 1; i++) g.Dashed(w.pts[i], w.pts[i + 1], 0.6f, 0.45f);
                }
                else g.Poly(w.pts);

                if (!w.notConnected)
                    foreach (var (k, p) in dots)
                    {
                        if (dotted.Contains(k) || !OnWire(w, p)) continue;
                        g.Disc(p, 0.42f, Color.white);
                        dotted.Add(k);
                    }

                var mesh = g.Build(new Mesh { name = "wire " + w.number });
                var v = new WireVis
                {
                    w = w,
                    core = MakeRenderer("wire " + w.number, mesh, coreMat, OrderWire, wroot),
                    glow = MakeRenderer("glow " + w.number, mesh, glowMat, OrderGlow, wroot),
                };
                v.glow.enabled = false;
                vis.Add(v);

                if (!w.noLabel && !string.IsNullOrEmpty(w.number)) AddWireLabel(w);
            }
        }

        /// Points where one net leaves in three or more directions (T-junctions and branch points).
        /// Grouped per net, so crossings of different nets never get a dot.
        List<(Vector2Int key, Vector2 p)> JunctionDots(HashSet<Vector2Int> termKeys)
        {
            var root = new Dictionary<Terminal, Terminal>();
            Terminal Find(Terminal t)
            {
                while (root.TryGetValue(t, out var r) && r != t) t = r;
                return t;
            }
            foreach (var w in circuit.wires)
                if (!w.notConnected)
                {
                    var ra = Find(w.a);
                    var rb = Find(w.b);
                    if (ra != rb) root[ra] = rb;
                }
            var dirs = new Dictionary<(Vector2Int, Terminal), (Vector2 p, HashSet<int> d)>();
            void Add(Vector2 p, Vector2 d, Terminal net)
            {
                var k = (Key(p), net);
                if (!dirs.TryGetValue(k, out var e)) dirs[k] = e = (p, new HashSet<int>());
                e.d.Add(Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 360f) / 15f) % 24);
            }
            var segs = new List<(Vector2 a, Vector2 b, Terminal net)>();
            foreach (var w in circuit.wires)
            {
                if (w.notConnected) continue;
                var net = Find(w.a);
                for (int i = 0; i < w.pts.Length - 1; i++)
                {
                    if ((w.pts[i + 1] - w.pts[i]).sqrMagnitude < 1e-6f) continue;
                    segs.Add((w.pts[i], w.pts[i + 1], net));
                    Add(w.pts[i], w.pts[i + 1] - w.pts[i], net);
                    Add(w.pts[i + 1], w.pts[i] - w.pts[i + 1], net);
                }
            }
            foreach (var (a, b, net) in segs)
            foreach (var kv in dirs.ToList())
            {
                if (kv.Key.Item2 != net) continue;
                var p = kv.Value.p;
                if ((p - a).sqrMagnitude < 1e-4f || (p - b).sqrMagnitude < 1e-4f || DistToSeg(p, a, b) > 0.01f) continue;
                Add(p, b - a, net);
                Add(p, a - b, net);
            }
            var result = new List<(Vector2Int, Vector2)>();
            foreach (var kv in dirs)
                if (kv.Value.d.Count >= 3 && !termKeys.Contains(kv.Key.Item1))
                    result.Add((kv.Key.Item1, kv.Value.p));
            return result;
        }

        static bool OnWire(Wire w, Vector2 p)
        {
            for (int i = 0; i < w.pts.Length - 1; i++)
                if (DistToSeg(p, w.pts[i], w.pts[i + 1]) < 0.01f) return true;
            return false;
        }

        void AddWireLabel(Wire w)
        {
            string text = w.labelText ?? w.number;
            Vector2 pos;
            var align = TextAlignmentOptions.Left;
            if (w.labelAt.HasValue) pos = w.labelAt.Value;
            else
            {
                // Label the last leg (the drop into the destination terminal); fall back to the first leg,
                // then to the longest. Chained wires share the net lines, so their own legs keep labels apart.
                int segCount = w.pts.Length - 1, best = segCount - 1;
                float Len(int i) => Vector2.Distance(w.pts[i], w.pts[i + 1]);
                if (Len(best) < 2.5f) best = 0;
                if (Len(best) < 2.5f)
                {
                    float bestLen = -1;
                    for (int i = 0; i < segCount; i++)
                        if (Len(i) > bestLen + 0.01f)
                        {
                            bestLen = Len(i);
                            best = i;
                        }
                }
                var a = w.pts[best];
                var b = w.pts[best + 1];
                var mid = (a + b) * 0.5f;
                bool vertical = Mathf.Abs(a.x - b.x) < 0.01f;
                if (vertical) pos = mid + new Vector2(0.45f, 0);
                else
                {
                    pos = mid + new Vector2(0, 0.72f);
                    align = TextAlignmentOptions.Center;
                }
            }
            var col = w.notConnected ? Pal.Caution : Pal.WireNo;
            lbls.Add(new Lbl { t = MakeText(text, pos, 0.9f, col, align, 0, true), wireNo = true });
        }

        static Vector2Int Key(Vector2 p) => new(Mathf.RoundToInt(p.x * 100), Mathf.RoundToInt(p.y * 100));

        static void Inc(Dictionary<Vector2Int, int> d, Vector2Int k) => d[k] = d.TryGetValue(k, out var v) ? v + 1 : 1;

        static bool PassesInterior(Wire o, Vector2 p)
        {
            if ((o.pts[0] - p).sqrMagnitude < 1e-4f || (o.pts[^1] - p).sqrMagnitude < 1e-4f) return false;
            for (int i = 0; i < o.pts.Length - 1; i++)
                if (DistToSeg(p, o.pts[i], o.pts[i + 1]) < 0.01f) return true;
            return false;
        }

        static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-8f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            double t0 = PerfStats.Now;
            UpdateBody();
            PerfStats.viewMs += PerfStats.Now - t0;
        }

        void UpdateBody()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            PerfStats.Begin();
            HandleInput();
            PerfStats.Mark("view.input");
            foreach (var d in circuit.devices) d.Tick(dt);
            PerfStats.Mark("view.tick");
            circuit.Solve();
            PerfStats.Mark("view.solve");
            proc.Update();
            Beep();
            StartHints();
            PerfStats.Mark("view.tools");
            UpdateWires(dt);
            PerfStats.Mark("view.wires");
            RebuildDynamic();
            PerfStats.Mark("view.dynamic");
            UpdateLabels();
            PerfStats.Mark("view.labels");
        }

        public string toast;
        public float toastUntil;
        bool onWas;

        public void Toast(string s, float seconds = 5f)
        {
            toast = s;
            toastUntil = Time.time + seconds;
        }

        /// Continuity beeper: a 2.7 kHz tone while the meter is on Ω and the probes are connected.
        void SetupBeeper()
        {
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            const int rate = 44100;
            var data = new float[rate / 10];
            for (int i = 0; i < data.Length; i++) data[i] = 0.18f * Mathf.Sin(2 * Mathf.PI * 2700f * i / rate);
            var clip = AudioClip.Create("beep", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            beeper = gameObject.AddComponent<AudioSource>();
            beeper.clip = clip;
            beeper.loop = true;
            beeper.playOnAwake = false;
            beeper.spatialBlend = 0;
        }

        void Beep()
        {
            bool on = meter.active && meter.mode == MeterMode.Ohm && meter.Read().beep;
            if (on && !beeper.isPlaying) beeper.Play();
            else if (!on && beeper.isPlaying) beeper.Stop();
        }

        /// Explain why ON does nothing (the circuit is right, but the reason is often off-screen).
        void StartHints()
        {
            bool on = circuit.Get<PushButton>("S2").pressed;
            if (on && !onWas && !circuit.Get<RelayK1>("K1").on && !proc.Active)
            {
                string why = null;
                if (!circuit.Get<Breaker>("QF0").closed) why = "the main CB QF0 is OFF. Switch it ON first (Q, or QF0 in the left panel).";
                else if (!circuit.Get<Breaker>("QF4").closed) why = "QF4 (24 V PSU) is OFF, so there is no control voltage.";
                else if (!circuit.Get<Fuse>("F1").inserted) why = "fuse F1 is out, so TB24C has no 24 V.";
                else if (circuit.Get<EStop>("S0").pressed) why = "the E-stop is pushed. Click it again to release it.";
                else if (circuit.Get<PushButton>("S1").pressed) why = "OFF is held at the same time.";
                if (why != null) Toast("ON has no effect: " + why);
            }
            onWas = on;
        }

        void Shortcuts()
        {
            var s0 = circuit.Get<EStop>("S0");
            if (Input.GetKeyDown(KeyCode.E)) s0.pressed = !s0.pressed;
            if (Input.GetKeyDown(KeyCode.Q) && !Ctrl) circuit.Get<Breaker>("QF0").closed ^= true;
            circuit.Get<PushButton>("S2").keyHold = Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1);
            circuit.Get<PushButton>("S1").keyHold = Input.GetKey(KeyCode.Alpha0) || Input.GetKey(KeyCode.Keypad0);
            if (Input.GetKeyDown(KeyCode.Space)) a11.ToggleSpindle();
            if (Input.GetKeyDown(KeyCode.H)) a11.HomeAll();
            if (Input.GetKeyDown(KeyCode.Escape)) a11.Stop();
            if (Input.GetKeyDown(KeyCode.F11)) SetBorderless(Screen.fullScreenMode != FullScreenMode.FullScreenWindow);
            if (Ctrl && Input.GetKeyDown(KeyCode.Q)) Application.Quit();
        }

        static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        void HandleInput()
        {
            Shortcuts();
            Vector2 mp = simScreen ?? (Vector2)Input.mousePosition;
            hoverSpot = null;
            hoverWire = null;
            hoverCaution = null;
            if (panel.full)
            {
                // The 3D door owns the mouse.
                tooltip = null;
                if (pressed != null)
                {
                    pressed.onUp?.Invoke();
                    pressed = null;
                }
                return;
            }
            bool overUi = hud.IsOver(mp);
            Vector2 wp = cam.ScreenToWorldPoint(mp);
            float wpp = 2f * cam.orthographicSize / Mathf.Max(1, Screen.height);

            hoverTerm = null;
            if (meter.active && !overUi && !rig.Panning && pressed == null)
            {
                float tol = Mathf.Max(0.7f, 9f * wpp), bestD = tol;
                foreach (var t in circuit.terminals)
                {
                    if (t.hidden) continue;
                    float d = Vector2.Distance(wp, t.pos);
                    if (d < bestD)
                    {
                        bestD = d;
                        hoverTerm = t;
                    }
                }
                if (hoverTerm != null)
                {
                    tooltip = $"<b>Probe → {hoverTerm.dev.tag} {hoverTerm.name}</b>\nClick to place the {(meter.nextBlack ? "black" : "<color=#ff6a5a>red</color>")} probe.";
                    if (Input.GetMouseButtonDown(0)) meter.Place(hoverTerm);
                    return;
                }
            }
            if (!overUi && !rig.Panning && pressed == null)
            {
                float best = float.MaxValue;
                foreach (var h in spots)
                {
                    if (!h.rect.Contains(wp)) continue;
                    float area = h.rect.width * h.rect.height;
                    if (area < best)
                    {
                        best = area;
                        hoverSpot = h;
                    }
                }
                if (showCautions)
                    foreach (var c in circuit.cautions)
                        if (Vector2.Distance(wp, c.pos) < 1.3f)
                        {
                            hoverCaution = c;
                            hoverSpot = null;
                        }
                if (hoverSpot == null && hoverCaution == null)
                {
                    float tol = Mathf.Max(0.45f, 6f * wpp), bestD = tol;
                    foreach (var w in circuit.wires)
                        for (int i = 0; i < w.pts.Length - 1; i++)
                        {
                            float d = DistToSeg(wp, w.pts[i], w.pts[i + 1]);
                            if (d < bestD)
                            {
                                bestD = d;
                                hoverWire = w;
                            }
                        }
                }
            }
            else if (pressed != null) hoverSpot = pressed;

            if (Input.GetMouseButtonDown(0) && !overUi)
            {
                if (hoverSpot?.onDown != null)
                {
                    hoverSpot.onDown();
                    pressed = hoverSpot;
                }
                else rig.BeginPan(0);
            }
            if (pressed != null && !Input.GetMouseButton(0))
            {
                pressed.onUp?.Invoke();
                pressed = null;
            }

            if (hoverSpot != null) tooltip = hoverSpot.tip?.Invoke();
            else if (hoverCaution != null) tooltip = $"<b><color=#ff9a3c>CAUTION {hoverCaution.id} — MEASURE / CONFIRM</color></b>\n{hoverCaution.text}";
            else if (hoverWire != null) tooltip = WireTip(hoverWire);
            else tooltip = null;
        }

        public string WireTip(Wire w)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(w.number))
            {
                sb.Append($"<b>Wire {w.number}</b>");
                if (!string.IsNullOrEmpty(w.spec)) sb.Append($"   <color=#8fa3bf>{w.spec}</color>");
                sb.Append('\n').Append(w.desc);
            }
            else if (w.isBus) sb.Append($"<b>{w.a.dev.title}</b>\n{w.a.dev.info}");
            else sb.Append($"<b>{w.desc}</b>");
            sb.Append("\n<color=#9fe8ff>State: ").Append(Describe(w)).Append("</color>");
            return sb.ToString();
        }

        string Describe(Wire w)
        {
            if (w.notConnected) return "not connected — do not fit (§ 9.1-7)";
            var p = w.pot;
            var parts = new List<string>();
            if (Has(p, Pot.L)) parts.Add("LIVE 230 V AC — line (L)");
            if (Has(p, Pot.N)) parts.Add("neutral (N) — part of the live supply");
            if (Has(p, Pot.PH)) parts.Add($"VFD output {vfd.freq:0.0} Hz ({vfd.Rpm:0} rpm)");
            if (Has(p, Pot.DCB)) parts.Add("VFD DC bus ≈ 310 V DC");
            if (Has(p, Pot.BRK)) parts.Add("BRAKING — current into R1");
            if (Has(p, Pot.P48)) parts.Add("+48 V DC");
            if (Has(p, Pot.Z48)) parts.Add("48 V return (−V)");
            if (Has(p, Pot.P24)) parts.Add("+24 V DC");
            if (Has(p, Pot.Z0)) parts.Add(w.kind == K.Sig ? "ACTIVE — pulled to 0 V, current flows" : "0 V (PELV, bonded to PE)");
            if (Has(p, Pot.P5)) parts.Add("+5 V (A11 axis common anode)");
            if (Has(p, Pot.MOT)) parts.Add("stepper phase current");
            if (Has(p, Pot.ACT)) parts.Add(w.kind == K.Pulse ? "step pulses" : "active (direction −)");
            if (parts.Count == 0) return Has(p, Pot.PE) ? "protective earth" : "de-energized";
            return string.Join(" · ", parts);
        }

        static bool Has(Pot p, Pot f) => (p & f) != 0;

        internal struct WireStyle
        {
            public Color col;
            public float gi, flow, speed, flicker;

            public static WireStyle Live(Color c, float glow, float fl, float s, float flick = 0) =>
                new() { col = c, gi = glow, flow = fl, speed = s, flicker = flick };
        }

        internal static WireStyle Style(Wire w)
        {
            var off = new WireStyle { col = Pal.WireOff };
            var p = w.pot;
            float sp = w.speedOf != null ? w.speedOf() : 1f;
            switch (w.kind)
            {
                case K.AC:
                    if (Has(p, Pot.L)) return WireStyle.Live(Pal.L, 1f, 0.45f, 1.4f);
                    if (Has(p, Pot.N)) return WireStyle.Live(Pal.N, 0.85f, 0.45f, 1.4f);
                    break;
                case K.Phase:
                    if (Has(p, Pot.PH)) return WireStyle.Live(Pal.Phase, 1f, 0.8f, 0.6f + sp);
                    break;
                case K.Brake:
                    if (Has(p, Pot.BRK)) return WireStyle.Live(Pal.Brake, 1.2f, 0.8f, 5f);
                    if (Has(p, Pot.DCB)) return WireStyle.Live(Pal.DCB, 0.7f, 0.25f, 0.8f);
                    break;
                case K.PE:
                    return new WireStyle { col = Pal.PE * 0.8f };
                case K.DC48:
                    if (Has(p, Pot.P48)) return WireStyle.Live(Pal.P48, 1f, 0.45f, 1.4f);
                    if (Has(p, Pot.Z48)) return WireStyle.Live(Pal.Ret, 0.35f, 0.3f, 1f);
                    break;
                case K.DC24:
                    if (Has(p, Pot.P24)) return WireStyle.Live(Pal.P24, 1f, 0.45f, 1.4f);
                    if (Has(p, Pot.Z0)) return WireStyle.Live(Pal.Ret, 0.35f, 0.3f, 1f);
                    break;
                case K.Sig:
                    if (Has(p, Pot.ACT) || Has(p, Pot.Z0)) return WireStyle.Live(Pal.Sig, 1f, 0.6f, 2.2f);
                    break;
                case K.Sig5:
                    if (Has(p, Pot.P5)) return WireStyle.Live(Pal.P5, 0.9f, 0.4f, 1.4f);
                    break;
                case K.Pulse:
                    if (Has(p, Pot.ACT)) return WireStyle.Live(Pal.Sig, 1f, 0.9f, 6f + sp * 0.08f, 1f);
                    break;
                case K.Dir:
                    if (Has(p, Pot.ACT)) return WireStyle.Live(Pal.Sig, 1f, 0.3f, 1f);
                    break;
                case K.Motor:
                    if (Has(p, Pot.MOT)) return WireStyle.Live(Pal.Mot, 0.85f, 0.6f, sp);
                    break;
            }
            return off;
        }

        void UpdateWires(float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 14f);
            foreach (var v in vis)
            {
                var s = Style(v.w);
                Color col = s.col;
                float gi = s.gi, flow = s.flow, speed = s.speed, flicker = s.flicker;
                bool live = gi > 0;
                var targetCore = live ? Color.Lerp(col, Color.white, 0.22f) : col;
                var targetGlow = live ? col * gi : Color.black;
                targetGlow.a = 1;
                v.col = Color.Lerp(v.col, targetCore, k);
                v.gcol = Color.Lerp(v.gcol, targetGlow, k);
                float f = showFlow ? flow : 0;
                v.cb.SetColor("_Color", v.col);
                v.cb.SetFloat("_FlowAmt", f * 0.35f);
                v.cb.SetFloat("_FlowSpeed", speed);
                v.cb.SetFloat("_Flicker", flicker);
                v.core.SetPropertyBlock(v.cb);
                bool glowOn = v.gcol.maxColorComponent > 0.01f;
                if (v.glow.enabled != glowOn) v.glow.enabled = glowOn;
                if (glowOn)
                {
                    v.gb.SetColor("_Color", v.gcol);
                    v.gb.SetFloat("_FlowAmt", f * 0.7f);
                    v.gb.SetFloat("_FlowSpeed", speed);
                    v.gb.SetFloat("_Flicker", flicker);
                    v.glow.SetPropertyBlock(v.gb);
                }
            }
        }

        void RebuildDynamic()
        {
            dyn.Clear();
            dynGlow.Clear();
            float t = Time.time;
            foreach (var d in circuit.devices)
            {
                dyn.color = Pal.Sym;
                dyn.width = 0.16f;
                d.DrawDynamic(dyn, dynGlow, t);
            }
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 7f);
            var gold = new Color(1f, 0.75f, 0.1f);
            if (guide.active && guide.Current != null && !panel.full)
            {
                var gw = guide.Current.w;
                for (int i = 0; i < gw.pts.Length - 1; i++)
                    dynGlow.Line(gw.pts[i], gw.pts[i + 1], 1.8f, gold * (0.6f * pulse));
            }
            foreach (var tt in proc.Targets())
            {
                dyn.color = gold * pulse;
                dyn.width = 0.18f;
                dyn.Circle(tt.pos, 1.4f + 0.3f * pulse);
            }
            if (meter.active)
            {
                if (meter.red != null)
                {
                    dynGlow.Disc(meter.red.pos, 1.1f, new Color(1f, 0.15f, 0.1f) * 0.8f);
                    dyn.Disc(meter.red.pos, 0.55f, new Color(1f, 0.25f, 0.2f));
                }
                if (meter.black != null)
                {
                    dynGlow.Disc(meter.black.pos, 1.1f, new Color(0.6f, 0.65f, 0.75f) * 0.6f);
                    dyn.Disc(meter.black.pos, 0.55f, new Color(0.9f, 0.92f, 0.98f));
                }
                if (hoverTerm != null)
                {
                    dyn.color = new Color(1f, 1f, 1f, 0.8f);
                    dyn.width = 0.12f;
                    dyn.Circle(hoverTerm.pos, 1.0f);
                }
            }
            var hl = new Color(0.55f, 0.8f, 1f, 1f);
            if (hoverWire != null)
                for (int i = 0; i < hoverWire.pts.Length - 1; i++)
                    dynGlow.Line(hoverWire.pts[i], hoverWire.pts[i + 1], 1.6f, hl * 0.55f);
            if (hoverSpot != null)
            {
                dyn.color = new Color(0.55f, 0.8f, 1f, 0.55f);
                dyn.width = 0.1f;
                var r = hoverSpot.rect;
                dyn.Rect(r.min - Vector2.one * 0.3f, r.max + Vector2.one * 0.3f);
            }
            dyn.Build(dynMesh);
            dynGlow.Build(dynGlowMesh);
        }

        void UpdateLabels()
        {
            float wpp = 2f * cam.orthographicSize / Mathf.Max(1, Screen.height);
            bool lod = Mathf.Abs(wpp - lastWpp) > lastWpp * 0.03f || lastNumbers != showNumbers || lastCautions != showCautions;
            if (lod)
            {
                lastWpp = wpp;
                lastNumbers = showNumbers;
                lastCautions = showCautions;
                cautionRenderer.enabled = showCautions;
            }
            foreach (var l in lbls)
            {
                if (l.item?.dynamic != null)
                {
                    string s = l.item.dynamic();
                    if (s != l.last)
                    {
                        l.last = s;
                        l.t.text = s;
                    }
                }
                if (!lod) continue;
                // Fade small text out when it is too small to read.
                float px = l.t.fontSize / FontScale / wpp;
                float a = Mathf.InverseLerp(3.5f, 7f, px);
                if (l.wireNo && !showNumbers) a = 0;
                if (l.caution && !showCautions) a = 0;
                bool on = a > 0.01f;
                if (l.t.enabled != on) l.t.enabled = on;
                if (on) l.t.alpha = a;
            }
        }

        // ------------------------------------------------------------------ automated screenshots

        IEnumerator AutoShots(string dir)
        {
            Directory.CreateDirectory(dir);
            var qf0 = circuit.Get<Breaker>("QF0");
            var s2 = circuit.Get<PushButton>("S2");
            var s0 = circuit.Get<EStop>("S0");
            for (int i = 0; i < 10; i++) yield return null;
            yield return Shot(dir, "01_all_off");
            qf0.closed = true;
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "02_qf0_on");
            s2.uiHold = true;
            yield return new WaitForSeconds(0.3f);
            s2.uiHold = false;
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "03_machine_on");
            a11.SetGrade(6);
            a11.ToggleSpindle();
            a11.jog[0] = 1;
            yield return new WaitForSeconds(1.5f);
            yield return Shot(dir, "04_spindle_jog");
            rig.Focus(new Vector2(62, 108), 34);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "05_zoom_e2");
            rig.Focus(new Vector2(228, 92), 40);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "06_zoom_a11");
            a11.jog[0] = 0;
            rig.Focus(new Vector2(150, 230), 45);
            yield return new WaitForSeconds(0.3f);
            simScreen = cam.WorldToScreenPoint(new Vector3(142, 195, 0));
            yield return new WaitForSeconds(0.2f);
            yield return Shot(dir, "07_zoom_e1_hover116");
            simScreen = cam.WorldToScreenPoint(new Vector3(156, 192, 0));
            yield return new WaitForSeconds(0.2f);
            yield return Shot(dir, "07b_caution_c8");
            simScreen = null;
            rig.Fit();
            a11.HomeAll();
            yield return new WaitForSeconds(9f);
            yield return Shot(dir, "07c_homing");
            rig.Focus(new Vector2(165, 88), 32);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "07d_sensors");
            rig.Fit();
            a11.RunJob();
            yield return new WaitForSeconds(2.5f);
            yield return Shot(dir, "08_job");
            panel.SetFull(true);
            yield return new WaitForSeconds(0.6f);
            yield return Shot(dir, "10_door3d_job");
            panel.FocusHandheld();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "11_handheld3d");
            panel.FocusDoor();
            s0.pressed = true;
            yield return new WaitForSeconds(1.0f);
            yield return Shot(dir, "12_door3d_estop");
            s0.pressed = false;
            panel.FocusInterior();
            yield return new WaitForSeconds(1.2f);
            yield return Shot(dir, "13_cabinet_open_off");
            circuit.Get<PushButton>("S2").uiHold = true;
            yield return new WaitForSeconds(0.3f);
            circuit.Get<PushButton>("S2").uiHold = false;
            a11.ToggleSpindle();
            a11.jog[1] = 1;
            yield return new WaitForSeconds(1.5f);
            yield return Shot(dir, "14_cabinet_live");
            panel.Focus(new Vector3(-0.04f, 0.36f, 0.17f), 12, 8, 0.5f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "15_cabinet_rows12");
            panel.Focus(new Vector3(-0.06f, -0.2f, 0.18f), -14, 6, 0.6f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "16_cabinet_drives");
            a11.jog[1] = 0;
            panel.Focus(new Vector3(-0.37f, 0.12f, 0.03f), -55, 12, 0.7f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "17_hinge_loop");
            panel.showCovers = true;
            panel.FocusInterior();
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "18_covers_on");
            panel.showCovers = false;
            panel.ToggleDoor();
            panel.FocusDoor();
            yield return new WaitForSeconds(1.2f);
            panel.SetFull(false);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "09_estop");
            Application.Quit();
        }

        /// Clicks the 3D door through the real raycast path (inset and full view) and logs what the circuit does.
        /// Exterior views of the enclosure (-a11views <dir>): fan side, outlet side, top, bottom (cable entry), three-quarter.
        IEnumerator EnclosureViews(string dir)
        {
            Directory.CreateDirectory(dir);
            for (int i = 0; i < 40; i++) yield return null;
            panel.SetFull(true);
            hud.hidden = true;
            var poses = new (string name, Vector3 target, float yaw, float pitch, float dist)[]
            {
                ("v1_fan_side", new Vector3(-0.4f, -0.12f, 0.11f), 80, 8, 0.75f),
                ("v2_outlet_side", new Vector3(0.4f, 0.4f, 0.11f), -80, 8, 0.75f),
                ("v3_top", new Vector3(0, 0.5f, 0.12f), 20, 55, 1.3f),
                ("v4_bottom", new Vector3(0, -0.5f, 0.12f), 15, -55, 1.3f),
                ("v5_three_quarter", new Vector3(0, 0, 0.12f), -35, 18, 2.6f),
            };
            foreach (var (name, target, yaw, pitch, dist) in poses)
            {
                panel.Focus(target, yaw, pitch, dist);
                for (int i = 0; i < 10; i++) yield return null;
                yield return Shot(dir, name);
            }
            Application.Quit();
        }

        /// GUI / frame benchmark (-a11perf <file>): three scenes, uncapped frame rate, main-thread allocations per frame.
        IEnumerator PerfTest(string file)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var sb = new StringBuilder();
            sb.AppendLine($"GPU {SystemInfo.graphicsDeviceName} · {Screen.width}x{Screen.height}");
            sb.AppendLine("scene | fps avg | frame ms avg / p95 | HUD ms/frame | HUD events/frame | view ms | panel ms | alloc KB/frame | GC runs");
            for (int i = 0; i < 60; i++) yield return null;

            IEnumerator Measure(string label, int frames)
            {
                for (int i = 0; i < 30; i++) yield return null; // settle
                var ft = new List<float>(frames);
                PerfStats.Reset();
                PerfStats.alloc.Clear();
                PerfStats.time.Clear();
                long alloc = 0, heap = GC.GetTotalMemory(false);
                int gc0 = GC.CollectionCount(0);
                double t0 = PerfStats.Now;
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    ft.Add(Time.unscaledDeltaTime * 1000f);
                    long h = GC.GetTotalMemory(false);
                    if (h > heap) alloc += h - heap; // growth between frames = allocations (a GC run drops the heap)
                    heap = h;
                }
                double wall = PerfStats.Now - t0;
                ft.Sort();
                sb.AppendLine($"{label} | {frames * 1000.0 / wall:0} | {wall / frames:0.00} / {ft[(int)(frames * 0.95f)]:0.00} | {PerfStats.hudMs / frames:0.000} | " +
                              $"{PerfStats.hudEvents / (float)frames:0.0} | {PerfStats.viewMs / frames:0.000} | {PerfStats.panelMs / frames:0.000} | " +
                              $"{alloc / 1024.0 / frames:0.0} | {GC.CollectionCount(0) - gc0}");
                foreach (var kv in PerfStats.alloc.OrderByDescending(k => k.Value))
                    sb.AppendLine($"    {kv.Key}: {kv.Value / 1024.0 / frames:0.0} KB/frame");
                foreach (var kv in PerfStats.time.OrderByDescending(k => k.Value))
                    sb.AppendLine($"    {kv.Key}: {kv.Value / frames:0.000} ms/frame");
                PerfStats.alloc.Clear();
                PerfStats.time.Clear();
            }

            PerfStats.probe = true;
            yield return Measure("schematic, machine off", 600);
            circuit.Get<Breaker>("QF0").closed = true;
            var s2 = circuit.Get<PushButton>("S2");
            s2.uiHold = true;
            for (int i = 0; i < 20; i++) yield return null;
            s2.uiHold = false;
            a11.RunJob();
            meter.active = true;
            meter.mode = MeterMode.VDC;
            meter.red = circuit.T("F2.out");
            meter.black = circuit.T("G2.-V1");
            proc.Start(1);
            yield return Measure("schematic, job running + meter + procedure", 600);
            proc.Stop();
            panel.SetFull(true);
            panel.FocusInterior();
            guide.Go(40);
            guide.active = true;
            yield return Measure("3D cabinet open, live wires + build guide + meter", 600);
            guide.active = false;
            guide.Go(0);
            File.WriteAllText(file, sb.ToString());
            Application.Quit();
        }

        /// Walks both commissioning procedures with the meter and steps the build guide (-a11tools <dir>).
        IEnumerator ToolsTest(string dir)
        {
            Directory.CreateDirectory(dir);
            var log = new StringBuilder();
            var qf = new[] { "QF0", "QF1", "QF2", "QF3" }.Select(circuit.Get<Breaker>).ToArray();
            var s0 = circuit.Get<EStop>("S0");
            var s1 = circuit.Get<PushButton>("S1");
            var s2 = circuit.Get<PushButton>("S2");
            var k1 = circuit.Get<RelayK1>("K1");
            for (int i = 0; i < 30; i++) yield return null;

            IEnumerator Wait(float s)
            {
                float end = Time.time + s;
                while (Time.time < end) yield return null;
            }
            IEnumerator Press(PushButton b)
            {
                b.uiHold = true;
                yield return Wait(0.25f);
                b.uiHold = false;
                yield return Wait(0.15f);
            }

            int pass = 0, fail = 0;
            for (int p = 0; p < 2; p++)
            {
                foreach (var b in qf) b.closed = false;
                s0.pressed = false;
                yield return Wait(0.2f);
                proc.Start(p);
                meter.Clear();
                while (proc.Current != null)
                {
                    var st = proc.Current;
                    string t = st.title;
                    s1.uiHold = s2.uiHold = false;
                    k1.testLever = false;
                    if (p == 0)
                    {
                        meter.mode = MeterMode.Ohm;
                        if (t == "Test 5") s2.uiHold = true;
                        if (t == "Test 6") { s0.pressed = true; s2.uiHold = true; }
                        if (t == "Test 7") { s0.pressed = false; s1.uiHold = s2.uiHold = true; }
                        if (t == "Test 9") k1.testLever = true;
                        if (t == "Test 10a") s0.pressed = false;
                        if (t == "Test 10b") s0.pressed = true;
                        if (t == "Test 11") s0.pressed = false;
                    }
                    else
                    {
                        if (t == "Stage 1 · power") qf[0].closed = true;
                        if (t == "Step 5" || t == "Step 9") yield return Press(s2);
                        if (t == "Step 6" || t == "Step 10") yield return Press(s1);
                        if (t == "Step 7") { yield return Press(s2); s0.pressed = true; }
                        if (t == "Stage 2 · step 8") s0.pressed = false;
                        if (t == "Stage 3 · prepare") { yield return Press(s2); qf[2].closed = true; }
                        if (t == "Step 12")
                            for (int a = 0; a < 3; a++)
                            {
                                a11.jog[a] = 1;
                                yield return Wait(0.3f);
                                a11.jog[a] = 0;
                            }
                        if (t.StartsWith("Stage 4")) qf[1].closed = true;
                        if (t == "Spindle") a11.ToggleSpindle();
                    }
                    if (st.IsMeasure) proc.ShowMe();
                    yield return Wait(st.title == "Spindle" ? 1.5f : 0.3f);
                    var rd = meter.Read();
                    string res = st.passed ? "PASS" : "FAIL";
                    if (st.passed) pass++;
                    else fail++;
                    log.AppendLine($"{Commissioning.Names[p]} · {t}: {res}" + (st.IsMeasure ? $" · meter {rd.value} {rd.unit} ({rd.note})" : "") + (st.passed ? "" : " · todo: " + proc.Todo()));
                    if (p == 0 && t == "Test 5") yield return Shot(dir, "t1_proc71_test5");
                    if (p == 1 && t == "Caution C2") yield return Shot(dir, "t2_proc72_c2");
                    proc.Next();
                }
                s1.uiHold = s2.uiHold = false;
                yield return Shot(dir, p == 0 ? "t3_proc71_done" : "t4_proc72_done");
                proc.Stop();
            }

            // Meter in the open cabinet.
            meter.active = true;
            meter.mode = MeterMode.VDC;
            meter.red = circuit.T("F2.out");
            meter.black = circuit.T("G2.-V1");
            panel.SetFull(true);
            panel.FocusInterior();
            yield return Wait(1.2f);
            panel.Focus(new Vector3(0.09f, 0.33f, 0.2f), 0, 4, 0.75f);
            yield return Wait(0.4f);
            yield return Shot(dir, "t5_meter_3d");
            foreach (var id in new[] { "F2.out", "G2.-V1", "K1.A1", "QF0.1" })
                if (panel.Cabinet.TryWorld(circuit.T(id), out var wpt))
                {
                    var sp = panel.cam.WorldToScreenPoint(wpt);
                    var got = panel.Cabinet.PickTerminal(sp, panel.cam, 14f);
                    log.AppendLine($"3D pick at {id} → {got?.id ?? "none"} {(got != null && meter.OnConductor(got, id) ? "(same conductor)" : "")}");
                }

            // Build guide.
            meter.active = false;
            guide.Go(0);
            guide.active = true;
            log.AppendLine($"Build guide: {guide.items.Count} wires; first {guide.items[0].w.number} ({BuildGuide.Groups[guide.items[0].group]}), last {guide.items[^1].w.desc}");
            for (int g = 0; g < BuildGuide.Groups.Length; g++) log.AppendLine($"  {BuildGuide.Groups[g]} starts at {guide.GroupStart(g) + 1}");
            guide.Go(guide.GroupStart(3) + 2);
            var it = guide.Current;
            if (panel.Cabinet.WireFocus(it.w, out var c, out float size))
            {
                float d = Mathf.Clamp(size * 2.3f, 0.55f, 2.4f);
                panel.FocusSmooth(c + new Vector3(0, 0.1f * d, 0), d);
            }
            yield return Wait(1.5f);
            log.AppendLine($"Guide step {guide.index + 1}: wire {it.w.number} {it.w.spec} cut {BuildGuide.Cut(it.routed):0.00} m · {guide.Termination(it.w.a, it.w)} / {guide.Termination(it.w.b, it.w)}");
            yield return Shot(dir, "t6_guide_24v");
            panel.FocusInterior();
            yield return Wait(1.0f);
            yield return Shot(dir, "t7_guide_overview");
            guide.active = false;
            guide.Go(0);

            log.Insert(0, $"Procedures: {pass} pass, {fail} fail\n");
            File.WriteAllText(Path.Combine(dir, "tools_log.txt"), log.ToString());
            Application.Quit();
        }

        IEnumerator Test3D(string dir)
        {
            Directory.CreateDirectory(dir);
            var log = new StringBuilder();
            var qf0 = circuit.Get<Breaker>("QF0");
            var k1 = circuit.Get<RelayK1>("K1");
            var km1 = circuit.Get<Contactor>("KM1");
            var s0 = circuit.Get<EStop>("S0");
            var h1 = circuit.Get<Lamp>("H1");
            var h3 = circuit.Get<Lamp>("H3");
            void L(string s) => log.AppendLine(s);
            for (int i = 0; i < 30; i++) yield return null;

            IEnumerator Click(string cap, float hold)
            {
                var p = panel.ScreenPointOf(cap);
                if (p == null)
                {
                    L($"  {cap}: NOT FOUND");
                    yield break;
                }
                panel.simMouse = p;
                yield return null;
                yield return null;
                L($"  click {cap} at {p.Value} → ray hit '{panel.lastHit}'");
                panel.simHeld = true;
                yield return new WaitForSeconds(hold);
                panel.simHeld = false;
                yield return null;
                yield return null;
                panel.simMouse = null;
                yield return new WaitForSeconds(0.2f);
            }

            L("== INSET (left panel) ==");
            L($"inset rect {panel.InsetRect} screen {Screen.width}x{Screen.height}");
            {
                var pc = panel.cam;
                var p = panel.ScreenPointOf("button I").Value;
                var r = panel.InsetRect;
                var vp = new Vector3((p.x - r.x) / r.width, (p.y - r.y) / r.height, 0);
                var unityRay = pc.ViewportPointToRay(vp);
                var ray = panel.InsetRay(vp);
                L($"  cam pos {pc.transform.position.ToString("F4")} fwd {pc.transform.forward.ToString("F4")} target {(pc.targetTexture ? pc.targetTexture.width + "x" + pc.targetTexture.height : "screen")} aspect {pc.aspect} enabled {pc.enabled}");
                L($"  vp {vp.ToString("F4")}");
                L($"  Unity ViewportPointToRay dir {unityRay.direction.ToString("F4")} hits: {string.Join(", ", Array.ConvertAll(Physics.RaycastAll(unityRay, 50f), h => h.collider.name))}");
                L($"  explicit inset ray      dir {ray.direction.ToString("F4")} hits: {string.Join(", ", Array.ConvertAll(Physics.RaycastAll(ray, 50f), h => h.collider.name))}");
                var cap = GameObject.Find("button I");
                L($"  cap world {(cap ? cap.transform.position.ToString("F4") : "none")}");
            }
            yield return Click("button I", 0.3f);
            L($"QF0 OFF, ON clicked → K1={k1.on} (expected False: no 24 V control supply) · hint shown: '{toast}'");
            yield return Shot(dir, "t0_hint_qf0_off");
            qf0.closed = true;
            yield return new WaitForSeconds(0.3f);
            yield return Click("button I", 0.3f);
            L($"QF0 ON, ON clicked → K1={k1.on} KM1={km1.on} POWER lamp={h1.lit}");
            yield return Shot(dir, "t1_inset_on");
            yield return Click("button O", 0.3f);
            L($"OFF clicked → K1={k1.on} KM1={km1.on} POWER lamp={h1.lit}");

            L("== FULL 3D VIEW ==");
            panel.SetFull(true);
            yield return new WaitForSeconds(0.5f);
            yield return Click("button I", 0.3f);
            L($"ON clicked → K1={k1.on} KM1={km1.on} POWER lamp={h1.lit}");
            yield return Click("E-stop mushroom", 0.15f);
            L($"E-stop clicked → pushed={s0.pressed} K1={k1.on} ALARM lamp={h3.lit}");
            yield return Shot(dir, "t2_full_estop");
            yield return Click("E-stop mushroom", 0.15f);
            L($"E-stop clicked again → pushed={s0.pressed} K1={k1.on} (expected False: no auto restart)");
            yield return Click("button I", 0.3f);
            L($"ON clicked → K1={k1.on} KM1={km1.on}");
            panel.FocusHandheld();
            yield return new WaitForSeconds(0.4f);
            yield return Click("key ON/OFF", 0.15f);
            L($"handheld ON/OFF → spindle={a11.spindleOn}");
            float x0 = a11.dro[0];
            yield return Click("key X+", 0.8f);
            L($"handheld X+ held 0.8 s → DRO X {x0:0.0} → {a11.dro[0]:0.0}");

            L("== CABINET (door open) ==");
            panel.FocusInterior();
            yield return new WaitForSeconds(1.2f);
            IEnumerator ClickAt(string what, Vector3 local)
            {
                for (int f = 0; f < 3; f++)
                {
                    panel.simMouse = panel.ScreenPointOfLocal(local);
                    yield return null;
                }
                panel.simMouse = panel.ScreenPointOfLocal(local);
                var ray = panel.cam.ScreenPointToRay(panel.simMouse.Value);
                var aimW = panel.root.TransformPoint(local);
                float miss = Vector3.Cross(ray.direction, aimW - ray.origin).magnitude;
                L($"  ray passes {miss * 1000:0.0} mm from the aim point");
                var all = Physics.RaycastAll(ray, 20f, 1 << Panel3D.Layer);
                Array.Sort(all, (p, q) => p.distance.CompareTo(q.distance));
                L($"  cam {panel.cam.transform.position.ToString("F3")} all hits: {string.Join(" | ", Array.ConvertAll(all, h => (h.collider.transform.parent ? h.collider.transform.parent.name + "/" : "") + h.collider.name + "@" + h.distance.ToString("0.000")))}");
                L($"  click {what} → ray hit '{panel.lastHit}'");
                panel.simHeld = true;
                yield return new WaitForSeconds(0.15f);
                panel.simHeld = false;
                yield return null;
                panel.simMouse = null;
                yield return new WaitForSeconds(0.3f);
            }
            var qf1 = circuit.Get<Breaker>("QF1");
            var vfdDev = circuit.Get<Vfd>("U1");
            yield return ClickAt("QF1 lever", new Vector3(-0.0155f, 0.3825f, 0.162f));
            L($"QF1 → closed={qf1.closed} VFD powered={vfdDev.powered} (expected False/False)");
            yield return ClickAt("QF1 lever", new Vector3(-0.0155f, 0.3825f, 0.162f));
            L($"QF1 → closed={qf1.closed} VFD powered={vfdDev.powered} (expected True/True)");
            var f1 = circuit.Get<Fuse>("F1");
            yield return ClickAt("F1 carrier", new Vector3(0.0791f, 0.3825f, 0.19f));
            L($"F1 → inserted={f1.inserted} K1={k1.on} (expected False/False)");
            yield return Shot(dir, "t4_cabinet_f1_open");
            yield return ClickAt("F1 carrier", new Vector3(0.0791f, 0.3825f, 0.19f));
            L($"F1 → inserted={f1.inserted}");
            var a2 = circuit.Get<StepperDrive>("A2");
            yield return ClickAt("drive A2", new Vector3(-0.176f, -0.13f, 0.193f));
            L($"A2 → fault={a2.fault} A11 X5={a11.X[5]} alarm={a11.Alarm}");
            yield return Shot(dir, "t3_handheld");
            File.WriteAllText(Path.Combine(dir, "test3d.txt"), log.ToString());
            Application.Quit();
        }

        IEnumerator QuickCheck(string dir)
        {
            Directory.CreateDirectory(dir);
            for (int i = 0; i < 90; i++) yield return null;
            Debug.Log($"[A11] Screen {Screen.width}x{Screen.height} mode={Screen.fullScreenMode} display={Display.main.systemWidth}x{Display.main.systemHeight} " +
                      $"GPU={SystemInfo.graphicsDeviceName} API={SystemInfo.graphicsDeviceType} MSAA={QualitySettings.antiAliasing}");
            yield return Shot(dir, "quick_borderless");
            Application.Quit();
        }

        IEnumerator Shot(string dir, string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
