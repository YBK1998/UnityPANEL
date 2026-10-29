using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace A11Sim
{
    /// 3D model of the enclosure door (§ 6): E-stop, twin-head ON/OFF, three 24 V pilot lamps,
    /// A11 handheld in its holder and the cable gland. Built from primitives, lit in real time with
    /// shadows, and wired to the same circuit model as the schematic.
    public class Panel3D : MonoBehaviour
    {
        public const int Layer = 8;
        const float FZ = -0.008f; // door front plane (camera looks along +Z)
        // Swing door of the bought 800 × 1000 × 250 enclosure: 1.5 mm steel with a 20 mm folded return, hinged on the left.
        const float DoorW = 0.8f, DoorH = 1.0f, DoorT = 0.002f;
        const float HingeX = DoorW / 2, HingeZ = FZ + DoorT + 0.002f;

        public SchematicView view;
        public Camera cam;
        public RenderTexture rt;
        public bool full;
        public string hoverTip;

        Rect insetScreen;
        internal Transform root;
        internal Transform door, doorPivot; // door hangs on the left hinge line (x = −0.402 m)
        public float doorAngle, doorTarget;
        public const float DoorOpenAngle = 105f;
        public bool DoorOpen => doorTarget > 1f;
        internal Material std, stdE, haloTemplate;
        Cabinet3D cabinet;
        public Cabinet3D Cabinet => cabinet;
        public bool showLive = true;    // energized wires glow inside the cabinet
        public bool showCovers;         // wire-duct covers fitted

        internal class Btn
        {
            public string tip;
            public Func<string> tipF;
            public Transform cap;
            public Vector3 rest, pressedOff;
            public Func<bool> isDown;
            public Action down, up;
            public Action click;   // press and release without dragging (a drag still orbits)
        }

        readonly List<Btn> btns = new();
        readonly Dictionary<Collider, Btn> byCollider = new();
        Btn active, hover;
        Btn pressed;
        bool pressPending, pressMoved;
        Vector2 pressAt;

        class LampVis
        {
            public Lamp lamp;
            public Color col;
            public Material lens;
            public Light light;
            public Transform halo;
            public Material haloMat;
            public float level;
        }

        readonly List<LampVis> lamps = new();
        readonly List<Transform> billboards = new();
        TextMeshPro lcd;
        Material lcdMat;
        string lcdLast;
        float lcdAt = -1;
        bool lcdPowered;
        Btn estop;
        float estopTwist;
        bool estopWas;

        // Camera poses (door-local target, yaw/pitch in degrees, distance in m).
        Vector3 target = new(0, 0, 0);
        float yaw = 18, pitch = 6, dist = 2.2f;
        Vector3 insetTarget = new(0, 0.215f, FZ);
        bool orbiting, panning;
        Vector3 lastMouse;

        EStop s0;
        PushButton s1, s2;
        A11Board a11;

        // ------------------------------------------------------------------ build

        public void Build()
        {
            var c = view.circuit;
            s0 = c.Get<EStop>("S0");
            s1 = c.Get<PushButton>("S1");
            s2 = c.Get<PushButton>("S2");
            a11 = view.a11;

            std = Resources.Load<Material>("A11Std") ?? new Material(Shader.Find("Standard"));
            stdE = Resources.Load<Material>("A11StdEmissive") ?? std;
            haloTemplate = new Material(Shader.Find("A11/Halo"));

            root = new GameObject("Door 3D").transform;
            // Near the origin: at large coordinates single-precision camera math loses ~0.5° and clicks
            // miss small parts. Layer 8 + camera culling keep it apart from the schematic (z = 0, layer 0).
            root.position = new Vector3(0, 0, -50);
            root.gameObject.layer = Layer;
            doorPivot = new GameObject("door hinge").transform;
            doorPivot.gameObject.layer = Layer;
            doorPivot.SetParent(root, false);
            // The door swings about its left hinge line (seen from the front).
            doorPivot.localPosition = new Vector3(-HingeX, 0, HingeZ);
            door = new GameObject("door").transform;
            door.gameObject.layer = Layer;
            door.SetParent(doorPivot, false);
            door.localPosition = new Vector3(HingeX, 0, -HingeZ); // door-local = cabinet-local while closed

            SetupRendering();
            BuildEnclosure();
            var latch = new GameObject("door latch").transform;
            latch.gameObject.layer = Layer;
            latch.SetParent(door, false);
            latch.localPosition = new Vector3(DoorW / 2 - 0.035f, 0, FZ - 0.01f);
            Register(latch.gameObject, new Vector3(0.05f, 0.09f, 0.02f), Vector3.zero,
                () => $"<b>Door lock (quarter-turn)</b>\nClick (or O) to {(DoorOpen ? "close" : "open")} the door.", ToggleDoor, null);
            // The door plate blocks clicks to the equipment behind it while it is closed.
            var plate = new GameObject("door plate").transform;
            plate.gameObject.layer = Layer;
            plate.SetParent(door, false);
            plate.localPosition = new Vector3(0, 0, 0.0005f);
            Register(plate.gameObject, new Vector3(DoorW, DoorH, 0.017f), Vector3.zero,
                () => "<b>Door</b> — 800 × 1000 steel swing door, RAL 7035, hinged on the left, foamed gasket\n" +
                      "Cut the device holes (22.5 mm) and the handheld gland on assembly.\nPress O (or click the lock on the right) to open it.", null, null);
            BuildEStop(new Vector2(0, 0.335f));
            BuildOnOff(new Vector2(0, 0.205f));
            BuildLamp(c.Get<Lamp>("H1"), new Vector2(-0.085f, 0.085f), new Color(1f, 0.78f, 0.1f), "POWER ON");
            BuildLamp(c.Get<Lamp>("H2"), new Vector2(0, 0.085f), new Color(0.15f, 1f, 0.3f), "WORK");
            BuildLamp(c.Get<Lamp>("H3"), new Vector2(0.085f, 0.085f), new Color(1f, 0.12f, 0.08f), "ALARM");
            BuildHandheld(new Vector2(0, -0.2f));
            cabinet = new Cabinet3D(this, view);
            cabinet.Build();
            Physics.SyncTransforms();
        }

        public void ToggleDoor() => doorTarget = DoorOpen ? 0f : DoorOpenAngle;

        /// Click target without a moving cap (cabinet equipment, wires, latch).
        internal void Register(GameObject go, Vector3 size, Vector3 center, Func<string> tip, Action down, Action up)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.center = center;
            byCollider[bc] = new Btn { tipF = tip, isDown = () => false, down = down, up = up };
        }

        internal void Register(Collider c, Func<string> tip, Action down, Action up, Action click = null) =>
            byCollider[c] = new Btn { tipF = tip, isDown = () => false, down = down, up = up, click = click };

        void SetupRendering()
        {
            QualitySettings.antiAliasing = 8;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 4f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 8;

            var go = new GameObject("Door Camera");
            cam = go.AddComponent<Camera>();
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.045f, 0.05f, 0.065f);
            cam.fieldOfView = 28;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 20f;
            cam.allowMSAA = true;
            cam.depth = 1;
            view.cam.cullingMask &= ~(1 << Layer);

            var key = new GameObject("Door key light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(38, -28, 0);
            key.intensity = 1.0f;
            key.color = new Color(1f, 0.97f, 0.92f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            key.shadowBias = 0.02f;
            key.shadowNormalBias = 0.15f;
            key.cullingMask = 1 << Layer;
            var fill = new GameObject("Door fill light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(12, 35, 0);
            fill.intensity = 0.35f;
            fill.color = new Color(0.75f, 0.85f, 1f);
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << Layer;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.39f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.22f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.09f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = StudioCubemap();
            RenderSettings.reflectionIntensity = 1f;
        }

        /// Small procedural "studio" environment so chrome bezels have something to reflect.
        static Cubemap StudioCubemap()
        {
            const int n = 32;
            var cube = new Cubemap(n, TextureFormat.RGBA32, false);
            foreach (CubemapFace face in Enum.GetValues(typeof(CubemapFace)))
            {
                if (face == CubemapFace.Unknown) continue;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector3 d = FaceDir(face, (x + 0.5f) / n * 2 - 1, (y + 0.5f) / n * 2 - 1).normalized;
                    float up = d.y;
                    Color c = Color.Lerp(new Color(0.07f, 0.07f, 0.08f), new Color(0.55f, 0.58f, 0.64f), Mathf.SmoothStep(-0.3f, 0.9f, up));
                    // Two soft boxes.
                    c += new Color(1f, 0.98f, 0.95f) * Mathf.Pow(Mathf.Clamp01(Vector3.Dot(d, new Vector3(0.5f, 0.6f, -0.6f).normalized)), 40) * 2.5f;
                    c += new Color(0.8f, 0.9f, 1f) * Mathf.Pow(Mathf.Clamp01(Vector3.Dot(d, new Vector3(-0.7f, 0.3f, -0.6f).normalized)), 30) * 1.2f;
                    px[y * n + x] = c;
                }
                cube.SetPixels(px, face);
            }
            cube.Apply();
            return cube;
        }

        static Vector3 FaceDir(CubemapFace f, float u, float v) => f switch
        {
            CubemapFace.PositiveX => new Vector3(1, -v, -u),
            CubemapFace.NegativeX => new Vector3(-1, -v, u),
            CubemapFace.PositiveY => new Vector3(u, 1, v),
            CubemapFace.NegativeY => new Vector3(u, -1, -v),
            CubemapFace.PositiveZ => new Vector3(u, -v, 1),
            _ => new Vector3(-u, -v, -1),
        };

        // ------------------------------------------------------------------ primitives

        internal Material M(Color c, float metal = 0f, float smooth = 0.4f)
        {
            var m = new Material(std) { color = c };
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        internal Material ME(Color c, Color emission, float smooth = 0.7f)
        {
            var m = new Material(stdE) { color = c };
            m.SetFloat("_Metallic", 0);
            m.SetFloat("_Glossiness", smooth);
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        internal GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            if (!collider) Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// Cylinder with its axis along Z (towards the camera).
        internal GameObject Disc(Transform parent, Vector3 pos, float diameter, float thick, Material m) =>
            Prim(PrimitiveType.Cylinder, parent, pos, new Vector3(diameter, thick / 2, diameter), m, new Vector3(90, 0, 0));

        internal GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = false) =>
            Prim(PrimitiveType.Cube, parent, pos, size, m, null, collider);

        internal TextMeshPro Text(string s, Transform parent, Vector3 pos, float height, Color col, bool bold = false,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("text");
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.text = s;
            t.fontSize = height * 12f;
            t.color = col;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(0.001f, 0.001f);
            return t;
        }

        Btn AddButton(Transform cap, Vector3 pressedOff, string tip, Func<bool> isDown, Action down, Action up, Vector3 colSize, Vector3 colCenter)
        {
            var bc = cap.gameObject.AddComponent<BoxCollider>();
            bc.size = colSize;
            bc.center = colCenter;
            var b = new Btn
            {
                tip = tip, cap = cap, rest = cap.localPosition, pressedOff = pressedOff, isDown = isDown, down = down, up = up,
            };
            btns.Add(b);
            byCollider[bc] = b;
            return b;
        }

        // ------------------------------------------------------------------ parts

        void BuildEnclosure()
        {
            var ral7035 = M(new Color(0.66f, 0.67f, 0.66f), 0.2f, 0.42f);
            var body = M(new Color(0.48f, 0.50f, 0.50f), 0.25f, 0.35f);
            var chrome = M(new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f);
            var black = M(new Color(0.05f, 0.05f, 0.055f), 0f, 0.55f);

            Box(root, new Vector3(0, 0, 0.27f), new Vector3(4f, 3f, 0.02f), M(new Color(0.11f, 0.12f, 0.14f), 0, 0.1f));
            // The hollow enclosure body, mounting plate, equipment and wiring are built by Cabinet3D.
            // Door leaf; foamed gasket on its inside, closing on the body's front frame.
            Box(door, new Vector3(0, 0, FZ + DoorT / 2), new Vector3(DoorW, DoorH, DoorT), ral7035);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(door, new Vector3(0, s * (DoorH / 2 - 0.012f), FZ + DoorT + 0.001f), new Vector3(DoorW - 0.012f, 0.01f, 0.002f), black);
                Box(door, new Vector3(s * (DoorW / 2 - 0.012f), 0, FZ + DoorT + 0.001f), new Vector3(0.01f, DoorH - 0.034f, 0.002f), black);
            }
            // Three hinges on the left edge (seen from the front): knuckle on the hinge line, leaf on the door.
            foreach (float y in new[] { -0.38f, 0f, 0.38f })
            {
                Prim(PrimitiveType.Cylinder, root, new Vector3(-HingeX - 0.004f, y, HingeZ), new Vector3(0.008f, 0.03f, 0.008f), chrome).name = "hinge";
                Box(door, new Vector3(-DoorW / 2 + 0.006f, y, FZ - 0.0008f), new Vector3(0.014f, 0.05f, 0.0015f), chrome);
            }
            // Quarter-turn lock on the right.
            Disc(door, new Vector3(DoorW / 2 - 0.035f, 0, FZ - 0.003f), 0.026f, 0.006f, black);
            Box(door, new Vector3(DoorW / 2 - 0.035f, 0, FZ - 0.008f), new Vector3(0.006f, 0.018f, 0.006f), chrome);

            // Nameplate.
            Box(door, new Vector3(0, 0.452f, FZ - 0.001f), new Vector3(0.36f, 0.042f, 0.002f), M(new Color(0.06f, 0.065f, 0.075f), 0.2f, 0.6f));
            Text("UNITED CONTROL BOX", door, new Vector3(0, 0.46f, FZ - 0.0023f), 0.011f, new Color(0.93f, 0.95f, 1f), true);
            Text("CNC 3-axis router · RichAuto A11 · 230 V 50 Hz", door, new Vector3(0, 0.442f, FZ - 0.0023f), 0.0062f, new Color(0.6f, 0.65f, 0.72f));

            // Warning sticker (yellow triangle).
            var tri = new GameObject("warning");
            tri.layer = Layer;
            tri.transform.SetParent(door, false);
            tri.transform.localPosition = new Vector3(0.225f, 0.392f, FZ - 0.0006f);
            var mf = tri.AddComponent<MeshFilter>();
            mf.sharedMesh = TriangleMesh(0.052f, 0.004f);
            var mr = tri.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { M(new Color(0.05f, 0.05f, 0.05f)), M(new Color(1f, 0.82f, 0.05f), 0, 0.5f) };
            Text("!", door, new Vector3(0.225f, 0.388f, FZ - 0.0012f), 0.02f, Color.black, true);
            Text("DANGER 230 V", door, new Vector3(0.225f, 0.358f, FZ - 0.0008f), 0.0055f, new Color(0.15f, 0.15f, 0.15f), true);

            // Cable gland for the handheld cable.
            Disc(door, new Vector3(0.17f, -0.45f, FZ - 0.006f), 0.022f, 0.012f, black);
            Disc(door, new Vector3(0.17f, -0.45f, FZ - 0.0015f), 0.027f, 0.003f, M(new Color(0.2f, 0.2f, 0.22f), 0.3f, 0.5f));
        }

        /// Equilateral triangle sticker: submesh 0 = black border, submesh 1 = yellow face.
        static Mesh TriangleMesh(float side, float border)
        {
            Vector3 P(float s, float a) => new(Mathf.Cos(a * Mathf.Deg2Rad) * s, Mathf.Sin(a * Mathf.Deg2Rad) * s, 0);
            float R = side / Mathf.Sqrt(3);
            float r = R - border * 2;
            var v = new[] { P(R, 90), P(R, 210), P(R, 330), P(r, 90) + new Vector3(0, 0, -0.0002f), P(r, 210) + new Vector3(0, 0, -0.0002f), P(r, 330) + new Vector3(0, 0, -0.0002f) };
            var m = new Mesh { vertices = v, subMeshCount = 2 };
            m.SetTriangles(new[] { 0, 2, 1 }, 0);
            m.SetTriangles(new[] { 3, 5, 4 }, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void LegendPlate(Vector2 at, string text, float w = 0.066f)
        {
            Box(door, new Vector3(at.x, at.y, FZ - 0.00075f), new Vector3(w, 0.014f, 0.0015f), M(new Color(0.06f, 0.06f, 0.07f), 0.1f, 0.6f));
            Text(text, door, new Vector3(at.x, at.y, FZ - 0.0017f), 0.0062f, new Color(0.92f, 0.94f, 0.97f), true);
        }

        void BuildEStop(Vector2 p)
        {
            var yellow = M(new Color(1f, 0.8f, 0.05f), 0, 0.5f);
            var chrome = M(new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f);
            var red = M(new Color(0.78f, 0.04f, 0.03f), 0f, 0.78f);
            var redDark = M(new Color(0.5f, 0.03f, 0.02f), 0f, 0.6f);
            Disc(door, new Vector3(p.x, p.y, FZ - 0.00075f), 0.096f, 0.0015f, yellow);
            Text("EMERGENCY", door, new Vector3(p.x, p.y + 0.035f, FZ - 0.0018f), 0.0075f, Color.black, true);
            Text("STOP", door, new Vector3(p.x, p.y - 0.036f, FZ - 0.0018f), 0.0085f, Color.black, true);
            Disc(door, new Vector3(p.x, p.y, FZ - 0.004f), 0.034f, 0.008f, chrome);

            var cap = new GameObject("E-stop mushroom").transform;
            cap.gameObject.layer = Layer;
            cap.SetParent(door, false);
            cap.localPosition = new Vector3(p.x, p.y, FZ - 0.008f);
            Disc(cap, new Vector3(0, 0, -0.004f), 0.024f, 0.008f, redDark);
            Disc(cap, new Vector3(0, 0, -0.011f), 0.042f, 0.007f, red);
            Prim(PrimitiveType.Sphere, cap, new Vector3(0, 0, -0.0145f), new Vector3(0.042f, 0.042f, 0.017f), red);
            estop = AddButton(cap, new Vector3(0, 0, 0.0085f),
                "<b>S0 — E-stop XB2-BS542</b>\nClick: push (latching). Click again: twist to release.\nNC1 opens the K1 chain; NC2 (added ZB2-BE102 block) opens A11 X7.",
                () => s0.pressed, () => s0.pressed = !s0.pressed, null, new Vector3(0.044f, 0.044f, 0.03f), new Vector3(0, 0, -0.012f));
            Text("S0", door, new Vector3(p.x + 0.054f, p.y + 0.04f, FZ - 0.0008f), 0.006f, new Color(0.2f, 0.22f, 0.25f), true);
        }

        void BuildOnOff(Vector2 p)
        {
            var black = M(new Color(0.06f, 0.06f, 0.065f), 0.1f, 0.55f);
            var chrome = M(new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f);
            Box(door, new Vector3(p.x, p.y, FZ - 0.008f), new Vector3(0.048f, 0.088f, 0.016f), black);
            MakeTwin(new Vector2(p.x, p.y + 0.021f), new Color(0.08f, 0.55f, 0.18f), "I", chrome,
                "<b>S2 — ON (ZB2-BE101C, NO)</b>\nHold to start: K1 energizes and latches, KM1 closes.", s2);
            MakeTwin(new Vector2(p.x, p.y - 0.021f), new Color(0.75f, 0.06f, 0.05f), "O", chrome,
                "<b>S1 — OFF (ZB2-BE102C, NC)</b>\nHold to stop: opens the K1 chain.", s1);
            LegendPlate(new Vector2(p.x, p.y - 0.058f), "ON / OFF", 0.07f);
        }

        void MakeTwin(Vector2 at, Color col, string mark, Material chrome, string tip, PushButton pb)
        {
            Disc(door, new Vector3(at.x, at.y, FZ - 0.018f), 0.031f, 0.004f, chrome);
            var cap = new GameObject("button " + mark).transform;
            cap.gameObject.layer = Layer;
            cap.SetParent(door, false);
            cap.localPosition = new Vector3(at.x, at.y, FZ - 0.020f);
            Disc(cap, new Vector3(0, 0, -0.003f), 0.025f, 0.006f, M(col, 0f, 0.65f));
            Text(mark, cap, new Vector3(0, 0, -0.0062f), 0.012f, Color.white, true);
            AddButton(cap, new Vector3(0, 0, 0.004f), tip, () => pb.pressed, () => pb.panelHold = true, () => pb.panelHold = false,
                new Vector3(0.026f, 0.026f, 0.01f), new Vector3(0, 0, -0.004f));
        }

        void BuildLamp(Lamp lamp, Vector2 at, Color col, string legend)
        {
            var chrome = M(new Color(0.85f, 0.86f, 0.88f), 1f, 0.85f);
            Disc(door, new Vector3(at.x, at.y, FZ - 0.003f), 0.031f, 0.006f, chrome);
            var lens = ME(col * 0.45f, Color.black, 0.85f);
            Prim(PrimitiveType.Sphere, door, new Vector3(at.x, at.y, FZ - 0.0072f), new Vector3(0.025f, 0.025f, 0.014f), lens);
            Disc(door, new Vector3(at.x, at.y, FZ - 0.0065f), 0.026f, 0.003f, lens);
            var light = new GameObject("lamp light").AddComponent<Light>();
            light.gameObject.layer = Layer;
            light.transform.SetParent(door, false);
            light.transform.localPosition = new Vector3(at.x, at.y, FZ - 0.03f);
            light.type = LightType.Point;
            light.range = 0.2f;
            light.color = col;
            light.intensity = 0;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << Layer;
            var halo = Prim(PrimitiveType.Quad, door, new Vector3(at.x, at.y, FZ - 0.016f), Vector3.one * 0.1f, null);
            var hm = new Material(haloTemplate);
            halo.GetComponent<MeshRenderer>().sharedMaterial = hm;
            halo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            billboards.Add(halo.transform);
            lamps.Add(new LampVis { lamp = lamp, col = col, lens = lens, light = light, halo = halo.transform, haloMat = hm });
            LegendPlate(new Vector2(at.x, at.y - 0.037f), legend);
            var sp = new GameObject("lamp hit").transform;
            sp.gameObject.layer = Layer;
            sp.SetParent(door, false);
            sp.localPosition = new Vector3(at.x, at.y, FZ - 0.006f);
            var bc = sp.gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.03f, 0.03f, 0.012f);
            byCollider[bc] = new Btn { tip = $"<b>{lamp.title}</b>\n{lamp.info}", cap = null, isDown = () => false };
        }

        void BuildHandheld(Vector2 p)
        {
            float hf = FZ - 0.032f; // handheld front face
            var dark = M(new Color(0.10f, 0.11f, 0.13f), 0.1f, 0.45f);
            var face = M(new Color(0.17f, 0.19f, 0.23f), 0.05f, 0.3f);
            var black = M(new Color(0.04f, 0.04f, 0.045f), 0f, 0.6f);
            Box(door, new Vector3(p.x, p.y - 0.005f, FZ - 0.002f), new Vector3(0.108f, 0.18f, 0.004f), black);
            Box(door, new Vector3(p.x, p.y - 0.086f, FZ - 0.017f), new Vector3(0.108f, 0.03f, 0.034f), M(new Color(0.07f, 0.07f, 0.08f), 0.1f, 0.4f));
            Box(door, new Vector3(p.x, p.y, FZ - 0.019f), new Vector3(0.092f, 0.175f, 0.026f), dark);
            Box(door, new Vector3(p.x, p.y, hf - 0.0005f), new Vector3(0.086f, 0.169f, 0.001f), face);
            Box(door, new Vector3(p.x, p.y + 0.053f, hf - 0.0011f), new Vector3(0.074f, 0.042f, 0.0012f), black);
            lcdMat = ME(new Color(0.04f, 0.1f, 0.3f), new Color(0.05f, 0.16f, 0.5f), 0.9f);
            Box(door, new Vector3(p.x, p.y + 0.053f, hf - 0.0017f), new Vector3(0.066f, 0.034f, 0.0006f), lcdMat);
            lcd = Text("", door, new Vector3(p.x - 0.031f, p.y + 0.053f, hf - 0.0022f), 0.0047f, new Color(0.85f, 0.93f, 1f), false, TextAlignmentOptions.Left);
            lcd.lineSpacing = -8;
            Text("RichAuto", door, new Vector3(p.x - 0.036f, p.y + 0.0795f, hf - 0.0012f), 0.0042f, new Color(0.9f, 0.92f, 0.96f), true, TextAlignmentOptions.Left);
            Text("DSP A11", door, new Vector3(p.x + 0.036f, p.y + 0.0795f, hf - 0.0012f), 0.0036f, new Color(0.6f, 0.68f, 0.8f), true, TextAlignmentOptions.Right);

            var keyGrey = M(new Color(0.62f, 0.64f, 0.68f), 0f, 0.35f);
            var keyGreen = M(new Color(0.25f, 0.55f, 0.3f), 0f, 0.35f);
            var keyRed = M(new Color(0.68f, 0.2f, 0.17f), 0f, 0.35f);
            var keyOrange = M(new Color(0.8f, 0.5f, 0.15f), 0f, 0.35f);
            // Approximate A11 layout (4×4). Jog keys hold; the rest act once.
            var keys = new (string a, string b, Material m, Action down, Action up, string tip)[]
            {
                ("X+", "1", keyGrey, () => a11.jog[0] = 1, () => a11.jog[0] = 0, "Jog X+ (hold)"),
                ("Y+", "2", keyGrey, () => a11.jog[1] = 1, () => a11.jog[1] = 0, "Jog Y+ (hold)"),
                ("Z+", "3", keyGrey, () => a11.jog[2] = 1, () => a11.jog[2] = 0, "Jog Z+ (hold)"),
                ("HOME", "0", keyGrey, () => a11.HomeAll(), null, "Home all axes (Z+, X−, Y−)"),
                ("X−", "4", keyGrey, () => a11.jog[0] = -1, () => a11.jog[0] = 0, "Jog X− (hold)"),
                ("Y−", "5", keyGrey, () => a11.jog[1] = -1, () => a11.jog[1] = 0, "Jog Y− (hold)"),
                ("Z−", "6", keyGrey, () => a11.jog[2] = -1, () => a11.jog[2] = 0, "Jog Z− (hold)"),
                ("MENU", "", keyGrey, () => a11.message = "MENU (not simulated)", null, "Menu (not simulated)"),
                ("S−", "7", keyGrey, () => a11.SetGrade(a11.grade - 1), null, "Spindle speed grade down"),
                ("S+", "8", keyGrey, () => a11.SetGrade(a11.grade + 1), null, "Spindle speed grade up"),
                ("TOOL", "9", keyGrey, () => a11.ToolSet(), null, "Tool setting (Z down to the plate, X4)"),
                ("OK", "", keyGrey, () => a11.message = "OK", null, "OK"),
                ("ON/OFF", "SPINDLE", keyOrange, () => a11.ToggleSpindle(), null, "Spindle on / off (Y1 → VFD X1)"),
                ("HI/LO", "SPEED", keyGrey, () => { a11.jogLow = !a11.jogLow; a11.message = a11.jogLow ? "Jog speed LOW" : "Jog speed HIGH"; }, null, "Jog speed high / low"),
                ("RUN", "PAUSE", keyGreen, () => a11.RunJob(), null, "Run the demo job"),
                ("STOP", "CANCEL", keyRed, () => a11.Stop(), null, "Stop / cancel"),
            };
            for (int i = 0; i < keys.Length; i++)
            {
                int col = i % 4, row = i / 4;
                var at = new Vector3(p.x - 0.0285f + 0.019f * col, p.y + 0.019f - 0.0205f * row, hf - 0.0025f);
                var (a, b, m, down, up, tip) = keys[i];
                var cap = new GameObject("key " + a).transform;
                cap.gameObject.layer = Layer;
                cap.SetParent(door, false);
                cap.localPosition = at;
                Box(cap, Vector3.zero, new Vector3(0.0165f, 0.0165f, 0.005f), m);
                Color tc = m == keyGrey ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
                Text(a, cap, new Vector3(0, b == "" ? 0 : 0.0022f, -0.0027f), a.Length > 4 ? 0.0028f : 0.0036f, tc, true);
                if (b != "") Text(b, cap, new Vector3(0, -0.0042f, -0.0027f), 0.0026f, tc);
                bool held = false;
                var keyDown = down;
                var keyUp = up;
                AddButton(cap, new Vector3(0, 0, 0.0022f), $"<b>A11 key {a}{(b != "" ? " / " + b : "")}</b>\n{tip}",
                    () => held, () => { held = true; keyDown?.Invoke(); }, () => { held = false; keyUp?.Invoke(); },
                    new Vector3(0.0165f, 0.0165f, 0.006f), Vector3.zero);
            }

            // Spiral-free handheld cable: a smooth curve of short cylinders down to the gland.
            var cable = M(new Color(0.03f, 0.03f, 0.035f), 0f, 0.5f);
            Vector3 c0 = new(p.x, p.y - 0.1f, FZ - 0.017f), c1 = new(p.x + 0.07f, p.y - 0.16f, FZ - 0.035f), c2 = new(0.17f, -0.45f, FZ - 0.012f);
            Vector3 prev = c0;
            for (int i = 1; i <= 18; i++)
            {
                float t = i / 18f;
                Vector3 q = (1 - t) * (1 - t) * c0 + 2 * (1 - t) * t * c1 + t * t * c2;
                Vector3 d = q - prev;
                var seg = Prim(PrimitiveType.Cylinder, door, (q + prev) * 0.5f, new Vector3(0.0075f, d.magnitude * 0.5f + 0.0008f, 0.0075f), cable);
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                prev = q;
            }
        }

        // ------------------------------------------------------------------ runtime

        /// Inset area in screen pixels (y up). Called by the HUD every repaint.
        public void SetInset(Rect screenRect)
        {
            insetScreen = screenRect;
        }

        public void SetFull(bool on)
        {
            full = on;
            view.cam.enabled = !on;
            view.rig.enabled = !on;
            orbiting = panning = false;
        }

        public void FocusHandheld()
        {
            goalTarget = null;
            target = new Vector3(0, -0.2f, FZ - 0.03f);
            yaw = 10;
            pitch = 8;
            dist = 0.55f;
        }

        /// Look into the open cabinet.
        public void FocusInterior()
        {
            goalTarget = null;
            if (!DoorOpen) ToggleDoor();
            target = new Vector3(0.02f, 0, 0.16f);
            yaw = 2;
            pitch = 3;
            dist = 2.15f;
        }

        /// Screen point (pixels, y up) of a cabinet-local position in the full view (tests).
        public Vector2 ScreenPointOfLocal(Vector3 p)
        {
            var sp = cam.WorldToScreenPoint(root.TransformPoint(p));
            return new Vector2(sp.x, sp.y);
        }

        /// Close-up of the pose; used for screenshots and the "zoom" presets.
        public void Focus(Vector3 localTarget, float yawDeg, float pitchDeg, float distance)
        {
            goalTarget = null;
            target = localTarget;
            yaw = yawDeg;
            pitch = pitchDeg;
            dist = distance;
        }

        /// Glide the camera to a cabinet-local point (build guide), keeping the current angle.
        public void FocusSmooth(Vector3 localTarget, float distance)
        {
            goalTarget = localTarget;
            goalDist = distance;
        }

        Vector3? goalTarget;
        float goalDist;

        public void FocusDoor()
        {
            goalTarget = null;
            target = Vector3.zero;
            yaw = 18;
            pitch = 6;
            dist = 2.2f;
        }

        void EnsureRt()
        {
            int w = Mathf.Max(16, Mathf.RoundToInt(insetScreen.width));
            int h = Mathf.Max(16, Mathf.RoundToInt(insetScreen.height));
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null)
            {
                cam.targetTexture = null;
                rt.Release();
                Destroy(rt);
            }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8, name = "door inset" };
            rt.Create();
        }

        // Simulated pointer for automated tests (screen pixels, y up).
        public Vector2? simMouse;
        public bool simHeld;
        bool simPrev;
        public string lastHit;
        public Rect InsetRect => insetScreen;

        bool LeftDown => simMouse.HasValue ? simHeld && !simPrev : Input.GetMouseButtonDown(0);
        bool LeftHeld => simMouse.HasValue ? simHeld : Input.GetMouseButton(0);

        /// Screen point (pixels, y up) of a named 3D button, through the inset or the full view.
        public Vector2? ScreenPointOf(string capName)
        {
            foreach (var b in btns)
            {
                if (b.cap == null || b.cap.name != capName) continue;
                if (full)
                {
                    var sp = cam.WorldToScreenPoint(b.cap.position);
                    return new Vector2(sp.x, sp.y);
                }
                var vp = InsetViewport(b.cap.position);
                return new Vector2(insetScreen.x + vp.x * insetScreen.width, insetScreen.y + vp.y * insetScreen.height);
            }
            return null;
        }

        // The inset camera renders into a RenderTexture whose size and aspect are set from the HUD.
        // Build its projection explicitly from the inset size, so picking does not depend on what
        // Camera.ViewportPointToRay assumes about the current render target.
        float InsetAspect => insetScreen.height > 0 ? insetScreen.width / insetScreen.height : 1f;

        public Ray InsetRay(Vector2 viewport)
        {
            float t = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var local = new Vector3((viewport.x * 2 - 1) * t * InsetAspect, (viewport.y * 2 - 1) * t, 1f);
            return new Ray(cam.transform.position, cam.transform.TransformDirection(local).normalized);
        }

        Vector2 InsetViewport(Vector3 world)
        {
            var l = cam.transform.InverseTransformPoint(world);
            float t = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2((l.x / l.z / (t * InsetAspect) + 1) * 0.5f, (l.y / l.z / t + 1) * 0.5f);
        }

        void Update()
        {
            PerfStats.Begin();
            Pointer();
            PerfStats.Mark("panel3d.pointer");
            simPrev = simHeld;
        }

        void Pointer()
        {
            if (Input.GetKeyDown(KeyCode.V)) SetFull(!full);
            if (Input.GetKeyDown(KeyCode.O)) ToggleDoor();

            Vector2 mp = simMouse ?? (Vector2)Input.mousePosition;
            bool overInset = !full && insetScreen.width > 4 && insetScreen.Contains(mp);
            bool overFull = full && !view.hud.IsOver(mp);
            hover = null;
            hoverTip = null;
            // A click that did not drag: on a wire it isolates that wire, anywhere else it shows all wires again.
            if (pressPending)
            {
                if ((mp - pressAt).sqrMagnitude > 25f) pressMoved = true;
                if (!LeftHeld)
                {
                    pressPending = false;
                    if (!pressMoved)
                    {
                        if (pressed?.click != null) pressed.click();
                        else cabinet.ClearWireSelection();
                    }
                }
            }
            // Multimeter: in the full view a click near a wiring point places a probe.
            if (overFull && view.meter.active && active == null && !orbiting && !panning)
            {
                var t = cabinet.PickTerminal(mp, cam, 14f * Mathf.Max(1f, Screen.height / 1080f));
                if (t != null)
                {
                    hoverTip = $"<b>Probe → {t.dev.tag} {t.name}</b>\nClick to place the {(view.meter.nextBlack ? "black" : "<color=#ff6a5a>red</color>")} probe.";
                    if (LeftDown) view.meter.Place(t);
                    return;
                }
            }
            if ((overInset || overFull) && active == null && !orbiting && !panning)
            {
                Ray ray = overInset
                    ? InsetRay(new Vector2((mp.x - insetScreen.x) / insetScreen.width, (mp.y - insetScreen.y) / insetScreen.height))
                    : cam.ScreenPointToRay(mp);
                lastHit = null;
                if (Physics.Raycast(ray, out var hit, 20f, 1 << Layer))
                {
                    var par = hit.collider.transform.parent;
                    lastHit = (par != null ? par.name + "/" : "") + hit.collider.name + $" @ {root.InverseTransformPoint(hit.point).ToString("F3")}";
                    if (byCollider.TryGetValue(hit.collider, out var b))
                    {
                        hover = b;
                        hoverTip = b.tipF != null ? b.tipF() : b.tip;
                    }
                }
                if (LeftDown)
                {
                    if (hover?.down != null)
                    {
                        hover.down();
                        active = hover;
                    }
                    else
                    {
                        pressed = hover;
                        pressPending = true;
                        pressMoved = false;
                        pressAt = mp;
                    }
                    if (hover?.down == null && overFull)
                    {
                        orbiting = true;
                        goalTarget = null;
                        lastMouse = mp;
                    }
                }
                if (overFull && Input.GetMouseButtonDown(1))
                {
                    orbiting = true;
                    lastMouse = mp;
                }
                if (overFull && Input.GetMouseButtonDown(2))
                {
                    panning = true;
                    goalTarget = null;
                    lastMouse = mp;
                }
                if (overFull)
                {
                    float sc = Input.mouseScrollDelta.y;
                    if (Mathf.Abs(sc) > 0.01f)
                    {
                        goalTarget = null;
                        dist = Mathf.Clamp(dist * Mathf.Pow(0.88f, sc), 0.25f, 4f);
                    }
                }
            }
            else if (active != null) hoverTip = active.tipF != null ? active.tipF() : active.tip;
            if (active != null && !LeftHeld)
            {
                active.up?.Invoke();
                active = null;
            }
            if (orbiting)
            {
                Vector3 d = (Vector3)mp - lastMouse;
                yaw += d.x * 0.25f;
                pitch = Mathf.Clamp(pitch - d.y * 0.25f, -60, 75);
                lastMouse = mp;
                if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) orbiting = false;
            }
            if (panning)
            {
                Vector3 d = (Vector3)mp - lastMouse;
                var rot = Quaternion.Euler(pitch, yaw, 0);
                target -= (rot * Vector3.right * d.x + rot * Vector3.up * d.y) * dist * 0.0012f;
                lastMouse = mp;
                if (!Input.GetMouseButton(2)) panning = false;
            }
            if (full)
            {
                if (Input.GetKeyDown(KeyCode.F)) FocusDoor();
                if (Input.GetKeyDown(KeyCode.G)) FocusHandheld();
                if (Input.GetKeyDown(KeyCode.I)) FocusInterior();
            }
        }

        void LateUpdate()
        {
            double t0 = PerfStats.Now;
            PerfStats.Begin();
            LateUpdateBody();
            PerfStats.Mark("panel3d.late");
            PerfStats.panelMs += PerfStats.Now - t0;
        }

        void LateUpdateBody()
        {
            float dt = Time.deltaTime;
            // Camera.
            if (goalTarget.HasValue)
            {
                float k = 1f - Mathf.Exp(-dt * 5f);
                target = Vector3.Lerp(target, goalTarget.Value, k);
                dist = Mathf.Lerp(dist, goalDist, k);
                if ((target - goalTarget.Value).sqrMagnitude < 1e-6f && Mathf.Abs(dist - goalDist) < 0.002f) goalTarget = null;
            }
            if (full)
            {
                cam.targetTexture = null;
                cam.rect = new Rect(0, 0, 1, 1);
                cam.ResetAspect();
                Place(target, yaw, pitch, dist);
            }
            else if (insetScreen.width > 4)
            {
                EnsureRt();
                cam.targetTexture = rt;
                cam.aspect = InsetAspect; // keep the rendered picture and InsetRay on the same projection
                float d = 0.245f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Place(insetTarget, 14, 6, d);
            }
            cam.enabled = full || insetScreen.width > 4;

            // Buttons.
            foreach (var b in btns)
            {
                if (b.cap == null) continue;
                var want = b.rest + (b.isDown() ? b.pressedOff : Vector3.zero);
                b.cap.localPosition = Vector3.Lerp(b.cap.localPosition, want, 1f - Mathf.Exp(-dt * 30f));
            }
            if (estopWas && !s0.pressed) estopTwist = 1f;
            estopWas = s0.pressed;
            estopTwist = Mathf.Max(0, estopTwist - dt * 2.5f);
            if (estop != null) estop.cap.localRotation = Quaternion.Euler(0, 0, -55f * Mathf.Sin(estopTwist * Mathf.PI));

            // Lamps.
            foreach (var l in lamps)
            {
                l.level = Mathf.Lerp(l.level, l.lamp.lit ? 1f : 0f, 1f - Mathf.Exp(-dt * 18f));
                l.lens.color = Color.Lerp(l.col * 0.45f, Color.Lerp(l.col, Color.white, 0.35f), l.level);
                l.lens.SetColor("_EmissionColor", l.col * (0.03f + 2.6f * l.level));
                l.light.intensity = 1.6f * l.level;
                l.light.enabled = l.level > 0.01f;
                l.haloMat.SetColor("_Color", l.col * 0.9f * l.level);
                l.halo.gameObject.SetActive(l.level > 0.01f);
            }
            foreach (var t in billboards) t.rotation = cam.transform.rotation;

            // Door swing (eased) and the cabinet: equipment state, live wires, hinge bundle.
            float prevAngle = doorAngle;
            doorAngle = Mathf.MoveTowards(doorAngle, doorTarget, 140f * dt);
            doorPivot.localRotation = Quaternion.Euler(0, Mathf.SmoothStep(0, DoorOpenAngle, doorAngle / DoorOpenAngle), 0);
            cabinet.Update(dt, doorAngle != prevAngle);
            cabinet.UpdateTools(view.meter, view.proc, view.guide);

            // Handheld LCD: refreshed at 20 Hz like the real display (and at once on power changes).
            if (a11.powered == lcdPowered && Time.unscaledTime - lcdAt < 0.05f) return;
            lcdAt = Time.unscaledTime;
            lcdPowered = a11.powered;
            string text;
            if (!a11.powered) text = "";
            else
                text = $"{a11.StatusLine()}\nX{a11.dro[0],8:0.00}  Y{a11.dro[1],8:0.00}\nZ{a11.dro[2],8:0.00}  S{a11.grade} {(a11.spindleOn ? "ON" : "OFF")}\n{Trim(a11.message, 22)}";
            if (text != lcdLast)
            {
                lcdLast = text;
                lcd.text = text;
                lcdMat.SetColor("_EmissionColor", a11.powered ? new Color(0.05f, 0.16f, 0.5f) : new Color(0.005f, 0.01f, 0.02f));
                lcdMat.color = a11.powered ? new Color(0.04f, 0.1f, 0.3f) : new Color(0.02f, 0.03f, 0.05f);
            }
        }

        void Place(Vector3 localTarget, float yawDeg, float pitchDeg, float distance)
        {
            var rot = Quaternion.Euler(pitchDeg, yawDeg, 0);
            Vector3 t = root.TransformPoint(localTarget);
            cam.transform.SetPositionAndRotation(t - rot * Vector3.forward * distance, rot);
        }

        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n - 1) + "…";
    }
}
