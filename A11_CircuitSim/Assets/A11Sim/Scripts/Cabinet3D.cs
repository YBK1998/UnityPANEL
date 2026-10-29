using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace A11Sim
{
    /// Inside of the enclosure at real size, rev 0.7 (wired directly; no terminal blocks, only the 230 V L / N distribution blocks).
    /// Enclosure: the bought 800 × 1000 × 250 mm steel wall cabinet with a swing door hinged on the left (seen from the
    /// front), no removable panels (cable glands drilled through the bottom wall) and its own galvanised mounting plate.
    /// The equipment layout (539 × 930 field, 40 × 60 ducts, 100 mm above the VFD) sits centred on that plate.
    /// Part sizes and their sources: Verification/A11_Enclosure_Selection.md.
    /// Every rev 0.7 wire is routed terminal → duct → duct → terminal with its cross-section and colour.
    /// Segregation: the incoming mains and the shielded VFD → spindle cable W1 run in their own power lane on the far left
    /// (VP), the stepper motor cables in their own motor lane on the far right (VM); both use the spare plate width.
    ///
    /// Units: metres, cabinet coordinates (x right, y up, z into the cabinet; door front plane z ≈ −0.008).
    public class Cabinet3D
    {
        const int Layer = Panel3D.Layer;

        // Enclosure 800 × 1000 × 250. Mounting plate: MEASURED 730 W × 930 H (owner, 2026-10-26).
        public const float EncW = 0.8f, EncH = 1.0f, EncD = 0.25f, PlateW = 0.73f, PlateH = 0.93f;
        // Body: front frame z = -0.004 (gasket + door to the front plane -0.008), back outer face z = 0.242 (250 overall),
        // sheet 1.5 mm. Mounting plate front face Zb on welded studs.
        const float ZFront = -0.004f, ZBack = 0.242f, Ts = 0.0015f;
        const float Zb = ZBack - Ts - 0.008f - 0.003f; // mounting plate front face (3 mm plate on 8 mm standoffs)
        const float Zr = Zb - 0.0075f;         // TS35 rail front face
        const float DuctDepth = 0.06f;         // 40 wide × 60 deep
        const float DuctHalf = 0.02f;
        const float Zd = Zb - DuctDepth * 0.5f;
        // Horizontal ducts D0…D4 (y centre) and vertical ducts at the plate edges (x centre).
        // D0 and D4 sit on the plate's top and bottom edges. D2 and D3 sit 25 mm lower than in commit 966028f so the
        // measured VFD (205 high) keeps 100 mm clear above it; row D (A11 board, 90 high) is 150 high instead of 175.
        static readonly float[] HD = { 0.445f, 0.295f, -0.055f, -0.255f, -0.445f };
        const float VLx = -0.2495f, VRx = 0.2495f;
        const float DuctX = 0.2295f;           // inner edge of the vertical ducts
        const int VL = 5, VR = 6;
        // Outer lanes on the spare plate width (40 × 60 duct): VP = power (mains in, VFD cable W1), VM = stepper motor cables.
        const float VPx = -0.3225f, VMx = 0.3225f;
        // VM is shortened to 230 mm: it starts at the top edge of D3, where the motor cables (drive power plugs at the
        // bottom) enter it. That frees the strip above it for the L / N busbars and the braking resistor on the right wall.
        const float VMTop = -0.235f;
        const int VP = 7, VM = 8;
        static float VX(int d) => d == VL ? VLx : d == VR ? VRx : d == VP ? VPx : VMx;
        // Rows: A = DIN rail (220 V + control), B = VFD and PSUs, C = drives, D = A11 board.
        const float RailA = 0.37f;
        // TS35 rail ends: from 5 mm right of the plate-mounted EMI filter to just past the Schuko socket (330 mm).
        const float RailL = -0.1685f, RailR = 0.1615f;
        const float RowBBottom = -0.03f;
        const float DriveTop = -0.08f;
        const float A11y = -0.35f;

        // Hinge crossings (door-local on the door side, cabinet-local on the cabinet side); the door hangs on the left.
        static readonly Vector3 HingeDoorUpper = new(-0.372f, 0.2f, 0.045f), HingeCabUpper = new(-0.387f, 0.2f, 0.06f);
        static readonly Vector3 HingeDoorLower = new(-0.372f, -0.43f, 0.03f), HingeCabLower = new(-0.39f, -0.43f, 0.045f);
        const float DoorSpineX = -0.3f, DoorSpineZ = 0.052f;
        // Owner's 80 × 80 mm 24 V DC fan (measured; 25 mm deep assumed) in the left wall, blowing in, and an outlet in the
        // right wall. Both carry the owner's plastic 80 mm filter adapter, a flanged housing slightly larger than the fan
        // (drawn 95 × 95, 15 proud of the wall — assumed). Cut-outs 80 × 80, made on assembly.
        static readonly Vector3 FanAt = new(-EncW / 2, -0.12f, 0.092f), OutletAt = new(EncW / 2, 0.40f, 0.092f);
        const float FanCut = 0.08f, FanDepth = 0.025f, FilterFace = 0.095f, FilterProud = 0.015f;

        readonly Panel3D P;
        readonly SchematicView V;
        readonly Circuit C;
        Transform root, door, cab;

        // Materials.
        Material mSheet, mGalv, mRail, mDuct, mWhite, mDark, mBlack, mGrey, mBlue, mGY, mScrew, mAlu, mVfd, mDrive, mPcb,
            mPlug, mBrass, mRelay, mOrange, mRed, mCopper, mSleeve;

        struct Anc
        {
            public Vector3 p;
            public int duct;
            public bool door;
        }

        readonly Dictionary<string, List<Anc>> anchors = new();
        readonly Dictionary<string, int> taken = new();

        class WireVis
        {
            public Wire w;
            public List<MeshRenderer> rends = new();
            public MaterialPropertyBlock mpb = new();
            public Color last = new(-1, 0, 0);
            public float len;
            public bool door;
            public readonly List<GameObject> markers = new();
            public readonly List<Material> mats = new();
            public int buildState = 1;
            public bool ghost;
        }

        // Click on a wire: every other wire turns into a faint ghost so this one can be followed through the ducts.
        WireVis selected;
        readonly Dictionary<Material, Material> ghostMats = new();

        public bool HasWireSelection => selected != null;
        public void ClearWireSelection() => selected = null;

        void ToggleSelect(WireVis wv) => selected = selected == wv ? null : wv;

        Material Ghost(Material m)
        {
            if (ghostMats.TryGetValue(m, out var g)) return g;
            var c = m.color;
            g = new Material(Shader.Find("A11/Ghost")) { color = new Color(c.r, c.g, c.b, 0.1f), mainTexture = m.mainTexture };
            return ghostMats[m] = g;
        }

        /// Routed conductors with their length (m, along the duct route) for the build guide.
        public IEnumerable<(Wire w, float len, bool door)> RoutedWires()
        {
            foreach (var wv in wires)
                if (wv.w != null) yield return (wv.w, wv.len, wv.door);
        }

        /// Centre (cabinet-local) and size of a routed wire, to frame it.
        public bool WireFocus(Wire w, out Vector3 center, out float size)
        {
            center = Vector3.zero;
            size = 0;
            foreach (var wv in wires)
            {
                if (wv.w != w || wv.rends.Count == 0) continue;
                var b = wv.rends[0].bounds;
                foreach (var r in wv.rends) b.Encapsulate(r.bounds);
                center = root.InverseTransformPoint(b.center);
                size = b.size.magnitude;
                return true;
            }
            return false;
        }

        /// World position of a terminal's wiring point (first anchor), on the door or in the cabinet.
        public bool TryWorld(Terminal t, out Vector3 world)
        {
            world = default;
            if (t == null || !anchors.TryGetValue(t.id, out var l) || l.Count == 0) return false;
            var a = l[0];
            world = (a.door ? door : root).TransformPoint(a.p);
            return true;
        }

        /// Terminal whose wiring point is nearest to a screen point (pixels, y up), if within maxPx and visible.
        public Terminal PickTerminal(Vector2 screen, Camera cam, float maxPx)
        {
            Terminal best = null;
            float bestD = maxPx;
            bool open = P.doorAngle > 60f;
            foreach (var kv in anchors)
            {
                if (!open || !C.byId.TryGetValue(kv.Key, out var t)) continue;
                foreach (var a in kv.Value)
                {
                    var sp = cam.WorldToScreenPoint((a.door ? door : root).TransformPoint(a.p));
                    if (sp.z <= 0) continue;
                    float d = Vector2.Distance(screen, sp);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = t;
                    }
                }
            }
            return best;
        }

        // Probe tips and target markers (multimeter, procedures, build guide).
        Transform probeRed, probeBlack, target1, target2;
        Material targetMat;

        Transform Marker(Color c, float d)
        {
            var m = P.ME(c, c * 1.6f, 0.6f);
            var go = P.Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * d, m);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        /// Probe: a tip on the terminal and a 9 cm body pointing at the viewer.
        Transform Probe(Color c)
        {
            var t = new GameObject("probe").transform;
            t.gameObject.layer = Layer;
            t.SetParent(root, false);
            var m = P.ME(c, c * 0.5f, 0.7f);
            var tip = P.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.006f, P.ME(new Color(0.8f, 0.8f, 0.82f), new Color(0.3f, 0.3f, 0.3f), 0.9f));
            var body = P.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.05f), new Vector3(0.008f, 0.04f, 0.008f), m, new Vector3(90, 0, 0));
            var grip = P.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0, 0.012f), new Vector3(0.0035f, 0.008f, 0.0035f), m, new Vector3(90, 0, 0));
            foreach (var r in new[] { tip, body, grip }) r.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            t.gameObject.SetActive(false);
            return t;
        }

        void Place(Transform m, Terminal t, float pulse = 1f)
        {
            bool on = TryWorld(t, out var w) && P.doorAngle > 60f;
            if (m.gameObject.activeSelf != on) m.gameObject.SetActive(on);
            if (!on) return;
            m.position = w;
            if (m.name == "probe")
            {
                var toCam = P.cam.transform.position - w;
                m.rotation = Quaternion.LookRotation(Vector3.Lerp(toCam.normalized, Vector3.up, 0.35f));
            }
            else if (pulse != 1f) m.localScale = Vector3.one * 0.011f * pulse;
        }

        public void UpdateTools(Multimeter meter, Commissioning proc, BuildGuide guide)
        {
            if (probeRed == null)
            {
                probeRed = Probe(new Color(0.85f, 0.06f, 0.04f));
                probeBlack = Probe(new Color(0.06f, 0.06f, 0.07f));
                target1 = Marker(new Color(1f, 0.8f, 0.1f), 0.011f);
                target2 = Marker(new Color(1f, 0.8f, 0.1f), 0.011f);
                targetMat = target1.GetComponent<Renderer>().sharedMaterial;
                target2.GetComponent<Renderer>().sharedMaterial = targetMat;
            }
            Place(probeRed, meter.active ? meter.red : null);
            Place(probeBlack, meter.active ? meter.black : null);
            Terminal ta = null, tb = null;
            if (proc.Active && proc.Current is { IsMeasure: true, passed: false } step)
            {
                ta = C.T(step.red);
                tb = C.T(step.black);
            }
            else if (guide.active && guide.Current != null)
            {
                ta = guide.Current.w.a;
                tb = guide.Current.w.b;
            }
            float pulse = 0.8f + 0.35f * Mathf.Sin(Time.time * 7f);
            Place(target1, ta, pulse);
            Place(target2, tb, pulse);
            targetMat.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.1f) * (1.2f + pulse));
        }

        readonly List<Wire> external = new();

        /// Routed length of every conductor (CSV), for the cutting list and the wire list: with the build guide also the
        /// function, the termination of each end and the build-guide group.
        public string LengthReport(BuildGuide guide = null)
        {
            var sb = new System.Text.StringBuilder("no,from,to,spec,routed_m,door,desc,end_from,end_to,group\n");
            static string Q(string s) => "\"" + System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "").Replace("\"", "\"\"") + "\""; // specs may contain commas
            string Extra(Wire w)
            {
                if (guide == null) return ",,,";
                var it = guide.items.Find(i => i.w == w);
                return $",{Q(w.desc)},{Q(guide.Termination(w.a, w))},{Q(guide.Termination(w.b, w))},{Q(it != null ? BuildGuide.Groups[it.group] : "")}";
            }
            foreach (var wv in wires)
                if (wv.w != null)
                    sb.Append($"{wv.w.number},{wv.w.a.id},{wv.w.b.id},{Q(wv.w.spec)},{wv.len.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)},{(wv.door ? 1 : 0)}{Extra(wv.w)}\n");
            foreach (var w in external)
                sb.Append($"{w.number},{w.a.id},{w.b.id},{Q(w.spec)},external,0{Extra(w)}\n");
            return sb.ToString();
        }

        readonly List<WireVis> wires = new();
        readonly List<(Transform t, Func<bool> on)> levers = new();
        readonly List<(Transform t, Func<bool> inserted)> fuseCarriers = new();
        readonly List<(Renderer r, Color c, Func<bool> on)> leds = new();
        readonly List<(Transform t, Func<bool> on, Vector3 off)> flags = new();
        readonly List<(string name, Bounds b)> solids = new();
        readonly List<(string wire, List<Vector3> pts)> routes = new();
        Transform fanRotor;
        Fan fan;
        TextMeshPro vfdDisplay;
        Vfd vfd;
        MeshFilter hingeUpper, hingeLower;
        GameObject covers, dims;
        Light interiorLight;
        int doorWireCountUpper;
        public string fitReport = "";
        public List<string> collisions = new();

        public Cabinet3D(Panel3D p, SchematicView v)
        {
            P = p;
            V = v;
            C = v.circuit;
        }

        // ------------------------------------------------------------------ build

        public void Build()
        {
            root = P.root;
            door = P.door;
            cab = new GameObject("cabinet").transform;
            cab.gameObject.layer = Layer;
            cab.SetParent(root, false);
            Materials();
            Shell();
            Ducts();
            RowA();
            RowB();
            RowC();
            RowD();
            Floor();
            SideWalls();
            DoorRear();
            Wiring();
            CheckCollisions();
            Dimensions();
            interiorLight = new GameObject("cabinet light").AddComponent<Light>();
            interiorLight.gameObject.layer = Layer;
            interiorLight.transform.SetParent(cab, false);
            interiorLight.transform.localPosition = new Vector3(0, 0.42f, 0.05f);
            interiorLight.type = LightType.Point;
            interiorLight.range = 1.4f;
            interiorLight.color = new Color(1f, 0.97f, 0.92f);
            interiorLight.shadows = LightShadows.Soft;
            interiorLight.shadowStrength = 0.6f;
            interiorLight.cullingMask = 1 << Layer;
            interiorLight.intensity = 0;
        }

        void Materials()
        {
            mSheet = P.M(new Color(0.66f, 0.67f, 0.66f), 0.2f, 0.4f);
            mGalv = P.M(new Color(0.70f, 0.72f, 0.74f), 0.85f, 0.45f);
            mRail = P.M(new Color(0.80f, 0.81f, 0.82f), 0.9f, 0.6f);
            mDuct = P.M(new Color(0.52f, 0.54f, 0.55f), 0f, 0.3f);
            mWhite = P.M(new Color(0.92f, 0.92f, 0.90f), 0f, 0.45f);
            mDark = P.M(new Color(0.15f, 0.16f, 0.18f), 0f, 0.4f);
            mBlack = P.M(new Color(0.05f, 0.05f, 0.055f), 0f, 0.5f);
            mGrey = P.M(new Color(0.62f, 0.64f, 0.66f), 0f, 0.35f);
            mBlue = P.M(new Color(0.18f, 0.33f, 0.72f), 0f, 0.4f);
            mGY = P.M(Color.white, 0f, 0.4f);
            mGY.mainTexture = StripeTex(new Color(0.15f, 0.6f, 0.2f), new Color(0.95f, 0.85f, 0.1f));
            mScrew = P.M(new Color(0.75f, 0.76f, 0.78f), 1f, 0.7f);
            mAlu = P.M(new Color(0.78f, 0.79f, 0.80f), 0.75f, 0.5f);
            mVfd = P.M(new Color(0.19f, 0.2f, 0.22f), 0.1f, 0.45f);
            mDrive = P.M(new Color(0.07f, 0.07f, 0.08f), 0.5f, 0.55f);
            mPcb = P.M(new Color(0.05f, 0.33f, 0.15f), 0f, 0.65f);
            mPlug = P.M(new Color(0.1f, 0.5f, 0.22f), 0f, 0.45f);
            mBrass = P.M(new Color(0.78f, 0.6f, 0.25f), 1f, 0.6f);
            mRelay = P.M(new Color(0.16f, 0.24f, 0.42f), 0f, 0.85f);
            mOrange = P.M(new Color(0.9f, 0.45f, 0.08f), 0f, 0.4f);
            mRed = P.M(new Color(0.75f, 0.08f, 0.06f), 0f, 0.4f);
            mCopper = P.M(new Color(0.8f, 0.48f, 0.28f), 1f, 0.55f);
            mSleeve = P.M(new Color(0.95f, 0.95f, 0.92f), 0f, 0.3f);
        }

        static Texture2D StripeTex(Color a, Color b)
        {
            var t = new Texture2D(8, 8) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                t.SetPixel(x, y, ((x + y) % 8) < 4 ? a : b);
            t.Apply();
            return t;
        }

        Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.gameObject.layer = Layer;
            t.SetParent(parent ?? cab, false);
            return t;
        }

        GameObject Box(Transform g, Vector3 c, Vector3 s, Material m) => P.Box(g, c, s, m);
        GameObject Cyl(Transform g, Vector3 c, float d, float len, Material m, Vector3 euler) =>
            P.Prim(PrimitiveType.Cylinder, g, c, new Vector3(d, len / 2, d), m, euler);
        GameObject ScrewHead(Transform g, Vector3 c) => P.Disc(g, c, 0.0036f, 0.0012f, mScrew);

        TextMeshPro Label(Transform g, string s, Vector3 p, float h, Color col, bool bold = false) => P.Text(s, g, p, h, col, bold);

        /// Register a part's body for the wire-through-part check.
        void Solid(string name, Vector3 center, Vector3 size) => solids.Add((name, new Bounds(center, size)));

        void Anchor(string id, Vector3 p, int duct, bool onDoor = false)
        {
            if (!anchors.TryGetValue(id, out var l)) anchors[id] = l = new List<Anc>();
            l.Add(new Anc { p = p, duct = duct, door = onDoor });
        }

        bool TryTake(string id, out Anc a)
        {
            a = default;
            if (!anchors.TryGetValue(id, out var l) || l.Count == 0) return false;
            taken.TryGetValue(id, out int n);
            a = l[n % l.Count];
            taken[id] = n + 1;
            return true;
        }

        void Click(Transform g, Vector3 center, Vector3 size, Func<string> tip, Action down = null, Action up = null)
        {
            var hit = Group("hit", g);
            hit.localPosition = center;
            P.Register(hit.gameObject, size, Vector3.zero, tip, down, up);
        }

        void Led(Transform g, Vector3 p, Color c, Func<bool> on, float d = 0.003f)
        {
            var m = P.ME(c * 0.35f, Color.black, 0.8f);
            var go = P.Prim(PrimitiveType.Sphere, g, p, new Vector3(d, d, d * 0.6f), m);
            leds.Add((go.GetComponent<Renderer>(), c, on));
        }

        static string Dims(float w, float h, float d, string src = "") =>
            $"\n<color=#8fa3bf>Size {w * 1000:0} × {h * 1000:0} × {d * 1000:0} mm (W × H × D){(src != "" ? " · " + src : "")}</color>";

        // ------------------------------------------------------------------ shell, plate, rail, ducts

        void Shell()
        {
            var g = Group("enclosure");
            float hy = EncH / 2, hx = EncW / 2, zc = (ZFront + ZBack) / 2, d = ZBack - ZFront;
            // Body: one welded box (back, sides, top, bottom; no removable panels), 20 mm folded front frame for the door.
            Box(g, new Vector3(0, 0, ZBack - Ts / 2), new Vector3(EncW, EncH, Ts), mSheet);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(g, new Vector3(s * (hx - Ts / 2), 0, zc), new Vector3(Ts, EncH - 2 * Ts, d), mSheet);
                Box(g, new Vector3(0, s * (hy - Ts / 2), zc), new Vector3(EncW, Ts, d), mSheet);
                Box(g, new Vector3(s * (hx - Ts - 0.01f), 0, ZFront + Ts / 2), new Vector3(0.02f, EncH - 2 * Ts, Ts), mSheet);
                Box(g, new Vector3(0, s * (hy - Ts - 0.01f), ZFront + Ts / 2), new Vector3(EncW - 2 * Ts - 0.04f, 0.02f, Ts), mSheet);
            }
            // Wall-mounting lugs at the back corners.
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sy in new[] { -1f, 1f })
                Box(g, new Vector3(sx * (hx - 0.04f), sy * (hy + 0.018f), ZBack - 0.0015f), new Vector3(0.04f, 0.036f, 0.003f), mScrew);
            // Mounting plate (supplied with the enclosure, galvanised) on four welded M6 studs with nuts.
            Box(g, new Vector3(0, 0, Zb + 0.0015f), new Vector3(PlateW, PlateH, 0.003f), mGalv);
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sy in new[] { -1f, 1f })
            {
                var nut = MeshObj(g, "M6 nut", Hex.Prism(), mScrew);
                nut.transform.localPosition = new Vector3(sx * (PlateW / 2 - 0.015f), sy * (PlateH / 2 - 0.015f), Zb);
                nut.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.back);
                nut.transform.localScale = new Vector3(0.01f, 0.005f, 0.01f);
            }
            float rc = (RailL + RailR) / 2, rl = RailR - RailL;
            Box(g, new Vector3(rc, RailA, Zb - 0.00375f), new Vector3(rl, 0.035f, 0.0015f), mRail);
            Box(g, new Vector3(rc, RailA + 0.0165f, Zb - 0.0045f), new Vector3(rl, 0.002f, 0.007f), mRail);
            Box(g, new Vector3(rc, RailA - 0.0165f, Zb - 0.0045f), new Vector3(rl, 0.002f, 0.007f), mRail);
            Click(g, new Vector3(0, 0, Zb), new Vector3(PlateW, PlateH, 0.004f),
                () => $"<b>Mounting plate</b> — supplied with the enclosure, galvanised, on four M6 studs\nSize {PlateW * 1000:0} × {PlateH * 1000:0} (measured). " +
                      $"The layout uses the centre 539 × {PlateH * 1000:0}; component holes are drilled on assembly. " +
                      "Enclosure 800 W × 1000 H × 250 D mm, steel, swing door hinged on the left.");
        }

        /// Slotted 40 × 60 ducts: one combined mesh for the troughs, one for the (optional) covers.
        void Ducts()
        {
            var mb = new MeshBuilder();
            var cb = new MeshBuilder();
            float x0 = -DuctX, x1 = DuctX;
            foreach (float y in HD)
            {
                mb.Box(new Vector3(0, y, Zb - 0.001f), new Vector3(x1 - x0, 2 * DuctHalf, 0.002f));
                for (float x = x0 + 0.003f; x < x1; x += 0.009f)
                foreach (float s in new[] { -1f, 1f })
                    mb.Box(new Vector3(x, y + s * (DuctHalf - 0.001f), Zb - DuctDepth / 2), new Vector3(0.005f, 0.002f, DuctDepth));
                cb.Box(new Vector3(0, y, Zb - DuctDepth - 0.001f), new Vector3(x1 - x0, 2 * DuctHalf + 0.004f, 0.002f));
            }
            float y0 = -PlateH / 2;
            foreach (float x in new[] { VLx, VRx, VPx, VMx })
            {
                float y1 = x == VMx ? VMTop : PlateH / 2;
                mb.Box(new Vector3(x, (y0 + y1) / 2, Zb - 0.001f), new Vector3(2 * DuctHalf, y1 - y0, 0.002f));
                for (float y = y0 + 0.003f; y < y1; y += 0.009f)
                foreach (float s in new[] { -1f, 1f })
                    mb.Box(new Vector3(x + s * (DuctHalf - 0.001f), y, Zb - DuctDepth / 2), new Vector3(0.002f, 0.005f, DuctDepth));
                cb.Box(new Vector3(x, (y0 + y1) / 2, Zb - DuctDepth - 0.001f), new Vector3(2 * DuctHalf + 0.004f, y1 - y0, 0.002f));
            }
            var g = Group("ducts");
            MeshObj(g, "duct troughs", mb.Build(), mDuct);
            covers = MeshObj(g, "duct covers", cb.Build(), mDuct);
            covers.SetActive(false);
            foreach (float y in HD)
                Click(g, new Vector3(0, y, Zb - DuctDepth / 2), new Vector3(2 * DuctX, 0.04f, 0.06f), () => "<b>Wire duct 40 × 60 mm</b>\nSlotted PVC. C = fit / remove the covers.");
            Click(g, new Vector3(VPx, 0, Zb - DuctDepth / 2), new Vector3(0.04f, PlateH, 0.06f),
                () => "<b>Power lane VP</b> (40 × 60 duct on the spare plate width, left)\nOnly the incoming mains (101, 102, 140) and the shielded VFD → spindle cable W1 (123–127). " +
                      "No 24 V or signal wire runs here; the door bundle crosses it at 90°.");
            Click(g, new Vector3(VMx, (VMTop - PlateH / 2) / 2, Zb - DuctDepth / 2), new Vector3(0.04f, VMTop + PlateH / 2, 0.06f),
                () => "<b>Motor lane VM</b> (40 × 60 duct on the spare plate width, right; 230 mm, from the fourth horizontal duct down)\nOnly the three stepper motor cables (LiYCY 4 × 1.0 shielded, shield clamped at the drive end) " +
                      "down to the GX16-6 motor connectors below it. PUL / DIR pairs stay in the inner ducts.");
            // 360° EMC clamp for the W1 shield on the plate where the cable enters the power lane (wire 127 bonds it to VFD PE).
            Box(g, new Vector3(VPx, HD[2] - 0.04f, Zb - 0.004f), new Vector3(0.03f, 0.008f, 0.008f), mGalv);
            Box(g, new Vector3(VPx, HD[2] - 0.04f, Zb - 0.0085f), new Vector3(0.016f, 0.012f, 0.002f), mScrew);
            Click(g, new Vector3(VPx, HD[2] - 0.04f, Zb - 0.006f), new Vector3(0.032f, 0.014f, 0.012f),
                () => "<b>EMC shield clamp</b> for cable W1 (VFD → spindle)\n360° on the bared braid, screwed to the galvanised plate and bonded to VFD PE (wire 127). " +
                      "The other end is clamped 360° in the HDC-HA metal hood, and at the spindle in an EMC gland — both ends, no pigtail.");
        }

        GameObject MeshObj(Transform g, string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        // ------------------------------------------------------------------ row A: DIN rail (220 V, K1, fuses, outlet)

        void RowA()
        {
            // Z1: HD2AC-20A single-phase EMI filter, 51 × 51 × 30 (measured), screwed to the plate at the far left of row A
            // (off the DIN rail). Mounted with the LINE side (2 terminals: L, N in) facing down towards D1 and the LOAD side
            // (3 terminals: L, N out, PE) facing up towards D0.
            var emi = Group("Z1 EMI filter");
            float ex = -0.199f, ew = 0.051f, eh = 0.051f, ed = 0.03f;
            Box(emi, new Vector3(ex, RailA, Zb - ed / 2), new Vector3(ew, eh, ed), P.M(new Color(0.58f, 0.6f, 0.62f), 0.7f, 0.45f));
            Box(emi, new Vector3(ex, RailA + 0.002f, Zb - ed - 0.0005f), new Vector3(0.042f, 0.02f, 0.001f), mWhite);
            Label(emi, "HD2AC-20A", new Vector3(ex, RailA + 0.006f, Zb - ed - 0.0012f), 0.0045f, Color.black, true);
            Label(emi, "EMI FILTER 250 VAC 20 A", new Vector3(ex, RailA - 0.002f, Zb - ed - 0.0012f), 0.0021f, new Color(0.2f, 0.2f, 0.2f));
            string[] eid = { "Z1.LIN", "Z1.NIN", "Z1.LOUT", "Z1.NOUT", "Z1.GND" };
            float[] exs = { ex - 0.008f, ex + 0.008f, ex - 0.014f, ex, ex + 0.014f };
            for (int i = 0; i < 5; i++)
            {
                bool line = i < 2;
                float ty = line ? RailA - eh / 2 + 0.004f : RailA + eh / 2 - 0.004f;
                Box(emi, new Vector3(exs[i], ty, Zb - ed - 0.002f), new Vector3(0.0063f, 0.008f, 0.004f), mScrew);
                Anchor(eid[i], new Vector3(exs[i], line ? RailA - eh / 2 - 0.002f : RailA + eh / 2 + 0.002f, Zb - ed - 0.002f), line ? 1 : 0);
            }
            Label(emi, "LOAD  L  N  PE", new Vector3(ex, RailA + eh / 2 - 0.011f, Zb - ed - 0.0012f), 0.0024f, Color.black);
            Label(emi, "LINE  L  N", new Vector3(ex, RailA - eh / 2 + 0.011f, Zb - ed - 0.0012f), 0.0024f, Color.black);
            foreach (var (dx, dy) in new[] { (-1, -1), (1, 1) })
                ScrewHead(emi, new Vector3(ex + dx * (ew / 2 - 0.004f), RailA + dy * (eh / 2 - 0.004f), Zb - ed - 0.0006f));
            Solid("Z1", new Vector3(ex, RailA, Zb - ed / 2), new Vector3(ew, eh, ed));
            Click(emi, new Vector3(ex, RailA, Zb - ed / 2), new Vector3(ew, eh, ed),
                () => C.Get<EmiFilter>("Z1").Tip() + "\nScrewed to the plate (not DIN rail). LINE side (L, N in) faces down, LOAD side (L, N out, PE) faces up." +
                      Dims(ew, eh, ed, "measured"));

            Mcb(C.Get<Breaker>("QF0"), -0.14f, 2, "C25", 0.053f); // measured 53 wide

            Contactor();

            float[] xs = { -0.0155f, 0.0025f, 0.0205f, 0.0385f };
            string[] tags = { "QF1", "QF2", "QF3", "QF4" };
            string[] ratings = { "C16", "C10", "C10", "C6" };
            for (int i = 0; i < 4; i++) Mcb(C.Get<Breaker>(tags[i]), xs[i], 1, ratings[i]);
            K1(0.062f);
            FuseTerm(C.Get<Fuse>("F1"), 0.085f);
            FuseTerm(C.Get<Fuse>("F2"), 0.1025f);

            // XS1: DIN-rail Schuko socket, Hager SNS016 class 44 × 75 × 66, terminals 2 × 2.5 mm².
            var xs1 = Group("XS1");
            float sx = 0.135f, sw = 0.044f, sh = 0.075f, sd = 0.066f;
            Box(xs1, new Vector3(sx, RailA, Zr - 0.0225f), new Vector3(sw, sh, 0.045f), mWhite);
            Box(xs1, new Vector3(sx, RailA, Zr - 0.0555f), new Vector3(sw, 0.05f, 0.021f), mWhite);
            P.Disc(xs1, new Vector3(sx, RailA, Zr - 0.0665f), 0.036f, 0.002f, mGrey);
            P.Disc(xs1, new Vector3(sx - 0.0095f, RailA, Zr - 0.0678f), 0.005f, 0.0008f, mBlack);
            P.Disc(xs1, new Vector3(sx + 0.0095f, RailA, Zr - 0.0678f), 0.005f, 0.0008f, mBlack);
            Anchor("XS1.L", new Vector3(sx - 0.012f, RailA - sh / 2 - 0.002f, Zr - 0.035f), 1);
            Anchor("XS1.N", new Vector3(sx, RailA - sh / 2 - 0.002f, Zr - 0.035f), 1);
            Anchor("XS1.N", new Vector3(sx + 0.004f, RailA - sh / 2 - 0.002f, Zr - 0.03f), 1);
            Anchor("XS1.PE", new Vector3(sx + 0.012f, RailA - sh / 2 - 0.002f, Zr - 0.035f), 1);
            Solid("XS1", new Vector3(sx, RailA, Zr - sd / 2), new Vector3(sw, sh, sd));
            Click(xs1, new Vector3(sx, RailA, Zr - 0.035f), new Vector3(sw, sh, sd),
                () => C.Get<SchukoPump>("XS1").Tip() + "\nPE now wired (149) — missing in rev 0.5." + Dims(sw, sh, sd, "Hager SNS016"));

            BusBar("XL", 0.29f, new Color(0.08f, 0.08f, 0.09f), "L", "<b>XL — L busbar</b> (230 V, 12 points, 1 conductor per point)\nSwitched L from KM1 2T1 (109) → CB-VFD (114), CB-48V (117), CB-PUMP (120); 8 points spare.");
            BusBar("XN", 0.326f, new Color(0.15f, 0.35f, 0.75f), "N", "<b>XN — N busbar</b> (230 V, 12 points, 1 conductor per point)\nEvery N conductor: EMI N-OUT in (106) → PSU 24 V (113), KM1 coil A1 (132), VFD (116), PSU 48 V (119), pump outlet (122); 6 points spare. " +
                      "KM1 switches L only.");

            // DIN-rail earth: ring lug under the rail's left fixing screw (wire 147).
            var re = Group("rail earth");
            P.Disc(re, new Vector3(RailL + 0.0035f, RailA, Zr - 0.002f), 0.008f, 0.002f, mBrass);
            Anchor("RAIL.T", new Vector3(RailL + 0.0035f, RailA - 0.006f, Zr - 0.003f), 1);
        }

        /// 230 V busbar, 160 × 16 × 42 mm (measured), 12 screw points, mounted vertically on the plate in the strip right of
        /// the right vertical duct VR (free above the shortened motor lane). Wires leave sideways into VR, one per point.
        void BusBar(string tag, float xc, Color insulator, string mark, string tip)
        {
            const float len = 0.16f, w = 0.016f, d = 0.042f, yc = 0.345f;
            var g = Group(tag);
            var ins = P.M(insulator, 0f, 0.45f);
            foreach (float s in new[] { -1f, 1f })
                Box(g, new Vector3(xc, yc + s * (len / 2 - 0.012f), Zb - 0.0125f), new Vector3(w, 0.024f, 0.025f), ins);
            Box(g, new Vector3(xc, yc, Zb - 0.025f - 0.0085f), new Vector3(w - 0.004f, len, 0.017f), mBrass);
            Label(g, tag + " " + mark, new Vector3(xc, yc + len / 2 - 0.006f, Zb - d - 0.0006f), 0.004f, Color.black, true);
            const int n = 12;
            float pitch = (len - 0.03f) / (n - 1);
            for (int k = 0; k < n; k++)
                ScrewHead(g, new Vector3(xc, yc + len / 2 - 0.015f - k * pitch, Zb - d - 0.0006f));
            // Anchors in an interleaved order so the few wires spread along the bar (taken in A11Layout wire order).
            foreach (int k in new[] { 1, 3, 5, 7, 9, 11, 0, 2, 4, 6, 8, 10 })
                Anchor(tag + ".T", new Vector3(xc - 0.004f, yc + len / 2 - 0.015f - k * pitch, Zb - d - 0.004f), VR);
            Solid(tag, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, len, d));
            Click(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, len, d), () => tip + Dims(w, len, d, "measured"));
        }

        void Mcb(Breaker b, float xc, int poles, string rating, float width = 0)
        {
            // Widths measured: 18 mm per pole (QF1–QF4), QF0 53 mm. Height 85 and depth ~75 from the rail: Acti9 iC60 class.
            float w = width > 0 ? width : 0.018f * poles, h = 0.085f, d = 0.075f, pitch = w / poles;
            var g = Group(b.tag);
            Box(g, new Vector3(xc, RailA, Zr - 0.0225f), new Vector3(w - 0.0006f, h, 0.045f), mWhite);
            Box(g, new Vector3(xc, RailA, Zr - 0.06f), new Vector3(w - 0.0006f, 0.045f, 0.03f), mWhite);
            Box(g, new Vector3(xc, RailA + 0.033f, Zr - 0.0455f), new Vector3(w - 0.001f, 0.012f, 0.001f), mDark);
            Label(g, b.tag, new Vector3(xc, RailA + 0.033f, Zr - 0.0465f), 0.0042f, Color.white, true);
            Label(g, rating, new Vector3(xc, RailA - 0.014f, Zr - 0.0755f), 0.0045f, Color.black, true);
            for (int p = 0; p < poles; p++)
            {
                float x = xc - w / 2 + pitch / 2 + p * pitch;
                ScrewHead(g, new Vector3(x, RailA + 0.023f, Zr - 0.0455f));
                ScrewHead(g, new Vector3(x, RailA - 0.03f, Zr - 0.0455f));
                Anchor($"{b.tag}.{2 * p + 1}", new Vector3(x, RailA + h / 2 - 0.002f, Zr - 0.035f), 0);
                Anchor($"{b.tag}.{2 * p + 2}", new Vector3(x, RailA - h / 2 + 0.002f, Zr - 0.035f), 1);
            }
            var lever = Group("lever", g);
            lever.localPosition = new Vector3(xc, RailA + 0.006f, Zr - 0.075f);
            Box(lever, new Vector3(0, 0.009f, 0), new Vector3(w * 0.72f, 0.018f, 0.007f), mDark);
            levers.Add((lever, () => b.closed));
            Solid(b.tag, new Vector3(xc, RailA, Zr - d / 2), new Vector3(w, h, d));
            Click(g, new Vector3(xc, RailA, Zr - 0.045f), new Vector3(w, h, 0.08f),
                () => b.Tip() + Dims(w, h, d, "width measured; height / depth Acti9 iC60 class") + "\nClick to switch.", () => b.closed = !b.closed);
        }

        void Contactor()
        {
            // Hyundai U-series, UMC40–65 frame: 55 wide; 94–106 high and 105–111 deep (catalogue / manual).
            var km1 = C.Get<Contactor>("KM1");
            var g = Group("KM1");
            float xc = -0.06f, y = RailA, w = 0.055f, h = 0.106f, d = 0.111f;
            Box(g, new Vector3(xc, y, Zb - 0.035f), new Vector3(w, h, 0.07f), mGrey);
            Box(g, new Vector3(xc, y, Zb - 0.09f), new Vector3(w * 0.85f, h * 0.55f, 0.041f), mGrey);
            Box(g, new Vector3(xc, y + 0.04f, Zb - 0.0705f), new Vector3(w - 0.002f, 0.018f, 0.001f), mDark);
            Box(g, new Vector3(xc, y - 0.04f, Zb - 0.0705f), new Vector3(w - 0.002f, 0.018f, 0.001f), mDark);
            Box(g, new Vector3(xc - 0.01f, y + 0.005f, Zb - 0.1112f), new Vector3(0.018f, 0.014f, 0.001f), mBlack);
            var flag = Group("indicator", g);
            flag.localPosition = new Vector3(xc - 0.01f, y + 0.002f, Zb - 0.1117f);
            Box(flag, Vector3.zero, new Vector3(0.013f, 0.005f, 0.0012f), mWhite);
            flags.Add((flag, () => km1.on, new Vector3(0, 0.005f, 0)));
            Label(g, "KM1", new Vector3(xc + 0.012f, y + 0.02f, Zb - 0.1112f), 0.0055f, Color.black, true);
            Label(g, "UMC65", new Vector3(xc + 0.012f, y + 0.011f, Zb - 0.1112f), 0.0034f, new Color(0.15f, 0.15f, 0.15f));
            // Front auxiliary block 43-44.
            Box(g, new Vector3(xc + 0.01f, y - 0.018f, Zb - 0.122f), new Vector3(0.024f, 0.028f, 0.022f), mGrey);
            ScrewHead(g, new Vector3(xc + 0.01f, y - 0.008f, Zb - 0.1335f));
            ScrewHead(g, new Vector3(xc + 0.01f, y - 0.028f, Zb - 0.1335f));
            Label(g, "43  44", new Vector3(xc + 0.01f, y - 0.018f, Zb - 0.1335f), 0.0028f, Color.black);
            Anchor("KM1.43", new Vector3(xc + 0.01f, y - 0.003f, Zb - 0.13f), 0);
            Anchor("KM1.43", new Vector3(xc + 0.014f, y - 0.003f, Zb - 0.128f), 0);
            Anchor("KM1.44", new Vector3(xc + 0.01f, y - 0.033f, Zb - 0.13f), 1);
            string[] top = { "KM1.1L1", "KM1.3L2" }, bot = { "KM1.2T1", "KM1.4T2" };
            for (int i = 0; i < 3; i++)
            {
                float x = xc - 0.018f + i * 0.016f;
                ScrewHead(g, new Vector3(x, y + 0.04f, Zb - 0.0712f));
                ScrewHead(g, new Vector3(x, y - 0.04f, Zb - 0.0712f));
                if (i < 2)
                {
                    Anchor(top[i], new Vector3(x, y + h / 2 - 0.002f, Zb - 0.062f), 0);
                    Anchor(top[i], new Vector3(x + 0.004f, y + h / 2 - 0.002f, Zb - 0.058f), 0);
                    Anchor(bot[i], new Vector3(x, y - h / 2 + 0.002f, Zb - 0.062f), 1);
                    Anchor(bot[i], new Vector3(x + 0.004f, y - h / 2 + 0.002f, Zb - 0.058f), 1);
                }
            }
            ScrewHead(g, new Vector3(xc + 0.023f, y + 0.048f, Zb - 0.0712f));
            ScrewHead(g, new Vector3(xc + 0.023f, y - 0.048f, Zb - 0.0712f));
            Anchor("KM1.A2", new Vector3(xc + 0.023f, y + h / 2 - 0.002f, Zb - 0.062f), 0);
            Anchor("KM1.A1", new Vector3(xc + 0.023f, y - h / 2 + 0.002f, Zb - 0.062f), 1);
            Solid("KM1", new Vector3(xc, y, Zb - d / 2), new Vector3(w, h, d));
            Click(g, new Vector3(xc, y, Zb - 0.07f), new Vector3(w, h, 0.14f), () => km1.Tip() + Dims(w, h, d, "Hyundai U-series UMC40–65 frame"));
        }

        void K1(float xc)
        {
            // Finder 55.34 relay (4 CO, 7 A) on the owner's socket, which looks like a Schneider RXZE2S114M (exact part not confirmed):
            // socket measured 28 W × 76 H; depth ≈ 70 with the relay (RXZE2S114M class). IEC + NEMA numbers, two A2 screws.
            var k1 = C.Get<RelayK1>("K1");
            var g = Group("K1");
            float w = 0.028f, h = 0.076f, d = 0.07f;
            Box(g, new Vector3(xc, RailA, Zr - 0.015f), new Vector3(w, h, 0.03f), mGrey);
            Box(g, new Vector3(xc, RailA + 0.004f, Zr - 0.048f), new Vector3(0.021f, 0.0275f, 0.036f), mRelay);
            Label(g, "K1", new Vector3(xc, RailA + 0.01f, Zr - 0.0662f), 0.0048f, Color.white, true);
            Label(g, "55.34", new Vector3(xc, RailA + 0.002f, Zr - 0.0662f), 0.0026f, Color.white);
            var lever = Group("test lever", g);
            lever.localPosition = new Vector3(xc, RailA - 0.006f, Zr - 0.0665f);
            Box(lever, Vector3.zero, new Vector3(0.006f, 0.005f, 0.002f), mOrange);
            flags.Add((lever, () => k1.testLever || k1.on, new Vector3(0, 0.003f, 0)));
            // Socket rows as marked (left to right): NC 42 32 22 12, NO 44 34 24 14, COM 41 31 21 11 — pole 4 on the left,
            // pole 1 on the right; coil row A2 A2 A1. Pole 1 latch (24 V), pole 2 spare, pole 3 empty (gap), pole 4 → KM1 coil (230 V).
            // Which end of the socket carries which row is assumed (contacts top, coil bottom).
            float PoleX(int pole) => xc + 0.0101f - pole * 0.00673f; // pole index 0 = pole 1 (right)
            for (int p = 0; p < 4; p++)
            for (int r = 0; r < 3; r++)
                ScrewHead(g, new Vector3(PoleX(p), RailA + h / 2 - 0.004f - r * 0.0065f, Zr - 0.0305f));
            foreach (var (id, pole, row) in new[] { ("K1.14", 0, 1), ("K1.44", 3, 1), ("K1.11", 0, 2), ("K1.41", 3, 2) })
                Anchor(id, new Vector3(PoleX(pole), RailA + h / 2 - 0.001f, Zr - 0.026f + (row - 1) * 0.004f), 0);
            foreach (float x in new[] { xc - 0.008f, xc - 0.0015f, xc + 0.006f })
                ScrewHead(g, new Vector3(x, RailA - h / 2 + 0.004f, Zr - 0.0305f));
            Anchor("K1.A2", new Vector3(xc - 0.008f, RailA - h / 2 + 0.001f, Zr - 0.026f), 1);
            Anchor("K1.A2b", new Vector3(xc - 0.0015f, RailA - h / 2 + 0.001f, Zr - 0.026f), 1);
            Anchor("K1.A1", new Vector3(xc + 0.006f, RailA - h / 2 + 0.001f, Zr - 0.026f), 1);
            Anchor("K1.A1", new Vector3(xc + 0.006f, RailA - h / 2 + 0.001f, Zr - 0.022f), 1);
            Solid("K1", new Vector3(xc, RailA, Zr - d / 2), new Vector3(w, h, d));
            Click(g, new Vector3(xc, RailA, Zr - 0.035f), new Vector3(w, h, 0.07f),
                () => k1.Tip() + "\nFinder 55.34.9.024.0000 on a socket that looks like a Schneider RXZE2S114M (exact part not confirmed; no diode fitted). The orange flag shows the state." +
                      "\n<color=#ffb050>Read the TOP label of each screw (IEC, arrow up), not the lower NEMA one (C11): NEMA 14 is the coil A2, not the pole 1 NO.</color>" +
                      Dims(w, h, d, "socket 28 × 76 measured; depth RXZE2S114M class + relay; row order assumed"),
                () => k1.testLever = true, () => k1.testLever = false);
        }

        void FuseTerm(Fuse f, float xc)
        {
            // RT18-32 1P DIN-rail holder (17 mm wide, measured; ~79 × 60) for a 10 × 38 mm gG fuse link; the front swings down to change it.
            float w = 0.017f, h = 0.079f, d = 0.06f;
            var g = Group(f.tag);
            Box(g, new Vector3(xc, RailA, Zr - 0.02f), new Vector3(w - 0.0004f, h, 0.04f), mGrey);
            var carrier = Group("carrier", g);
            carrier.localPosition = new Vector3(xc, RailA - 0.025f, Zr - 0.04f);
            Box(carrier, new Vector3(0, 0.025f, -0.01f), new Vector3(0.0155f, 0.05f, 0.02f), mBlack);
            Label(carrier, f.tag, new Vector3(0, 0.038f, -0.0202f), 0.0034f, Color.white, true);
            Label(carrier, "gG 2A", new Vector3(0, 0.028f, -0.0202f), 0.0024f, Color.white);
            fuseCarriers.Add((carrier, () => f.inserted));
            Anchor(f.tag + ".in", new Vector3(xc, RailA + h / 2 - 0.002f, Zr - 0.03f), 0);
            Anchor(f.tag + ".out", new Vector3(xc, RailA - h / 2 + 0.002f, Zr - 0.03f), 1);
            Anchor(f.tag + ".out", new Vector3(xc, RailA - h / 2 + 0.002f, Zr - 0.026f), 1);
            Solid(f.tag, new Vector3(xc, RailA, Zr - d / 2), new Vector3(w, h, d));
            Click(g, new Vector3(xc, RailA, Zr - 0.03f), new Vector3(w, h, 0.06f),
                () => f.Tip() + "\nRT18-32 DIN-rail holder, 10 × 38 mm gG 2 A fuse link (not aM: an aM fuse does not protect against overload). " +
                      "Click to open / close the holder." + Dims(w, h, d, "width measured; height / depth RT18-32 1P class"),
                () => f.inserted = !f.inserted);
        }

        // ------------------------------------------------------------------ row B: VFD and PSUs

        void RowB()
        {
            vfd = C.Get<Vfd>("U1");
            var g = Group("U1 VFD");
            // BEST FC300-2.2G: 140 W × 205 H × 160 D (measured), left edge 10 mm from the left vertical duct.
            float xc = -0.1495f, w = 0.14f, h = 0.205f, d = 0.16f, yc = RowBBottom + h / 2;
            float bottom = RowBBottom;
            Box(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d), mVfd);
            for (int i = 0; i < 10; i++)
                Box(g, new Vector3(xc - w / 2 - 0.004f, yc, Zb - 0.02f - i * 0.014f), new Vector3(0.008f, h * 0.9f, 0.0015f), mAlu);
            Box(g, new Vector3(xc, yc + 0.04f, Zb - d - 0.001f), new Vector3(0.075f, 0.075f, 0.002f), P.M(new Color(0.3f, 0.31f, 0.34f), 0, 0.5f));
            Box(g, new Vector3(xc, yc + 0.061f, Zb - d - 0.0022f), new Vector3(0.052f, 0.017f, 0.001f), P.M(new Color(0.12f, 0.02f, 0.02f), 0, 0.9f));
            vfdDisplay = Label(g, "", new Vector3(xc, yc + 0.061f, Zb - d - 0.003f), 0.011f, new Color(1f, 0.15f, 0.1f), true);
            for (int i = 0; i < 6; i++)
                P.Disc(g, new Vector3(xc - 0.025f + (i % 3) * 0.025f, yc + 0.025f + (i / 3) * 0.018f, Zb - d - 0.0025f), 0.009f, 0.002f, mDark);
            Led(g, new Vector3(xc + 0.032f, yc + 0.07f, Zb - d - 0.002f), new Color(0.2f, 1f, 0.3f), () => vfd.run);
            Led(g, new Vector3(xc + 0.032f, yc + 0.056f, Zb - d - 0.002f), new Color(1f, 0.8f, 0.2f), () => vfd.powered);
            Label(g, "BEST FC300-2.2G", new Vector3(xc, yc - 0.002f, Zb - d - 0.001f), 0.0055f, new Color(0.85f, 0.88f, 0.92f), true);
            Label(g, "U1  2.2 kW · 230 V 1~", new Vector3(xc, yc - 0.012f, Zb - d - 0.001f), 0.0038f, new Color(0.7f, 0.74f, 0.8f));
            Box(g, new Vector3(xc, bottom + 0.016f, Zb - d * 0.45f), new Vector3(w * 0.96f, 0.022f, d * 0.9f), mBlack);
            string[] pw = { "U1.L", "U1.N", "U1.P+", "U1.PB", "U1.U", "U1.V", "U1.W", "U1.PE" };
            string[] pwl = { "L", "N", "P+", "PB", "U", "V", "W", "PE" };
            for (int i = 0; i < pw.Length; i++)
            {
                float x = xc - 0.058f + i * 0.0075f;
                ScrewHead(g, new Vector3(x, bottom + 0.018f, Zb - d * 0.9f - 0.0005f));
                Label(g, pwl[i], new Vector3(x, bottom + 0.009f, Zb - d * 0.9f - 0.0005f), 0.0026f, Color.white);
                Anchor(pw[i], new Vector3(x, bottom + 0.002f, Zb - d * 0.8f), 2); // U V W PE: cable W1 drops into D2, then the power lane
            }
            Anchor("U1.PEb", new Vector3(xc - 0.058f + 7 * 0.0075f + 0.002f, bottom + 0.002f, Zb - d * 0.78f), 2);
            Anchor("U1.PEc", new Vector3(xc - 0.058f + 7 * 0.0075f - 0.002f, bottom + 0.002f, Zb - d * 0.76f), 2);
            string[] ct = { "U1.X1", "U1.X2", "U1.X3", "U1.X4", "U1.COM" };
            for (int i = 0; i < ct.Length; i++)
            {
                float x = xc + 0.012f + i * 0.0085f;
                P.Box(g, new Vector3(x, bottom + 0.018f, Zb - d * 0.9f - 0.001f), new Vector3(0.006f, 0.008f, 0.003f), mPlug);
                Label(g, ct[i].Substring(3), new Vector3(x, bottom + 0.009f, Zb - d * 0.9f - 0.0005f), 0.0024f, Color.white);
                Anchor(ct[i], new Vector3(x, bottom + 0.002f, Zb - d * 0.8f), 2);
            }
            Solid("U1", new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d));
            float clear = (HD[1] - DuctHalf - (RowBBottom + h)) * 1000;
            Click(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w + 0.02f, h, d),
                () => vfd.Tip() + Dims(w, h, d, "measured") +
                      $"\n<color=#7dffa0>{clear:0} mm clear above the VFD (§ 6 asks for 100 mm) · 50 mm to the PSU on its right.</color>" +
                      "\n<color=#ffb050>REMINDER C14 (open): the nameplate's 11 A is the OUTPUT current — input ≈ 22–24 A at full load. QF1 C16 trips on long full-power cuts, Z1 20 A is overloaded, QF0 C25 marginal; the supply path behind QF0 is 4 mm².</color>" +
                      "\nOutput: one 4G1.5 shielded cable W1 down the power lane, shield 360° at both ends (EMC clamp + HDC-HA hood).");

            // PSUs upright, measured: G1 50 mm right of the VFD, G2 10 mm right of G1 (9 mm left to the right vertical duct).
            Psu(C.Get<Psu>("G1"), 0.033f, 0.125f, 0.24f, 0.065f, "S-720-48", "48 V  15 A",
                new[] { "G1.L", "G1.N", "G1.PE", "G1.-V1", "G1.-V2", "G1.-V3", "G1.+V1", "G1.+V2", "G1.+V3" },
                new[] { "L", "N", "PE", "-V", "-V", "-V", "+V", "+V", "+V" }, "measured");
            Psu(C.Get<Psu>("G2"), 0.163f, 0.115f, 0.215f, 0.05f, "S-500-24", "24 V  20 A",
                new[] { "G2.L", "G2.N", "G2.PE", "G2.-V1", "G2.-V2", "G2.+V1", "G2.+V2" },
                new[] { "L", "N", "PE", "-V", "-V", "+V", "+V" }, "measured");
        }

        void Psu(Psu psu, float xc, float w, float h, float d, string model, string rating, string[] ids, string[] labels, string src)
        {
            var g = Group(psu.tag);
            float yc = RowBBottom + h / 2;
            Box(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d), mAlu);
            for (int i = 0; i < 8; i++)
                Box(g, new Vector3(xc, yc + h * 0.38f - i * 0.009f, Zb - d - 0.0003f), new Vector3(w * 0.8f, 0.003f, 0.001f), mBlack);
            Box(g, new Vector3(xc, yc - 0.01f, Zb - d - 0.0005f), new Vector3(0.07f, 0.04f, 0.001f), mWhite);
            Label(g, model, new Vector3(xc, yc + 0.001f, Zb - d - 0.0012f), 0.0062f, Color.black, true);
            Label(g, rating, new Vector3(xc, yc - 0.01f, Zb - d - 0.0012f), 0.0046f, new Color(0.15f, 0.15f, 0.15f), true);
            Label(g, psu.tag, new Vector3(xc, yc - 0.021f, Zb - d - 0.0012f), 0.0055f, new Color(0.1f, 0.1f, 0.4f), true);
            Box(g, new Vector3(xc, RowBBottom + 0.016f, Zb - d - 0.006f), new Vector3(w * 0.94f, 0.02f, 0.012f), mBlack);
            float step = w * 0.86f / ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                float x = xc - w * 0.43f + step * (i + 0.5f);
                ScrewHead(g, new Vector3(x, RowBBottom + 0.019f, Zb - d - 0.0122f));
                Label(g, labels[i], new Vector3(x, RowBBottom + 0.0095f, Zb - d - 0.0122f), 0.0026f, Color.white);
                Anchor(ids[i], new Vector3(x, RowBBottom + 0.002f, Zb - d - 0.008f), 2);
                Anchor(ids[i], new Vector3(x + 0.002f, RowBBottom + 0.002f, Zb - d - 0.004f), 2);
            }
            Led(g, new Vector3(xc + w * 0.35f, RowBBottom + 0.035f, Zb - d - 0.001f), new Color(0.2f, 1f, 0.3f), () => psu.on);
            P.Disc(g, new Vector3(xc + w * 0.25f, RowBBottom + 0.035f, Zb - d - 0.001f), 0.006f, 0.002f, mOrange);
            Solid(psu.tag, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d));
            Click(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d), () => psu.Tip() + $"\nEnclosed switching PSU, {model} class." + Dims(w, h, d, src));
        }

        // ------------------------------------------------------------------ rows C and D: drives, A11 board

        void RowC()
        {
            float[] xs = { -0.176f, -0.067f, 0.042f };
            string[] tags = { "A2", "A3", "A4" };
            for (int i = 0; i < 3; i++) Drive(C.Get<StepperDrive>(tags[i]), xs[i]);
        }

        void Drive(StepperDrive dr, float xc)
        {
            // DMA860H side-mounted (measured on the plate 52 W × 150 H, 97 out from the plate). The terminals face the door in
            // one column: signal connector at the top (PUL / DIR / ALM → D2), power connector at the bottom (AC, motor → D3),
            // as on the owner's drives.
            var g = Group(dr.tag);
            float w = 0.052f, h = 0.15f, d = 0.097f, top = DriveTop, yc = top - h / 2, zf = Zb - d;
            Box(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d), mDrive);
            // Fins along the plate side, label on the left face.
            for (int i = 0; i < 6; i++)
                Box(g, new Vector3(xc - w * 0.35f + i * w * 0.14f, yc, Zb - 0.006f), new Vector3(0.0015f, h * 0.92f, 0.012f), mDrive);
            float xs = xc - w / 2 - 0.0004f;
            Box(g, new Vector3(xs, yc, Zb - d * 0.55f), new Vector3(0.001f, 0.1f, 0.07f), P.M(new Color(0.12f, 0.3f, 0.6f), 0, 0.5f));
            foreach (var (text, dy, size) in new[] { ("Leadshine", 0.03f, 0.006f), ("DMA860H", 0.015f, 0.0075f), ("1600 p/rev · 5.14 A", -0.002f, 0.0036f) })
                Label(g, text, new Vector3(xs - 0.0007f, yc + dy, Zb - d * 0.55f), size, Color.white, true).transform.localEulerAngles = new Vector3(0, 90, 0);
            // Front: signal plug (top), tag and LEDs, power plug (bottom).
            Box(g, new Vector3(xc, top - 0.021f, zf - 0.006f), new Vector3(0.016f, 0.038f, 0.012f), mPlug);
            Box(g, new Vector3(xc, top - h + 0.027f, zf - 0.006f), new Vector3(0.016f, 0.05f, 0.012f), mPlug);
            Label(g, dr.tag, new Vector3(xc, yc + 0.012f, zf - 0.0011f), 0.0055f, Color.white, true);
            Label(g, "XYZ"[dr.axis] + " axis", new Vector3(xc, yc + 0.004f, zf - 0.0011f), 0.0036f, new Color(0.85f, 0.9f, 1f));
            Box(g, new Vector3(xc, yc - 0.008f, zf - 0.001f), new Vector3(0.016f, 0.006f, 0.002f), mRed);
            Led(g, new Vector3(xc - 0.006f, yc - 0.018f, zf - 0.001f), new Color(0.2f, 1f, 0.3f), () => dr.powered && !dr.fault);
            Led(g, new Vector3(xc + 0.006f, yc - 0.018f, zf - 0.001f), new Color(1f, 0.15f, 0.1f), () => dr.powered && dr.fault && Mathf.Repeat(Time.time, 0.5f) < 0.3f);
            string[] sig = { "PUL+", "PUL-", "DIR+", "DIR-", "ALM+", "ALM-" };
            string[] pwr = { "AC1", "AC2", "A+", "A-", "B+", "B-" };
            for (int i = 0; i < 6; i++)
            {
                float ys = top - 0.0063f - i * 0.0051f, yp = top - h + 0.008f + (5 - i) * 0.0076f;
                P.Disc(g, new Vector3(xc + 0.004f, yp, zf - 0.0125f), 0.003f, 0.001f, mScrew);
                P.Disc(g, new Vector3(xc + 0.004f, ys, zf - 0.0125f), 0.0022f, 0.001f, mScrew);
                Anchor($"{dr.tag}.{pwr[i]}", new Vector3(xc - 0.004f, yp, zf - 0.014f), 3);
                Anchor($"{dr.tag}.{sig[i]}", new Vector3(xc - 0.004f, ys, zf - 0.014f), 2);
                Anchor($"{dr.tag}.{sig[i]}", new Vector3(xc - 0.0055f, ys, zf - 0.017f), 2);
            }
            Solid(dr.tag, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d));
            Click(g, new Vector3(xc, yc, Zb - d / 2), new Vector3(w, h, d), () => dr.Tip() + Dims(w, h, d, "measured, side-mounted; signals top, power bottom"), () => dr.fault = !dr.fault);
        }

        void RowD()
        {
            // RichAuto A11E interface board, measured 153 × 90, 20 high from the plate (35 with the green screw terminals).
            // Landscape, as on the owner's board (2026-10-27): CN1 50-pin handheld header on the left edge; top strip (left →
            // right) Y1–Y8, GND (outputs), X1–X8, 24V, GND, SHIELD (inputs), 24V, GND (board power); bottom strip 5V, PULSE,
            // DIR, SHIELD for the X, Y, Z and C axes. Which 24V / GND pair is the board supply is assumed (same nets either way).
            var a11 = C.Get<A11Board>("A1");
            var g = Group("A1 A11E board");
            float xc = -0.145f, yc = A11y, w = 0.153f, h = 0.09f, zp = Zb - 0.012f;
            Box(g, new Vector3(xc, yc, zp), new Vector3(w, h, 0.0016f), mPcb);
            foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                Cyl(g, new Vector3(xc + dx * (w / 2 - 0.004f), yc + dy * (h / 2 - 0.004f), zp + 0.006f), 0.005f, 0.011f, mBrass, new Vector3(90, 0, 0));
            var topIds = new List<string> { "A1.Y1", "A1.Y2", "A1.Y3", "A1.Y4", "A1.Y5", "A1.Y6", "A1.Y7", "A1.Y8", "A1.OGND",
                "A1.X1", "A1.X2", "A1.X3", "A1.X4", "A1.X5", "A1.X6", "A1.X7", "A1.X8", "A1.24V", "A1.GND", "A1.Shield", "A1.DC24V", "A1.DCGND" };
            var bot = new List<string>();
            foreach (var a in new[] { "X", "Y", "Z", "C" }) bot.AddRange(new[] { $"A1.{a}_5V", $"A1.{a}_PUL", $"A1.{a}_DIR", $"A1.{a}_SH" });
            // Both strips use the same screw pitch; the bottom strip is drawn centred (its position on the board is assumed).
            float step = w * 0.94f / topIds.Count;
            Box(g, new Vector3(xc, yc + h / 2 - 0.007f, zp - 0.0118f), new Vector3(w * 0.96f, 0.01f, 0.022f), mPlug);
            Box(g, new Vector3(xc, yc - h / 2 + 0.007f, zp - 0.0118f), new Vector3(step * bot.Count + w * 0.02f, 0.01f, 0.022f), mPlug);
            float xCn = xc - w / 2 + 0.009f;
            Box(g, new Vector3(xCn, yc, zp - 0.005f), new Vector3(0.009f, 0.058f, 0.009f), mBlack);
            Label(g, "CN1 50-pin", new Vector3(xCn + 0.016f, yc + 0.02f, zp - 0.0012f), 0.0032f, Color.white);
            Box(g, new Vector3(xc + 0.04f, yc - 0.01f, zp - 0.002f), new Vector3(0.02f, 0.02f, 0.003f), mBlack);
            Label(g, "RichAuto A11E", new Vector3(xc + 0.01f, yc + 0.022f, zp - 0.0012f), 0.0055f, Color.white, true);
            Label(g, "A1", new Vector3(xc + 0.058f, yc + 0.022f, zp - 0.0012f), 0.006f, new Color(1f, 0.9f, 0.4f), true);
            Led(g, new Vector3(xc + 0.062f, yc + 0.006f, zp - 0.002f), new Color(0.2f, 1f, 0.3f), () => a11.powered);
            Led(g, new Vector3(xc + 0.062f, yc - 0.003f, zp - 0.002f), new Color(1f, 0.15f, 0.1f), () => a11.powered && a11.Alarm != null);
            for (int i = 0; i < topIds.Count; i++)
            {
                Anchor(topIds[i], new Vector3(xc - w * 0.47f + step * (i + 0.5f), yc + h / 2 + 0.002f, zp - 0.016f), 3);
                Anchor(topIds[i], new Vector3(xc - w * 0.47f + step * (i + 0.5f) + 0.0012f, yc + h / 2 + 0.002f, zp - 0.013f), 3);
            }
            float x0B = xc - step * bot.Count / 2;
            for (int i = 0; i < bot.Count; i++)
            {
                Anchor(bot[i], new Vector3(x0B + step * (i + 0.5f), yc - h / 2 - 0.002f, zp - 0.016f), 4);
                Anchor(bot[i], new Vector3(x0B + step * (i + 0.5f) + 0.0012f, yc - h / 2 - 0.002f, zp - 0.013f), 4);
            }
            // The handheld cable comes over the hinge on the left, so CN1 is fed from the left vertical duct.
            Anchor("A1.HH", new Vector3(xCn - 0.006f, yc, zp - 0.01f), VL);
            Solid("A1", new Vector3(xc, yc, Zb - 0.0175f), new Vector3(w, h, 0.035f));
            Click(g, new Vector3(xc, yc, Zb - 0.0175f), new Vector3(w, h, 0.035f), () => a11.Tip() + Dims(w, h, 0.035f, "measured; 20 high without the screw terminals"));
        }

        // ------------------------------------------------------------------ floor (gland plate, earth bar) and side walls

        void Floor()
        {
            var g = Group("cable entry");
            float yf = -EncH / 2 + Ts; // inside face of the bottom wall (glands drilled through it on assembly)
            const float zg = ZBack - 0.12f; // connector row 120 mm from the back
            void Gland(float x, float d, Material m, string tip, string[] ids, int duct = 4)
            {
                Cyl(g, new Vector3(x, yf - 0.012f, zg), d + 0.006f, 0.008f, mBlack, Vector3.zero);
                Cyl(g, new Vector3(x, yf + 0.012f, zg), d, 0.024f, m, Vector3.zero);
                Cyl(g, new Vector3(x, yf + 0.003f, zg), d + 0.008f, 0.004f, mScrew, Vector3.zero);
                for (int i = 0; i < ids.Length; i++)
                {
                    float a = i * Mathf.PI * 2 / ids.Length;
                    Anchor(ids[i], new Vector3(x + Mathf.Cos(a) * d * 0.25f, yf + 0.025f, zg + Mathf.Sin(a) * d * 0.25f), duct);
                    Anchor(ids[i], new Vector3(x + Mathf.Cos(a) * d * 0.25f + 0.001f, yf + 0.025f, zg + Mathf.Sin(a) * d * 0.25f), duct);
                }
                Solid("gland " + tip.Substring(3, Math.Min(12, tip.Length - 3)), new Vector3(x, yf + 0.012f, zg), new Vector3(d, 0.024f, d));
                Click(g, new Vector3(x, yf + 0.01f, zg), new Vector3(d + 0.01f, 0.03f, d + 0.01f), () => tip);
            }
            // Power entries under the power lane VP (far left): mains gland and the HDC-HA spindle connector.
            Gland(-0.35f, 0.02f, mBlack, "<b>Mains cable gland M20</b>\nL, N, PE in (wires 101, 102, 140), straight up the power lane VP to QF0 (unfiltered side kept away from the 24 V and signal ducts).",
                new[] { "Mains.L", "Mains.N", "Mains.PE" }, VP);
            const float xh = -0.29f;
            Box(g, new Vector3(xh, yf + 0.014f, zg), new Vector3(0.044f, 0.028f, 0.044f), mAlu);
            Box(g, new Vector3(xh, yf - 0.016f, zg), new Vector3(0.044f, 0.03f, 0.044f), mAlu);
            string[] hdc = { "X1.1", "X1.2", "X1.3", "X1.SH", "X1.PE" };
            for (int i = 0; i < hdc.Length; i++)
            {
                Anchor(hdc[i], new Vector3(xh - 0.015f + i * 0.0075f, yf + 0.029f, zg), VP);
                Anchor(hdc[i], new Vector3(xh - 0.015f + i * 0.0075f + 0.001f, yf + 0.029f, zg + 0.003f), VP);
            }
            Solid("X1 HDC-HA", new Vector3(xh, yf + 0.014f, zg), new Vector3(0.044f, 0.028f, 0.044f));
            Click(g, new Vector3(xh, yf, zg), new Vector3(0.046f, 0.06f, 0.046f), () => C.Get<SpindleConnector>("X1").Tip() +
                "\nMetal housing in the bottom wall under the power lane; cable W1 shield clamped 360° in the hood (no pigtail on pin 4).");
            // Stepper motor connectors under the motor lane VM (far right).
            string[] ax = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
                Gland(0.285f + 0.04f * i, 0.018f, mScrew, $"<b>XM-{ax[i]} GX16-6</b> {ax[i]}-axis motor connector (pins 1–4 = A+ A− B+ B−), under the motor lane VM. " +
                                                          "LiYCY 4 × 1.0 shielded cable; bond the shield to the connector shell and the shell to the bare-metal wall.",
                    new[] { $"XM-{ax[i]}.1", $"XM-{ax[i]}.2", $"XM-{ax[i]}.3", $"XM-{ax[i]}.4" }, VM);
            // Sensor connectors near the A11 board (inner bottom duct D4).
            for (int i = 0; i < 3; i++)
                Gland(-0.19f + 0.04f * i, 0.016f, mScrew, $"<b>{ax[i]}-LIM GX16-4</b> NPN NO limit / home sensors. Pins 2, 3 and 4 are looped to the next connector (2 conductors per solder cup); " +
                                                          "the sensor 0 V comes from the A11 J8 GND screw (337), not through the drives.",
                    new[] { $"{ax[i]}-LIM.1", $"{ax[i]}-LIM.2", $"{ax[i]}-LIM.3", $"{ax[i]}-LIM.4" });
            Gland(-0.07f, 0.016f, mScrew, "<b>XT GX16-5</b> tool setter: pin 1 touch → X4; pin 3 over-travel (NC) → E-stop NC2 → X7 (347); pins 2 and 4 jumpered to 0 V. " +
                                          "Unplugged = E-stop on the A11: fit a dummy plug (3-4 bridged) if the setter is not used.", new[] { "XT.1", "XT.2", "XT.3", "XT.4" });
            Gland(0.215f, 0.016f, mBlack, "<b>Pump cable gland</b> (coolant pump cord to XS1).", Array.Empty<string>());

            // Two earth bars (owner's parts): copper 150 × 25 mm on two 17 mm red insulators, 4 points each, linked by 138.
            // Up to 2 conductors per point (owner decision 2026-10-26; IEC 60204-1 asks for one protective conductor per point).
            // Anchors are listed in the order the wires take them (A11Layout order), so each wire lands on its planned point:
            //   bar 1: P1 140 + 139 · P2 141 + 143 · P3 144 + 138 · P4 142 (door strap, alone)
            //   bar 2: P1 138 + 148 · P2 145 + 146 · P3 147 + 149 · P4 spare
            var e = Group("PE bars");
            float zpe = ZBack - 0.185f;
            yf = -EncH / 2 + 0.0015f;
            const float barL = 0.15f, barW = 0.025f, barT = 0.005f, insH = 0.017f;
            float[] pts = { -0.0525f, -0.0175f, 0.0175f, 0.0525f };
            void EarthBar(string id, float xc, string mark, (int p, int k)[] order)
            {
                Box(e, new Vector3(xc, yf + insH + barT / 2, zpe), new Vector3(barL, barT, barW), mCopper);
                foreach (float s in new[] { -1f, 1f })
                    Cyl(e, new Vector3(xc + s * (barL / 2 - 0.012f), yf + insH / 2, zpe), 0.016f, insH, mRed, Vector3.zero);
                foreach (float x in pts)
                    Cyl(e, new Vector3(xc + x, yf + insH + barT + 0.0015f, zpe), 0.008f, 0.003f, mScrew, Vector3.zero);
                foreach (var (pi, k) in order)
                    Anchor(id, new Vector3(xc + pts[pi] + (k == 0 ? -0.003f : 0.003f), yf + insH + barT + 0.004f, zpe - 0.004f), 4);
                Label(e, mark, new Vector3(xc - barL / 2 + 0.006f, yf + insH + barT + 0.0002f, zpe - barW / 2 - 0.0005f), 0.005f, Color.black, true);
                Solid(mark, new Vector3(xc, yf + (insH + barT) / 2, zpe), new Vector3(barL, insH + barT, barW));
                Click(e, new Vector3(xc, yf + (insH + barT) / 2, zpe), new Vector3(barL, insH + barT + 0.006f, barW),
                    () => C.Get<PeBar>("PEBAR").Tip() + $"\n{mark}: copper bar on two red insulators on the bottom wall, 4 points." + Dims(barL, insH + barT, barW, "measured"));
            }
            EarthBar("PEBAR.BAR", -0.11f, "PE 1", new[] { (0, 0), (0, 1), (1, 0), (3, 0), (1, 1), (2, 0), (2, 1) });
            EarthBar("PEBAR.BAR2", 0.06f, "PE 2", new[] { (2, 0), (1, 0), (1, 1), (2, 1), (0, 0), (0, 1) });
            Anchor("BKP.T", new Vector3(-0.225f, -0.4275f, Zb - 0.004f), 4);
            P.Disc(e, new Vector3(-0.225f, -0.4275f, Zb - 0.002f), 0.008f, 0.003f, mBrass);
            // Body earth stud (wire 139) on the inside of the left wall, in front of the mounting plate.
            float xb = -EncW / 2 + Ts;
            Anchor("BODY.T", new Vector3(xb + 0.008f, -0.30f, 0.10f), VL);
            Cyl(e, new Vector3(xb + 0.003f, -0.30f, 0.10f), 0.008f, 0.006f, mBrass, new Vector3(0, 0, 90));
            Click(e, new Vector3(xb + 0.006f, -0.30f, 0.10f), new Vector3(0.012f, 0.014f, 0.014f),
                () => C.Get<NodeT>("BODY").Tip() + "\nM6 earth stud on the enclosure body (left wall); wire 139 to the earth bar.");
        }

        void SideWalls()
        {
            fan = C.Get<Fan>("M2");
            var g = Group("M2 fan");
            // Owner's 80 × 80 fan screwed to the inside of the left wall over its cut-out, body reaching 'depth' into the cabinet.
            float fs = FanCut, depth = FanDepth, inw = 1f, x0 = FanAt.x, y = FanAt.y, z = FanAt.z;
            float x = x0 + Ts + inw * depth / 2, k = fs / 0.08f;
            foreach (float sgn in new[] { -1f, 1f })
            {
                Box(g, new Vector3(x, y + sgn * (fs / 2 - 0.002f), z), new Vector3(depth, 0.004f, fs), mBlack);
                Box(g, new Vector3(x, y, z + sgn * (fs / 2 - 0.002f)), new Vector3(depth, fs, 0.004f), mBlack);
            }
            FilterHousing(g, x0, -inw, y, z);
            Cyl(g, new Vector3(x + inw * 0.002f, y, z), 0.032f * k, 0.018f, mBlack, new Vector3(0, 0, 90));
            fanRotor = Group("rotor", g);
            fanRotor.localPosition = new Vector3(x - inw * 0.004f, y, z);
            for (int i = 0; i < 7; i++)
            {
                var blade = Group("blade", fanRotor);
                blade.localEulerAngles = new Vector3(i * 360f / 7f, 0, 0);
                Box(blade, new Vector3(0, 0.02f * k, 0), new Vector3(0.003f, 0.03f * k, 0.016f * k), mDark);
            }
            Anchor("M2.+", new Vector3(x + inw * (depth / 2 + 0.0005f), y + 0.44f * fs, z - 0.375f * fs), inw > 0 ? VL : VR);
            Anchor("M2.-", new Vector3(x + inw * (depth / 2 + 0.0005f), y + 0.44f * fs, z - 0.31f * fs), inw > 0 ? VL : VR);
            Solid("M2", new Vector3(x, y, z), new Vector3(depth, fs, fs));
            Click(g, new Vector3(x, y, z), new Vector3(depth + 0.005f, fs, fs), () => fan.Tip() +
                $"\nOwner's {fs * 1000:0} × {fs * 1000:0} mm 24 V DC fan, blowing in, behind its plastic filter adapter (flanged, outside the left wall) in a {fs * 1000:0} × {fs * 1000:0} cut-out." +
                Dims(fs, fs, depth, "80 × 80 measured; 25 deep assumed"));
            OutletGrille();

            // R1: 800W100RJ tube braking resistor (100 Ω 800 W), measured Ø 60 × 300 with its axis 60 mm off the wall.
            // Vertical on the right wall in front of the free plate strip above the motor lane; leads at both ends.
            var r = Group("R1 braking resistor");
            const float rd = 0.06f, rl = 0.3f, off = 0.06f;
            // 25 mm below the busbars, ≥ 50 mm in front of the right vertical duct VR; the motor lane starts below it.
            float wall = EncW / 2 - Ts, rx = wall - off, rz = 0.08f, ry0 = -0.06f, ry1 = ry0 + rl, ryc = (ry0 + ry1) / 2;
            var hot = P.ME(new Color(0.72f, 0.7f, 0.66f), Color.black, 0.5f);
            var hotBody = Cyl(r, new Vector3(rx, ryc, rz), rd, rl - 0.03f, hot, Vector3.zero);
            leds.Add((hotBody.GetComponent<Renderer>(), new Color(1f, 0.25f, 0.05f), () => vfd != null && vfd.braking));
            foreach (float yb in new[] { ry0 + 0.008f, ry1 - 0.008f })
            {
                Box(r, new Vector3((wall + rx - 0.01f) / 2, yb, rz), new Vector3(wall - rx + 0.01f, 0.016f, 0.03f), mAlu); // bracket
                Box(r, new Vector3(wall - 0.0015f, yb, rz), new Vector3(0.003f, 0.016f, 0.05f), mAlu);                       // flange
            }
            Label(r, "100 Ω 800 W", new Vector3(rx - rd / 2 - 0.001f, ryc, rz), 0.008f, Color.black, true).transform.localEulerAngles = new Vector3(0, 90, 90);
            // Solder lugs at the tube ends, just inside the brackets.
            foreach (var (id, yl) in new[] { ("R1.T1", ry1 - 0.02f), ("R1.T2", ry0 + 0.02f) })
            {
                Box(r, new Vector3(rx - rd / 2 + 0.004f, yl, rz), new Vector3(0.012f, 0.006f, 0.0015f), mBrass);
                Anchor(id, new Vector3(rx - rd / 2 - 0.004f, yl, rz), VR);
            }
            Solid("R1", new Vector3((wall + rx - rd / 2) / 2, ryc, rz), new Vector3(wall - rx + rd / 2, rl, rd));
            Click(r, new Vector3(rx, ryc, rz), new Vector3(rd, rl, rd),
                () => C.Get<BrakeResistor>("R1").Tip() + "\nTube resistor on two brackets, vertical on the right side wall, below the air outlet so the heat leaves the box. " +
                      "<color=#ffb050>Bare and very hot while braking: keep ≥ 50 mm to ducts, busbars and wires; the 1 mm² PVC leads 128 / 129 must not touch the tube.</color>" +
                      Dims(rd, rl, rd, "Ø 60 × 300 measured, axis 60 mm off the wall"));
        }

        /// SK 3238.200 outlet filter on the outside of its cut-out in the right wall; the cut-out stays open inside.
        void OutletGrille()
        {
            var g = Group("air outlet");
            float s = FilterFace, cut = FanCut, xo = OutletAt.x + FilterProud / 2;
            FilterHousing(g, OutletAt.x, 1, OutletAt.y, OutletAt.z);
            Click(g, new Vector3(xo, OutletAt.y, OutletAt.z), new Vector3(FilterProud + 0.002f, s, s),
                () => $"<b>Air outlet</b>\nOwner's plastic 80 mm filter adapter (flanged, same as on the fan, no fan) on a {cut * 1000:0} × {cut * 1000:0} mm cut-out in the right wall (cut on assembly)." +
                      Dims(s, s, FilterProud, "flange size assumed — slightly larger than 80 × 80"));
        }

        /// Flanged plastic filter housing on the outside of a wall cut-out; 'outward' is −1 on the left wall, +1 on the right.
        void FilterHousing(Transform g, float wallX, float outward, float y, float z)
        {
            float s = FilterFace, cut = FanCut, xo = wallX + outward * FilterProud / 2;
            Box(g, new Vector3(xo, y, z), new Vector3(FilterProud, s, s), mGrey);
            for (int i = 1; i < 7; i++)
                Box(g, new Vector3(xo + outward * (FilterProud / 2 + 0.0003f), y - cut / 2 + i * cut / 7, z), new Vector3(0.001f, 0.003f, cut - 0.006f), mDark);
        }

        // ------------------------------------------------------------------ door rear (contact blocks, lamp holders)

        void DoorRear()
        {
            var g = Group("door rear", door);
            const float zr = -0.006f; // inside face of the door leaf
            Material white = mWhite, black = mBlack;
            P.Disc(g, new Vector3(0, 0.335f, zr + 0.006f), 0.032f, 0.012f, black);
            foreach (var (dx, a, b) in new[] { (-0.0065f, "S0.1", "S0.2"), (0.0065f, "S0.3", "S0.4") }) // built-in NC1 + added ZB2-BE102 (NC2)
                Block(g, new Vector3(dx, 0.335f, zr + 0.03f), a, b, true);
            P.Disc(g, new Vector3(0, 0.205f, zr + 0.006f), 0.03f, 0.012f, black);
            Block(g, new Vector3(-0.0065f, 0.205f, zr + 0.03f), "S2.1", "S2.2", false);
            Block(g, new Vector3(0.0065f, 0.205f, zr + 0.03f), "S1.1", "S1.2", true);
            foreach (var (x, tag) in new[] { (-0.085f, "H1"), (0f, "H2"), (0.085f, "H3") })
            {
                Cyl(g, new Vector3(x, 0.085f, zr + 0.02f), 0.026f, 0.032f, black, new Vector3(90, 0, 0));
                Box(g, new Vector3(x, 0.085f, zr + 0.04f), new Vector3(0.024f, 0.012f, 0.008f), white);
                ScrewHead(g, new Vector3(x - 0.006f, 0.085f, zr + 0.0445f)).transform.localEulerAngles = new Vector3(90, 0, 0);
                ScrewHead(g, new Vector3(x + 0.006f, 0.085f, zr + 0.0445f)).transform.localEulerAngles = new Vector3(90, 0, 0);
                Anchor($"{tag}.+", new Vector3(x - 0.006f, 0.085f, zr + 0.047f), -1, true);
                Anchor($"{tag}.+", new Vector3(x - 0.0065f, 0.0858f, zr + 0.047f), -1, true);
                Anchor($"{tag}.-", new Vector3(x + 0.006f, 0.085f, zr + 0.047f), -1, true);
                Anchor($"{tag}.-", new Vector3(x + 0.0065f, 0.0858f, zr + 0.047f), -1, true);
            }
            P.Disc(g, new Vector3(-0.34f, -0.43f, zr + 0.003f), 0.01f, 0.006f, mBrass).transform.localEulerAngles = new Vector3(90, 0, 0);
            Anchor("DOOR.T", new Vector3(-0.34f, -0.43f, zr + 0.007f), -1, true);
            Anchor("DOOR.HH", new Vector3(0.17f, -0.45f, zr + 0.012f), -1, true);
            Cyl(g, new Vector3(0.17f, -0.45f, zr + 0.008f), 0.02f, 0.016f, black, new Vector3(90, 0, 0));
        }

        void Block(Transform g, Vector3 c, string top, string bottom, bool nc)
        {
            Box(g, c, new Vector3(0.0115f, 0.033f, 0.036f), mWhite);
            Box(g, c + new Vector3(0, 0, 0.0181f), new Vector3(0.008f, 0.004f, 0.0005f), nc ? mRed : P.M(new Color(0.1f, 0.6f, 0.2f)));
            ScrewHead(g, c + new Vector3(0, 0.012f, 0.0182f)).transform.localEulerAngles = new Vector3(90, 0, 0);
            ScrewHead(g, c + new Vector3(0, -0.012f, 0.0182f)).transform.localEulerAngles = new Vector3(90, 0, 0);
            Anchor(top, c + new Vector3(0, 0.012f, 0.0205f), -1, true);
            Anchor(top, c + new Vector3(0.0012f, 0.0128f, 0.0205f), -1, true);
            Anchor(bottom, c + new Vector3(0, -0.012f, 0.0205f), -1, true);
            Anchor(bottom, c + new Vector3(0.0012f, -0.0128f, 0.0205f), -1, true);
        }

        /// Wire-through-part check: every routed segment against every part body (shrunk 2 mm).
        /// The two segments at each end (the tail into its own terminal) are skipped.
        void CheckCollisions()
        {
            collisions.Clear();
            foreach (var (wire, pts) in routes)
            {
                for (int i = 2; i < pts.Count - 3; i++)
                foreach (var (name, b) in solids)
                {
                    var s = new Bounds(b.center, b.size - Vector3.one * 0.004f);
                    if (SegmentHitsBox(pts[i], pts[i + 1], s))
                    {
                        collisions.Add($"{wire} through {name}");
                        break;
                    }
                }
            }
            Debug.Log($"[A11] Cabinet: {routes.Count} routed conductors, {collisions.Count} pass through a part" +
                      (collisions.Count > 0 ? ": " + string.Join("; ", collisions.Distinct().Take(20)) : ""));
        }

        static bool SegmentHitsBox(Vector3 a, Vector3 b, Bounds box)
        {
            Vector3 d = b - a;
            float t0 = 0, t1 = 1;
            for (int k = 0; k < 3; k++)
            {
                if (Mathf.Abs(d[k]) < 1e-9f)
                {
                    if (a[k] < box.min[k] || a[k] > box.max[k]) return false;
                    continue;
                }
                float inv = 1f / d[k];
                float ta = (box.min[k] - a[k]) * inv, tb = (box.max[k] - a[k]) * inv;
                if (ta > tb) (ta, tb) = (tb, ta);
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ wiring

        static float Section(string spec)
        {
            if (spec.StartsWith("6")) return 6f;
            if (spec.StartsWith("4")) return 4f;
            if (spec.StartsWith("2.5")) return 2.5f;
            if (spec.StartsWith("1.5")) return 1.5f;
            if (spec.StartsWith("1 ")) return 1f;
            if (spec.StartsWith("0.5")) return 0.5f;
            return 1.5f;
        }

        Material Insulation(Wire w)
        {
            string s = w.spec ?? "";
            if (s.Contains("shield,") || s.StartsWith("W1 braided")) return mCopper; // cable shield (braid)
            if (w.kind == WireKind.PE || s.Contains("G/Y")) return mGY;
            int band = s.IndexOf(", band");
            if (band >= 0) s = s.Substring(0, band); // the insulation colour; the band is only at the ends
            if (s.Contains("yellow")) return InsMat("yellow", new Color(0.92f, 0.78f, 0.1f));
            if (s.Contains("blue/white"))
            {
                if (!insMats.TryGetValue("blue/white", out var bw))
                {
                    insMats["blue/white"] = bw = P.ME(Color.white, Color.black, 0.45f);
                    bw.mainTexture = StripeTex(new Color(0.07f, 0.1f, 0.42f), new Color(0.9f, 0.9f, 0.88f));
                }
                return bw;
            }
            if (s.Contains("dark blue")) return InsMat("dark blue", new Color(0.07f, 0.1f, 0.42f));
            if (s.Contains("light blue")) return InsMat("light blue", new Color(0.35f, 0.62f, 0.9f));
            if (s.Contains("brown")) return InsMat("brown", new Color(0.36f, 0.2f, 0.1f));
            if (s.Contains("grey")) return InsMat("grey", new Color(0.5f, 0.5f, 0.52f));
            if (s.Contains("blue")) return InsMat("blue", new Color(0.12f, 0.3f, 0.75f));
            if (s.Contains("red")) return InsMat("red", new Color(0.75f, 0.08f, 0.06f));
            if (s.Contains("black")) return InsMat("black", new Color(0.06f, 0.06f, 0.065f));
            if (s.Contains("shielded")) return InsMat("shielded", new Color(0.12f, 0.12f, 0.13f));
            if (w.kind == WireKind.Motor) return InsMat("black", new Color(0.06f, 0.06f, 0.065f));
            int n = int.TryParse(w.number, out var v) ? v : 0;
            if (n >= 200 && n < 300) return InsMat("dark blue", new Color(0.07f, 0.1f, 0.42f)); // DC control (IEC 60204-1)
            return InsMat("white", new Color(0.86f, 0.86f, 0.83f));
        }

        readonly Dictionary<string, Material> insMats = new();

        Material InsMat(string key, Color c)
        {
            if (!insMats.TryGetValue(key, out var m)) insMats[key] = m = P.ME(c, Color.black, 0.45f);
            return m;
        }

        static Vector2 Offset(string key)
        {
            int h = 17;
            foreach (char ch in key) h = h * 31 + ch;
            h &= 0x7fffffff;
            return new Vector2(((h % 7) - 3) * 0.0042f, (((h / 7) % 9) - 4) * 0.0045f);
        }

        List<Vector3> Tail(Anc a, Vector2 off)
        {
            var pts = new List<Vector3> { a.p };
            if (a.duct <= 4)
            {
                float yc = HD[a.duct];
                bool above = a.p.y > yc;
                float e = above ? yc + DuctHalf + 0.004f : yc - DuctHalf - 0.004f;
                if (above ? a.p.y < e : a.p.y > e) e = a.p.y;
                pts.Add(new Vector3(a.p.x, e, a.p.z));
                pts.Add(new Vector3(a.p.x, yc + off.x, Zd + off.y));
            }
            else
            {
                float xc = VX(a.duct);
                bool left = a.p.x < xc;
                float e = left ? xc - DuctHalf - 0.004f : xc + DuctHalf + 0.004f;
                if (left ? a.p.x > e : a.p.x < e) e = a.p.x;
                pts.Add(new Vector3(e, a.p.y, a.p.z));
                pts.Add(new Vector3(xc + off.x, a.p.y, Zd + off.y));
            }
            return pts;
        }

        List<Vector3> Middle(Vector3 ea, int da, Vector3 eb, int db, Vector2 off, int forceV = -1)
        {
            var pts = new List<Vector3>();
            bool ha = da <= 4, hb = db <= 4;
            if (ha && hb)
            {
                if (da == db) return pts;
                int v = forceV >= 0 ? forceV
                    : Mathf.Abs(ea.x - VLx) + Mathf.Abs(eb.x - VLx) <= Mathf.Abs(ea.x - VRx) + Mathf.Abs(eb.x - VRx) ? VL : VR;
                float xv = VX(v) + off.x;
                pts.Add(new Vector3(xv, ea.y, Zd + off.y));
                pts.Add(new Vector3(xv, eb.y, Zd + off.y));
            }
            else if (!ha && hb) pts.Add(new Vector3(ea.x, HD[db] + off.x, Zd + off.y));
            else if (ha) pts.Add(new Vector3(eb.x, HD[da] + off.x, Zd + off.y));
            else if (da != db)
            {
                float yb = HD[4] + off.x;
                pts.Add(new Vector3(ea.x, yb, Zd + off.y));
                pts.Add(new Vector3(eb.x, yb, Zd + off.y));
            }
            return pts;
        }

        List<Vector3> CabinetRoute(Anc a, Anc b, Vector2 off)
        {
            var ta = Tail(a, off);
            var tb = Tail(b, off);
            var path = new List<Vector3>(ta);
            path.AddRange(Middle(ta[^1], a.duct, tb[^1], b.duct, off));
            tb.Reverse();
            path.AddRange(tb);
            return path;
        }

        /// Door-side route from a door anchor to the hinge exit, along the door's wiring channel.
        static List<Vector3> DoorToHinge(Vector3 p, Vector3 hinge, Vector2 off)
        {
            float z = DoorSpineZ + off.y * 0.5f, x = DoorSpineX + off.x * 0.6f;
            return new List<Vector3>
            {
                p, new(p.x, p.y, z), new(x, p.y, z), new(x, hinge.y + off.x * 0.5f, z), hinge + new Vector3(0, off.x * 0.5f, off.y * 0.3f),
            };
        }

        void Wiring()
        {
            var wroot = Group("wiring");
            var droot = Group("door wiring", door);
            int skipped = 0;
            var doorUpper = new List<Vector2>();
            foreach (var w in C.wires)
            {
                if (w.isBus || w.notConnected) continue;
                if (w.number == "" && w.kind != WireKind.Motor) continue;
                if (!anchors.ContainsKey(w.a.id) || !anchors.ContainsKey(w.b.id))
                {
                    skipped++;
                    external.Add(w);
                    continue;
                }
                TryTake(w.a.id, out var a);
                TryTake(w.b.id, out var b);
                string key = w.number != "" ? w.number : w.a.id + w.b.id;
                var off = Offset(key);
                float dia = (w.kind == WireKind.Motor ? 0.0034f : Section(w.spec) switch { 6f => 0.006f, 4f => 0.005f, 2.5f => 0.0042f, 1.5f => 0.0034f, _ => 0.0024f });
                var mat = Insulation(w);
                var wv = new WireVis { w = w, door = a.door || b.door };
                if (!a.door && !b.door)
                {
                    // A bridge between two close terminals of the same part (e.g. PUL+ → DIR+ on a drive plug) stays on the part.
                    bool bridge = w.a.id.Split('.')[0] == w.b.id.Split('.')[0] && Vector3.Distance(a.p, b.p) < 0.03f;
                    var route = bridge ? new List<Vector3> { a.p, a.p + Vector3.back * 0.008f, b.p + Vector3.back * 0.008f, b.p } : CabinetRoute(a, b, off);
                    routes.Add((key, route));
                    AddTube(wv, wroot, route, dia, mat);
                }
                else if (a.door && b.door)
                {
                    float sx = -0.016f + off.x * 0.4f, z = DoorSpineZ + off.y * 0.4f;
                    AddTube(wv, droot, new List<Vector3> { a.p, new(a.p.x, a.p.y, z), new(sx, a.p.y, z), new(sx, b.p.y, z), new(b.p.x, b.p.y, z), b.p }, dia, mat);
                }
                else
                {
                    var d = a.door ? a : b;
                    var c = a.door ? b : a;
                    bool lower = w.a.id == "DOOR.T" || w.b.id == "DOOR.T";
                    var hd = lower ? HingeDoorLower : HingeDoorUpper;
                    var hc = lower ? HingeCabLower : HingeCabUpper;
                    AddTube(wv, droot, DoorToHinge(d.p, hd, off), dia, mat);
                    var pc = new Anc { p = hc + new Vector3(0, off.x * 0.5f, off.y * 0.3f), duct = VL };
                    var route = CabinetRoute(pc, c, off);
                    routes.Add((key, route));
                    AddTube(wv, wroot, route, dia, mat);
                    wv.len += 0.30f; // service loop across the hinge (the model shows it compressed)
                    if (!lower) doorUpper.Add(off);
                }
                wires.Add(wv);
                if (w.number != "") Markers(wv, w.number, a.door ? droot : wroot, b.door ? droot : wroot, a, b);
            }
            doorWireCountUpper = doorUpper.Count;

            // Handheld cable: door gland → hinge loop → A11 board CN1.
            if (TryTake("DOOR.HH", out var hh) && TryTake("A1.HH", out var cn))
            {
                var cable = InsMat("cable", new Color(0.03f, 0.03f, 0.035f));
                var cv = new WireVis();
                AddTube(cv, droot, DoorToHinge(hh.p, HingeDoorUpper, Vector2.zero), 0.0062f, cable);
                AddTube(cv, wroot, CabinetRoute(new Anc { p = HingeCabUpper, duct = VL }, cn, new Vector2(0, -0.008f)), 0.0062f, cable);
                foreach (var r in cv.rends)
                {
                    var c = r.gameObject.AddComponent<MeshCollider>();
                    c.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    P.Register(c, () => "<b>Handheld cable</b>\n50-way, A11 handheld → door gland → service loop → A11E board CN1.", null, null);
                }
            }

            // Service loops across the hinge (rebuilt as the door swings).
            var sleeve = P.M(new Color(0.04f, 0.04f, 0.045f), 0f, 0.35f);
            sleeve.mainTexture = StripeTex(new Color(0.03f, 0.03f, 0.035f), new Color(0.12f, 0.12f, 0.13f));
            hingeUpper = MeshObj(wroot, "hinge loop (spiral sleeve)", new Mesh(), sleeve).GetComponent<MeshFilter>();
            hingeLower = MeshObj(wroot, "door earth strap", new Mesh(), mCopper).GetComponent<MeshFilter>();
            UpdateHinge();
            fitReport = $"{wires.Count} conductors routed" + (skipped > 0 ? $", {skipped} external (outside the cabinet)" : "");
            Debug.Log("[A11] Cabinet wiring: " + fitReport);
        }

        void AddTube(WireVis wv, Transform parent, List<Vector3> pts, float dia, Material mat)
        {
            var smooth = Smooth(pts, Mathf.Max(0.006f, dia * 2.5f));
            var mesh = Tube(smooth, dia * 0.5f, 8, 0.012f);
            for (int i = 1; i < smooth.Count; i++) wv.len += Vector3.Distance(smooth[i - 1], smooth[i]);
            var go = MeshObj(parent, wv.w != null ? "wire " + (wv.w.number != "" ? wv.w.number : wv.w.a.id) : "cable", mesh, mat);
            var r = go.GetComponent<MeshRenderer>();
            wv.rends.Add(r);
            wv.mats.Add(mat);
            if (wv.w != null)
            {
                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                var w = wv.w;
                P.Register(col, () => V.WireTip(w) + (selected == wv ? "\nClick again (or on empty space) to show all wires." : "\nClick to isolate this wire."),
                    null, null, () => ToggleSelect(wv));
            }
        }

        void Markers(WireVis wv, string num, Transform pa, Transform pb, Anc a, Anc b)
        {
            foreach (var (anc, parent) in new[] { (a, pa), (b, pb) })
            {
                Vector3 dir = anc.door ? Vector3.forward :(anc.duct <= 4 ? (anc.p.y > HD[anc.duct] ? Vector3.down : Vector3.up) : (anc.duct is VL or VP ? Vector3.left : Vector3.right));
                Vector3 p = anc.p + dir * 0.012f;
                var s = P.Prim(PrimitiveType.Cylinder, parent, p, new Vector3(0.0052f, 0.0035f, 0.0052f), mSleeve);
                s.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var t = P.Text(num, parent, p + new Vector3(0, 0, -0.0032f), 0.0024f, Color.black, true);
                if (anc.door) t.transform.localPosition = p + new Vector3(0, 0.0035f, 0);
                wv.markers.Add(s);
                wv.markers.Add(t.gameObject);
            }
        }

        void UpdateHinge()
        {
            Vector3 Door(Vector3 local) => root.InverseTransformPoint(door.TransformPoint(local));
            Vector3 DoorDir(Vector3 local) => root.InverseTransformDirection(door.TransformDirection(local));
            Vector3 a = Door(HingeDoorUpper), b = HingeCabUpper;
            var pts = new List<Vector3>();
            Vector3 c1 = a + DoorDir(Vector3.forward) * 0.03f + Vector3.down * 0.03f, c2 = b + new Vector3(0, -0.03f, -0.03f);
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                pts.Add(Mathf.Pow(1 - t, 3) * a + 3 * Mathf.Pow(1 - t, 2) * t * c1 + 3 * (1 - t) * t * t * c2 + t * t * t * b);
            }
            float bundle = 0.006f + 0.0012f * Mathf.Sqrt(Mathf.Max(1, doorWireCountUpper));
            hingeUpper.sharedMesh = Tube(pts, bundle, 12, 0.008f);
            Vector3 la = Door(HingeDoorLower), lb = HingeCabLower;
            var lp = new List<Vector3>();
            Vector3 l1 = la + DoorDir(Vector3.forward) * 0.02f + Vector3.down * 0.02f, l2 = lb + new Vector3(0, -0.02f, -0.02f);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                lp.Add(Mathf.Pow(1 - t, 3) * la + 3 * Mathf.Pow(1 - t, 2) * t * l1 + 3 * (1 - t) * t * t * l2 + t * t * t * lb);
            }
            hingeLower.sharedMesh = Tube(lp, 0.003f, 6, 0.004f);
        }

        // ------------------------------------------------------------------ dimensions

        void Dimensions()
        {
            dims = new GameObject("dimensions") { layer = Layer };
            dims.transform.SetParent(cab, false);
            var g = dims.transform;
            var m = P.ME(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.7f, 0.15f) * 0.6f, 0.2f);
            void Line(Vector3 a, Vector3 b)
            {
                var d = b - a;
                var go = P.Prim(PrimitiveType.Cylinder, g, (a + b) / 2, new Vector3(0.0015f, d.magnitude / 2, 0.0015f), m);
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            float hy = EncH / 2;
            float hx = EncW / 2;
            Line(new Vector3(-hx, -hy - 0.03f, -0.01f), new Vector3(hx, -hy - 0.03f, -0.01f));
            P.Text($"{EncW * 1000:0} mm", g, new Vector3(0, -hy - 0.045f, -0.01f), 0.016f, new Color(1f, 0.8f, 0.3f), true);
            Line(new Vector3(hx + 0.03f, -hy, -0.01f), new Vector3(hx + 0.03f, hy, -0.01f));
            P.Text($"{EncH * 1000:0} mm", g, new Vector3(hx + 0.08f, 0, -0.01f), 0.016f, new Color(1f, 0.8f, 0.3f), true);
            Line(new Vector3(hx + 0.03f, hy + 0.03f, 0f), new Vector3(hx + 0.03f, hy + 0.03f, EncD));
            P.Text($"{EncD * 1000:0} mm", g, new Vector3(hx + 0.03f, hy + 0.05f, EncD / 2), 0.016f, new Color(1f, 0.8f, 0.3f), true).transform.localEulerAngles = new Vector3(0, -90, 0);
            P.Text($"Steel wall cabinet, swing door hinged on the left · mounting plate {PlateW * 1000:0} × {PlateH * 1000:0} (measured)", g, new Vector3(0, -hy - 0.075f, -0.01f), 0.011f, new Color(1f, 0.8f, 0.3f));
            dims.SetActive(false);
        }

        // ------------------------------------------------------------------ runtime

        public void ToggleCovers() => covers.SetActive(!covers.activeSelf);
        public void ToggleDims() => dims.SetActive(!dims.activeSelf);

        public void Update(float dt, bool doorMoved)
        {
            if (doorMoved) UpdateHinge();
            float open = Mathf.Clamp01(P.doorAngle / 30f);
            interiorLight.intensity = 0.9f * open;
            interiorLight.enabled = open > 0.01f;
            if (Input.GetKeyDown(KeyCode.C) && P.full) ToggleCovers();
            if (Input.GetKeyDown(KeyCode.M) && P.full) ToggleDims();
            if (covers.activeSelf != P.showCovers) covers.SetActive(P.showCovers);

            foreach (var (t, on) in levers)
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(on() ? -20f : -160f, 0, 0), 1f - Mathf.Exp(-dt * 25f));
            foreach (var (t, ins) in fuseCarriers)
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(ins() ? 0f : 35f, 0, 0), 1f - Mathf.Exp(-dt * 20f));
            foreach (var (t, on, off) in flags)
            {
                if (!flagRest.TryGetValue(t, out var rest)) flagRest[t] = rest = t.localPosition;
                t.localPosition = Vector3.Lerp(t.localPosition, rest + (on() ? off : Vector3.zero), 1f - Mathf.Exp(-dt * 30f));
            }
            var mpb = ledBlock;
            foreach (var (r, c, on) in leds)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", on() ? c * 2.2f : Color.black);
                r.SetPropertyBlock(mpb);
            }
            if (fan != null && fan.running) fanRotor.Rotate(Vector3.right, 900f * dt, Space.Self);
            if (vfdDisplay != null)
            {
                // Rebuild the display string only when the shown value (0.1 Hz) changes.
                int shown = !vfd.powered ? -1 : Mathf.RoundToInt(vfd.freq * 10f);
                if (shown != vfdShown)
                {
                    vfdShown = shown;
                    vfdDisplay.text = shown < 0 ? "" : (shown / 10f).ToString("0.0");
                }
            }

            // Live wires: emission in the schematic's potential colours.
            // Build guide: wires not fitted yet are hidden, the one to fit now pulses.
            // Selected wire: all the others are ghosted and lose their markers.
            if (selected != null && selected.buildState == 0) selected = null;
            foreach (var wv in wires)
            {
                int bs = V.guide != null ? V.guide.StateOf(wv.w) : 1;
                bool ghost = selected != null && wv != selected;
                if (bs != wv.buildState || ghost != wv.ghost)
                {
                    wv.buildState = bs;
                    wv.ghost = ghost;
                    for (int i = 0; i < wv.rends.Count; i++)
                    {
                        var r = wv.rends[i];
                        r.enabled = bs != 0;
                        r.sharedMaterial = ghost ? Ghost(wv.mats[i]) : wv.mats[i];
                        r.shadowCastingMode = ghost ? ShadowCastingMode.Off : ShadowCastingMode.On;
                        if (r.TryGetComponent<MeshCollider>(out var mc)) mc.enabled = bs != 0;
                    }
                    foreach (var m in wv.markers) m.SetActive(bs != 0 && !ghost);
                }
                Color e = Color.black;
                if (bs == 2) e = new Color(1f, 0.78f, 0.15f) * (1.4f + 0.9f * Mathf.Sin(Time.time * 7f));
                else if (P.showLive && wv.w != null)
                {
                    var st = SchematicView.Style(wv.w);
                    if (st.gi > 0) e = st.col * (0.28f * st.gi) * (0.85f + 0.15f * Mathf.Sin(Time.time * 4f + wv.w.GetHashCode() % 10));
                }
                if ((e - wv.last).maxColorComponent < 0.02f && (wv.last - e).maxColorComponent < 0.02f) continue;
                wv.last = e;
                wv.mpb.SetColor("_EmissionColor", e);
                foreach (var r in wv.rends) r.SetPropertyBlock(wv.mpb);
            }
        }

        readonly Dictionary<Transform, Vector3> flagRest = new();
        int vfdShown = int.MinValue;
        readonly MaterialPropertyBlock ledBlock = new();

        // ------------------------------------------------------------------ mesh helpers

        static List<Vector3> Smooth(List<Vector3> p, float r)
        {
            var q = new List<Vector3> { p[0] };
            for (int i = 1; i < p.Count; i++)
                if ((p[i] - q[^1]).sqrMagnitude > 1e-9f) q.Add(p[i]);
            if (q.Count < 3) return q;
            var o = new List<Vector3> { q[0] };
            for (int i = 1; i < q.Count - 1; i++)
            {
                Vector3 a = q[i - 1], b = q[i], c = q[i + 1];
                float la = (b - a).magnitude, lc = (c - b).magnitude;
                float rr = Mathf.Min(r, la * 0.45f, lc * 0.45f);
                Vector3 s = b + (a - b).normalized * rr, e = b + (c - b).normalized * rr;
                for (int k = 0; k <= 5; k++)
                {
                    float t = k / 5f;
                    o.Add((1 - t) * (1 - t) * s + 2 * (1 - t) * t * b + t * t * e);
                }
            }
            o.Add(q[^1]);
            return o;
        }

        /// Tube along a polyline with parallel-transport frames; UV.x = length / uvScale (stripes).
        static Mesh Tube(List<Vector3> pts, float radius, int sides, float uvScale)
        {
            var clean = new List<Vector3> { pts[0] };
            for (int i = 1; i < pts.Count; i++)
                if ((pts[i] - clean[^1]).sqrMagnitude > 1e-10f) clean.Add(pts[i]);
            pts = clean;
            int n = pts.Count;
            var mesh = new Mesh();
            if (n < 2) return mesh;
            int ring = sides + 1;
            var v = new Vector3[n * ring];
            var nr = new Vector3[n * ring];
            var uv = new Vector2[n * ring];
            Vector3 t0 = (pts[1] - pts[0]).normalized;
            Vector3 nrm = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float arc = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = i == 0 ? pts[1] - pts[0] : i == n - 1 ? pts[n - 1] - pts[n - 2] : pts[i + 1] - pts[i - 1];
                t.Normalize();
                if (i > 0)
                {
                    arc += (pts[i] - pts[i - 1]).magnitude;
                    var pn = Vector3.ProjectOnPlane(nrm, t);
                    if (pn.sqrMagnitude > 1e-8f) nrm = pn.normalized;
                }
                Vector3 bin = Vector3.Cross(t, nrm);
                for (int s = 0; s <= sides; s++)
                {
                    float ang = s * Mathf.PI * 2 / sides;
                    Vector3 d = nrm * Mathf.Cos(ang) + bin * Mathf.Sin(ang);
                    v[i * ring + s] = pts[i] + d * radius;
                    nr[i * ring + s] = d;
                    uv[i * ring + s] = new Vector2(arc / uvScale, s / (float)sides);
                }
            }
            var tris = new int[(n - 1) * sides * 6];
            int k = 0;
            for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = i * ring + s, b = a + 1, c = a + ring, d = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }
            // Make the winding face outwards whatever the frame handedness turned out to be.
            Vector3 fn = Vector3.Cross(v[tris[1]] - v[tris[0]], v[tris[2]] - v[tris[0]]);
            if (Vector3.Dot(fn, nr[tris[0]]) < 0)
                for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            mesh.indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = v;
            mesh.normals = nr;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        class MeshBuilder
        {
            readonly List<Vector3> v = new(), n = new();
            readonly List<Vector2> uv = new();
            readonly List<int> t = new();

            public void Box(Vector3 c, Vector3 s)
            {
                Vector3 h = s * 0.5f;
                Face(c, new Vector3(0, 0, -1), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), h.z);
                Face(c, new Vector3(0, 0, 1), new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0), h.z);
                Face(c, new Vector3(-1, 0, 0), new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0), h.x);
                Face(c, new Vector3(1, 0, 0), new Vector3(0, 0, h.z), new Vector3(0, h.y, 0), h.x);
                Face(c, new Vector3(0, 1, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), h.y);
                Face(c, new Vector3(0, -1, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, -h.z), h.y);
            }

            void Face(Vector3 c, Vector3 normal, Vector3 u, Vector3 w, float dist)
            {
                int b = v.Count;
                Vector3 o = c + normal * dist;
                v.Add(o - u - w); v.Add(o - u + w); v.Add(o + u + w); v.Add(o + u - w);
                for (int i = 0; i < 4; i++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                Vector3 fn = Vector3.Cross(v[b + 1] - v[b], v[b + 2] - v[b]);
                if (Vector3.Dot(fn, normal) >= 0) t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                else t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }

            public Mesh Build()
            {
                var m = new Mesh { indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
