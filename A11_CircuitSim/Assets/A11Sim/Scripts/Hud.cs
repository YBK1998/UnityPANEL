using System;
using UnityEngine;

namespace A11Sim
{
    /// Screen overlay. Layout: app bar (title, live status, tools, window), left sidebar (operator panel, power,
    /// faults, display), right sidebar (A11 handheld, machine position), footer (wire legend, renderer), and the
    /// schematic / 3D view in between. Styles and widgets: HudTheme.cs; tools: HudTools.cs; status and help: HudStatus.cs.
    public partial class Hud : MonoBehaviour
    {
        public SchematicView view;

        float scale = 1;
        Rect bar, foot, left, right, legend;
        int heldId = -1, prevHeld = -1;
        const int HoldOn = 1, HoldOff = 2, HoldLever = 3, JogBase = 10;

        Breaker qf0, qf1, qf2, qf3, qf4;
        Fuse f1, f2;
        EStop s0;
        PushButton s1, s2;
        Lamp h1, h2, h3;
        StepperDrive[] drv;
        SensorConnector[] sens;
        ToolSetter ts;
        A11Board a11;
        Vfd vfd;
        Machine m;

        GUIStyle sPanel, sHead, sSection, sText, sSmall, sBtn, sBtnOn, sBtnWarn, sBtnRed, sBtnRedOn, sLcd, sTip, sLegend, sSmallCenterTop, sSmallCenter;
        float[] legendW;
        Texture2D texWhite, texCircle;
        Font uiFont;
        bool ready;

        void Awake()
        {
            // No GUILayout here: skip IMGUI's Layout event, which halves the OnGUI passes per frame.
            useGUILayout = false;
        }

        void Start()
        {
            var c = view.circuit;
            qf0 = c.Get<Breaker>("QF0");
            qf1 = c.Get<Breaker>("QF1");
            qf2 = c.Get<Breaker>("QF2");
            qf3 = c.Get<Breaker>("QF3");
            qf4 = c.Get<Breaker>("QF4");
            f1 = c.Get<Fuse>("F1");
            f2 = c.Get<Fuse>("F2");
            s0 = c.Get<EStop>("S0");
            s1 = c.Get<PushButton>("S1");
            s2 = c.Get<PushButton>("S2");
            h1 = c.Get<Lamp>("H1");
            h2 = c.Get<Lamp>("H2");
            h3 = c.Get<Lamp>("H3");
            drv = new[] { c.Get<StepperDrive>("A2"), c.Get<StepperDrive>("A3"), c.Get<StepperDrive>("A4") };
            sens = new[] { c.Get<SensorConnector>("X-LIM"), c.Get<SensorConnector>("Y-LIM"), c.Get<SensorConnector>("Z-LIM") };
            ts = c.Get<ToolSetter>("XT");
            a11 = view.a11;
            vfd = view.vfd;
            m = c.machine;
        }

        public bool hidden;

        static float ScaleFor() => Mathf.Clamp(Screen.height / 900f, 0.72f, 1.8f);

        public bool IsOver(Vector2 screen)
        {
            if (hidden) return false;
            var g = new Vector2(screen.x, Screen.height - screen.y) / scale;
            return bar.Contains(g) || foot.Contains(g) || left.Contains(g) || right.Contains(g) || OverTools(g) || helpOpen;
        }

        /// Screen area (pixels, y up) between the bars and sidebars — used by "fit view".
        public Rect FreeScreenRect()
        {
            if (hidden) return new Rect(0, 0, Screen.width, Screen.height);
            float s = ScaleFor();
            return Rect.MinMaxRect((Ui.SideW + 16) * s, (Ui.FootH + 16) * s, Screen.width - (Ui.SideW + 16) * s, Screen.height - (Ui.BarH + 16) * s);
        }

        void Update()
        {
            smoothDt = Mathf.Lerp(smoothDt, Time.unscaledDeltaTime, 0.05f);
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                hidden = !hidden;
                view.rig.Fit();
            }
            ToolKeys();
            HelpKeys();
            if (heldId >= 0 && !Input.GetMouseButton(0)) heldId = -1;
            if (heldId == prevHeld) return;
            // Release what the previous hold pressed, then press the new one.
            Apply(prevHeld, false);
            Apply(heldId, true);
            prevHeld = heldId;
        }

        void Apply(int id, bool on)
        {
            if (id < 0) return;
            if (id == HoldOn) s2.uiHold = on;
            else if (id == HoldOff) s1.uiHold = on;
            else if (id == HoldLever) view.circuit.Get<RelayK1>("K1").testLever = on;
            else if (id >= JogBase && id < JogBase + 6)
            {
                int axis = (id - JogBase) / 2;
                int dir = (id - JogBase) % 2 == 0 ? -1 : 1;
                a11.jog[axis] = on ? dir : 0;
            }
        }

        // ------------------------------------------------------------------ textures and basic widgets

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static Texture2D RoundTex(Color fill, Color border, int size = 16, float radius = 5)
        {
            var t = new Texture2D(size, size) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                bool edge = d > radius - 1.2f || x == 0 || y == 0 || x == size - 1 || y == size - 1;
                var c = edge ? border : fill;
                c.a *= a;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (ready) return;
            ready = true;
            InitTheme();
            InitThemeExtras();
        }

        bool Button(Rect r, string text, GUIStyle st) => GUI.Button(r, text, st);

        void Hold(Rect r, string text, int id, GUIStyle st, GUIStyle held)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                heldId = id;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && heldId == id)
            {
                heldId = -1;
                e.Use();
            }
            GUI.Box(r, text, heldId == id ? held : st);
        }

        void Dot(Rect r, Color c, bool on)
        {
            var old = GUI.color;
            GUI.color = on ? c : new Color(c.r * 0.3f + 0.08f, c.g * 0.3f + 0.08f, c.b * 0.3f + 0.1f, 1);
            if (on)
            {
                var gr = new Rect(r.x - r.width * 0.5f, r.y - r.height * 0.5f, r.width * 2, r.height * 2);
                GUI.color = new Color(c.r, c.g, c.b, 0.3f);
                GUI.DrawTexture(gr, texCircle);
                GUI.color = c;
            }
            GUI.DrawTexture(r, texCircle);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ OnGUI

        void OnGUI()
        {
            double t0 = PerfStats.Now;
            PerfStats.Begin();
            OnGUIBody();
            PerfStats.Mark("hud." + Event.current.type);
            PerfStats.hudMs += PerfStats.Now - t0;
            PerfStats.hudEvents++;
        }

        void OnGUIBody()
        {
            if (view == null || a11 == null) return;
            InitStyles();
            scale = ScaleFor();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float W = Screen.width / scale, H = Screen.height / scale;

            bar = new Rect(0, 0, W, Ui.BarH);
            foot = new Rect(0, H - Ui.FootH, W, Ui.FootH);
            left = new Rect(0, Ui.BarH, Ui.SideW, H - Ui.BarH - Ui.FootH);
            right = new Rect(W - Ui.SideW, Ui.BarH, Ui.SideW, H - Ui.BarH - Ui.FootH);
            legend = foot;

            if (hidden)
            {
                GUI.Label(new Rect(12, 8, 600, 18), "Tab — show panels · E e-stop · Q main CB · hold 1 = ON, 0 = OFF · F1 help", sSmall);
                DrawTooltip();
                return;
            }
            double t = PerfStats.Now;
            void Lap(string name)
            {
                if (!PerfStats.probe) return;
                double n = PerfStats.Now;
                PerfStats.time[name] = (PerfStats.time.TryGetValue(name, out var v) ? v : 0) + (n - t);
                t = n;
                PerfStats.Mark(name);
            }
            PerfStats.Begin();
            DrawLeft();
            Lap("hud.left");
            DrawRight();
            Lap("hud.right");
            DrawFooter();
            Lap("hud.legend");
            DrawAppBar(W);
            Lap("hud.status");
            DrawTools(W, H);
            Lap("hud.tools");
            DrawToast();
            DrawHelp(W, H);
            DrawTooltip();
            Lap("hud.tooltip");
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(view.toast) || Time.time > view.toastUntil) return;
            var c = new GUIContent(view.toast);
            float w = Mathf.Min(560, right.xMin - left.xMax - 40);
            float h = sTip.CalcHeight(c, w - 4);
            var r = new Rect((left.xMax + right.xMin) / 2 - w / 2, toolsBottom + 10, w, h);
            GUI.Box(r, c, sTip);
            Rounded(new Rect(r.x + 1, r.y + 6, 3, r.height - 12), Ui.Warn, 1.5f);
        }

        // ------------------------------------------------------------------ app bar and footer

        void DrawAppBar(float W)
        {
            Fill(bar, Ui.Surface);
            Fill(new Rect(0, bar.yMax - 1, W, 1), Ui.Border);
            // Brand.
            var mark = new Rect(14, 11, 24, 24);
            Rounded(mark, Ui.Accent, 6);
            GUI.Label(mark, "A11", sMarkStyle);
            GUI.Label(new Rect(46, 6, 300, 20), "A11 Control Box", sTitle);
            GUI.Label(new Rect(46, 24, 330, 16), EnclosureSubtitle(), sSub);

            // Right: tools, help, window.
            float x = W - 14;
            x -= 32;
            if (Button(new Rect(x, 9, 32, 28), "×", sBtnGhost)) Application.Quit();
            x -= 44;
            bool borderless = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
            if (Button(new Rect(x, 9, 40, 28), borderless ? "Win" : "Full", sBtnGhostSm)) SchematicView.SetBorderless(!borderless);
            x -= 12;
            Fill(new Rect(x, 13, 1, 20), Ui.Border);
            x -= 12 + 62;
            if (Button(new Rect(x, 9, 62, 28), "Help", helpOpen ? sBtnOn : sBtnGhost)) SetHelp(!helpOpen);
            float tw = 118;
            x -= tw + 6;
            if (Button(new Rect(x, 9, tw, 28), ToolLabel("Build guide", "B"), Guide.active ? sBtnOn : sBtnGhost)) ToggleGuide();
            x -= tw + 4;
            if (Button(new Rect(x, 9, tw, 28), ToolLabel("Procedures", "P"), Proc.Active || procMenu ? sBtnOn : sBtnGhost)) ToggleProcedures();
            x -= tw + 4;
            if (Button(new Rect(x, 9, tw, 28), ToolLabel("Multimeter", "T"), Meter.active ? sBtnOn : sBtnGhost)) Meter.active = !Meter.active;
            float toolsLeft = x;

            // Centre: live status.
            DrawStatusPills(380, toolsLeft - 16);
        }

        static readonly System.Collections.Generic.Dictionary<string, string> toolLabels = new();

        static string ToolLabel(string name, string key)
        {
            if (!toolLabels.TryGetValue(name, out var s))
                toolLabels[name] = s = $"{name}  <color={Hex(Ui.Text3)}>{key}</color>";
            return s;
        }

        GUIStyle sMarkStyle;
        string encSub;

        string EnclosureSubtitle() =>
            encSub ??= "RichAuto A11 · wiring rev 0.7 · enclosure 800 × 1000 × 250, swing door";
        string gpuMeasured;
        float gpuW;

        void DrawFooter()
        {
            Fill(foot, Ui.Surface);
            Fill(new Rect(0, foot.y, foot.width, 1), Ui.Border);
            float y = foot.y + 5;
            RefreshFrameText();
            // Renderer and frame rate (right).
            if (!ReferenceEquals(gpuMeasured, gpuText))
            {
                gpuMeasured = gpuText;
                chipContent.text = gpuText;
                gpuW = sLegend.CalcSize(chipContent).x;
            }
            float gw = gpuW;
            GUI.Label(new Rect(foot.xMax - gw - 14, y, gw + 4, 16), gpuText, sLegend);
            // Wire legend (left, between the sidebars when there is room).
            if (view.panel.full) return;
            float x = Ui.SideW + 14, avail = foot.xMax - gw - 40 - x;
            float need = 0;
            for (int i = 0; i < legendW.Length; i++) need += legendW[i] + 26;
            float k = Mathf.Min(1.15f, avail / need);
            bool labels = k > 0.7f;
            for (int i = 0; i < LegendItems.Length; i++)
            {
                var (c, l) = LegendItems[i];
                float tw = labels ? legendW[i] : 0;
                Rounded(new Rect(x, y + 6, 14, 4), c, 2);
                if (labels) GUI.Label(new Rect(x + 18, y, tw + 4, 16), l, sLegend);
                x += labels ? (tw + 26) * k : avail / LegendItems.Length;
            }
        }

        // ------------------------------------------------------------------ left sidebar

        void DrawLeft()
        {
            Fill(left, Ui.Surface);
            Fill(new Rect(left.xMax - 1, left.y, 1, left.height), Ui.Border);
            float x = left.x + Ui.Pad, w = left.width - 2 * Ui.Pad, y = left.y + 12;

            Section(x, ref y, w, "OPERATOR PANEL", "click to operate");
            var inset = new Rect(x, y, w, 168);
            var panel = view.panel;
            Rounded(inset, new Color(0.03f, 0.035f, 0.045f), 6);
            if (!panel.full && panel.rt != null) GUI.DrawTexture(inset, panel.rt, ScaleMode.StretchToFill, false);
            else GUI.Label(inset, panel.full ? "\n\n\n\n3D view is full screen\nV — back to the schematic" : "", sSmallCenterTop);
            if (!panel.full && Event.current.type == EventType.Repaint)
                panel.SetInset(new Rect(inset.x * scale, Screen.height - inset.yMax * scale, inset.width * scale, inset.height * scale));
            Outline(inset, Ui.Border, 6);
            y += 176;

            if (Button(new Rect(x, y, w, 34), s0.pressed ? "E-STOP ACTIVE — click to release" : "EMERGENCY STOP   E", s0.pressed ? sBtnRedOn : sBtnRed))
                s0.pressed = !s0.pressed;
            y += 40;
            float b3 = (w - 8) / 3;
            if (Button(new Rect(x, y, b3, 26), panel.full ? "Schematic" : "3D view", panel.full ? sBtnSmOn : sBtnSm)) panel.SetFull(!panel.full);
            if (Button(new Rect(x + b3 + 4, y, b3, 26), panel.DoorOpen ? "Close door" : "Open door", panel.DoorOpen ? sBtnSmOn : sBtnSm))
            {
                panel.ToggleDoor();
                if (panel.DoorOpen && !panel.full) panel.SetFull(true);
                if (panel.DoorOpen) panel.FocusInterior();
            }
            if (Button(new Rect(x + 2 * (b3 + 4), y, b3, 26), "Live wires", panel.showLive ? sBtnSmOn : sBtnSm)) panel.showLive = !panel.showLive;
            y += 26 + 18;

            Section(x, ref y, w, "POWER DISTRIBUTION");
            if (SwitchRow(x, ref y, w, "QF0", "Main C25 · Q", qf0.closed, Ui.Ok, Ui.Ok)) qf0.closed = !qf0.closed;
            if (SwitchRow(x, ref y, w, "QF4", "24 V PSU C6", qf4.closed, Ui.Ok, Ui.Ok)) qf4.closed = !qf4.closed;
            if (SwitchRow(x, ref y, w, "QF1", "VFD C16", qf1.closed, Ui.Ok, Ui.Ok)) qf1.closed = !qf1.closed;
            if (SwitchRow(x, ref y, w, "QF2", "48 V drives C10", qf2.closed, Ui.Ok, Ui.Ok)) qf2.closed = !qf2.closed;
            if (SwitchRow(x, ref y, w, "QF3", "Pump socket C10", qf3.closed, Ui.Ok, Ui.Ok)) qf3.closed = !qf3.closed;
            if (SwitchRow(x, ref y, w, "F1", "Control fuse 2 A", f1.inserted, Ui.Ok, Ui.Ok)) f1.inserted = !f1.inserted;
            if (SwitchRow(x, ref y, w, "F2", "Signal fuse 2 A", f2.inserted, Ui.Ok, Ui.Ok)) f2.inserted = !f2.inserted;
            y += 14;

            Section(x, ref y, w, "FAULTS AND SENSORS", "amber = forced");
            for (int i = 0; i < 3; i++)
            {
                var d = drv[i];
                var r = new Rect(x + i * (b3 + 4), y, b3, 26);
                if (Button(r, d.fault ? DriveFault[i] : DriveOk[i], d.fault ? sBtnRedOn : sBtn)) d.fault = !d.fault;
                Led(new Rect(r.x + 8, r.y + 9, 8, 8), d.fault ? Ui.Danger : Ui.Ok, d.powered || d.fault);
            }
            y += 32;
            int k = 0;
            float hw = (w - 4) / 2;
            foreach (var sc in sens)
                foreach (var s in sc.sensors)
                {
                    var r = new Rect(x + (k % 2) * (hw + 4), y + (k / 2) * 30, hw, 26);
                    if (Button(r, s.name, s.manual ? sBtnWarn : sBtnLeft)) s.manual = !s.manual;
                    Led(new Rect(r.xMax - 16, r.y + 9, 8, 8), Ui.Ok, s.active);
                    k++;
                }
            y += 3 * 30;
            var tr = new Rect(x, y, w, 26);
            if (Button(tr, ts.touch ? "Tool setter — touching" : "Tool setter — open", ts.manual ? sBtnWarn : sBtnLeft)) ts.manual = !ts.manual;
            Led(new Rect(tr.xMax - 16, tr.y + 9, 8, 8), Ui.Ok, ts.touch);
            y += 26 + 14;

            Section(x, ref y, w, "DISPLAY", "F fit view");
            if (SwitchRow(x, ref y, w, "", "Wire numbers", view.showNumbers, Ui.Accent, Ui.Accent)) view.showNumbers = !view.showNumbers;
            if (SwitchRow(x, ref y, w, "", "Current-flow animation", view.showFlow, Ui.Accent, Ui.Accent)) view.showFlow = !view.showFlow;
            if (SwitchRow(x, ref y, w, "", "Cautions C1–C14", view.showCautions, Ui.Accent, Ui.Warn)) view.showCautions = !view.showCautions;
        }

        GUIStyle sBtnLeft;

        // ------------------------------------------------------------------ right sidebar

        void DrawRight()
        {
            Fill(right, Ui.Surface);
            Fill(new Rect(right.x, right.y, 1, right.height), Ui.Border);
            float x = right.x + Ui.Pad, w = right.width - 2 * Ui.Pad, y = right.y + 12;

            string badge = !a11.powered ? "NO POWER" : a11.Alarm != null ? "ALARM" : a11.mode == A11Board.Mode.Job ? "RUNNING" : "READY";
            Color bc = !a11.powered ? Ui.Text3 : a11.Alarm != null ? Ui.Danger : a11.mode == A11Board.Mode.Job ? Ui.Accent : Ui.Ok;
            Section(x, ref y, w, "A11 HANDHELD");
            float bw0 = ChipWidth(badge);
            float px = x + w - bw0 + 6;
            Pill(ref px, y - 30, badge, bc, true, a11.Alarm != null && a11.powered);

            RefreshFrameText();
            GUI.Box(new Rect(x, y, w, 100), lcdText, sLcd);
            y += 108;

            float bw = (w - 8) / 4;
            if (Button(new Rect(x, y, bw * 2 + 4, 30), a11.spindleOn ? "Spindle  ON" : "Spindle  OFF", a11.spindleOn ? sBtnOn : sBtn)) a11.ToggleSpindle();
            if (Button(new Rect(x + 2 * (bw + 4), y, bw - 2, 30), "S −", sBtn)) a11.SetGrade(a11.grade - 1);
            if (Button(new Rect(x + 3 * (bw + 4) - 2, y, bw + 2, 30), "S +", sBtn)) a11.SetGrade(a11.grade + 1);
            y += 34;
            GUI.Label(new Rect(x, y, w, 16), gradeText, sMonoSmall);
            y += 24;

            // Jog pad (hold): X / Y cross, Z column.
            float jb = (w - 12) / 4, jh = 30;
            Hold(new Rect(x + jb + 4, y, jb, jh), "Y+", JogBase + 3, sBtn, sBtnOn);
            Hold(new Rect(x + 3 * (jb + 4), y, jb, jh), "Z+", JogBase + 5, sBtn, sBtnOn);
            Hold(new Rect(x, y + jh + 4, jb, jh), "X−", JogBase + 0, sBtn, sBtnOn);
            Hold(new Rect(x + 2 * (jb + 4), y + jh + 4, jb, jh), "X+", JogBase + 1, sBtn, sBtnOn);
            GUI.Label(new Rect(x + jb + 4, y + jh + 4, jb, jh), "JOG", sSmallCenter);
            Hold(new Rect(x + jb + 4, y + 2 * (jh + 4), jb, jh), "Y−", JogBase + 2, sBtn, sBtnOn);
            Hold(new Rect(x + 3 * (jb + 4), y + 2 * (jh + 4), jb, jh), "Z−", JogBase + 4, sBtn, sBtnOn);
            y += 3 * (jh + 4) + 6;

            float hb = (w - 4) / 2;
            if (Button(new Rect(x, y, hb, 30), "Home all", a11.mode == A11Board.Mode.Homing ? sBtnOn : sBtn)) a11.HomeAll();
            if (Button(new Rect(x + hb + 4, y, hb, 30), "Tool set", a11.mode == A11Board.Mode.ToolSet ? sBtnOn : sBtn)) a11.ToolSet();
            y += 34;
            if (Button(new Rect(x, y, hb, 30), "Run demo job", a11.mode == A11Board.Mode.Job ? sBtnOn : sBtnPrimary)) a11.RunJob();
            if (Button(new Rect(x + hb + 4, y, hb, 30), "Stop   Esc", sBtnStop)) a11.Stop();
            y += 30 + 18;

            // Machine position (actual): top view and Z bar.
            Section(x, ref y, w, "MACHINE POSITION", "actual");
            float pw = w - 24, ph = pw * 0.72f;
            var plot = new Rect(x, y, pw, ph);
            Rounded(plot, new Color(0.035f, 0.042f, 0.056f), 4);
            for (int i = 1; i < 4; i++)
            {
                Fill(new Rect(plot.x + plot.width * i / 4, plot.y + 1, 1, plot.height - 2), new Color(1, 1, 1, 0.04f));
                Fill(new Rect(plot.x + 1, plot.y + plot.height * i / 4, plot.width - 2, 1), new Color(1, 1, 1, 0.04f));
            }
            Outline(plot, Ui.Border, 4);
            float mx = plot.x + Mathf.Clamp01(m.pos[0] / m.max[0]) * plot.width;
            float my = plot.yMax - Mathf.Clamp01(m.pos[1] / m.max[1]) * plot.height;
            float dx = plot.x + Mathf.Clamp01(a11.dro[0] / m.max[0]) * plot.width;
            float dy = plot.yMax - Mathf.Clamp01(a11.dro[1] / m.max[1]) * plot.height;
            Fill(new Rect(mx - 0.5f, plot.y + 1, 1, plot.height - 2), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.35f));
            Fill(new Rect(plot.x + 1, my - 0.5f, plot.width - 2, 1), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.35f));
            if (a11.powered && (Mathf.Abs(dx - mx) > 1.5f || Mathf.Abs(dy - my) > 1.5f))
                Dot(new Rect(dx - 4, dy - 4, 8, 8), Ui.Warn, true);
            Dot(new Rect(mx - 6, my - 6, 12, 12), vfd.freq > 1 ? Pal.Phase : Ui.Accent, true);
            var zr = new Rect(plot.xMax + 8, y, 16, ph);
            Rounded(zr, new Color(0.035f, 0.042f, 0.056f), 4);
            Outline(zr, Ui.Border, 4);
            float zz = Mathf.Clamp01((m.pos[2] - m.min[2]) / (m.max[2] - m.min[2]));
            Rounded(new Rect(zr.x + 3, zr.yMax - zz * (zr.height - 6) - 6, zr.width - 6, 6), Pal.Mot, 2);
            GUI.Label(new Rect(plot.x + 4, plot.yMax - 15, 40, 14), "X →", sLabelDim);
            GUI.Label(new Rect(plot.x + 4, plot.y + 2, 40, 14), "Y ↑", sLabelDim);
            y += ph + 8;
            GUI.Label(new Rect(x, y, w, 34), posText, sMonoSmall);
        }

        GUIStyle sBtnStop;

        // Text that changes with the simulation: rebuilt at most once per frame, the LCD and position at 20 Hz
        // (a new rich-text string makes IMGUI lay the text out again), the renderer line four times a second.
        string lcdText = "", gradeText = "", posText = "", gpuText = "";
        int textFrame = -1;
        float textAt = -1;
        bool textPowered;
        float gpuTextAt = -1;
        int gradeFor = -1;
        string gpuLine;
        float smoothDt = 1 / 60f;

        void RefreshFrameText()
        {
            if (textFrame == Time.frameCount || (Time.unscaledTime - textAt < 0.05f && a11.powered == textPowered && a11.grade == gradeFor)) return;
            textFrame = Time.frameCount;
            textAt = Time.unscaledTime;
            textPowered = a11.powered;
            string status = a11.StatusLine();
            string sc = !a11.powered ? "#5d6b70" : a11.Alarm != null ? "#ff6a5f" : "#8ff5d6";
            lcdText =
                $"<color={sc}><b>{status}</b></color>\n" +
                (a11.powered
                    ? $"X {a11.dro[0],8:0.00}   Y {a11.dro[1],8:0.00}\nZ {a11.dro[2],8:0.00}   {(a11.homed ? "homed" : "<color=#f0b25a>not homed</color>")}\n" +
                      $"S{a11.grade} {(a11.spindleOn ? "<b>M3</b>" : "M5")}  {vfd.freq,5:0.0} Hz {vfd.Rpm,6:0} rpm\n" +
                      $"<color=#6fa89a>{Trim(a11.message, 32)}</color>"
                    : "\n\n\n");
            if (gradeFor != a11.grade)
            {
                gradeFor = a11.grade;
                gradeText = $"S{a11.grade} → Y4Y3Y2 {Vfd.Bits3(a11.grade - 1)} → P03{a11.grade - 1} {Vfd.Table[a11.grade - 1]:0.0} Hz";
            }
            string lost = "";
            for (int i = 0; i < 3; i++)
                if (a11.powered && Mathf.Abs(a11.dro[i] - m.pos[i]) > 0.5f) lost = $"\n<color={Hex(Ui.Warn)}>lost steps — DRO and machine differ</color>";
            posText = $"X {m.pos[0],7:0.0}   Y {m.pos[1],7:0.0}   Z {m.pos[2],6:0.0} mm{lost}";
            if (Time.unscaledTime - gpuTextAt > 0.25f)
            {
                gpuTextAt = Time.unscaledTime;
                gpuLine ??= $"{SystemInfo.graphicsDeviceName} · {SystemInfo.graphicsDeviceType}";
                float fps = 1f / Mathf.Max(1e-4f, smoothDt);
                gpuText = $"{gpuLine} · MSAA {QualitySettings.antiAliasing}× · {Screen.width}×{Screen.height} · {fps:0} fps";
            }
        }

        static readonly string[] DriveOk = { "   X drive", "   Y drive", "   Z drive" };
        static readonly string[] DriveFault = { "   X FAULT", "   Y FAULT", "   Z FAULT" };

        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        static readonly (Color c, string l)[] LegendItems =
        {
            (Pal.L, "L 230 V"), (Pal.N, "N"), (Pal.Phase, "VFD U/V/W"), (Pal.DCB, "DC bus / brake"), (Pal.P48, "+48 V"),
            (Pal.P24, "+24 V"), (Pal.Ret, "0 V"), (Pal.Sig, "signal active"), (Pal.P5, "+5 V"), (Pal.Mot, "stepper"),
            (Pal.PE, "PE"), (Pal.WireOff, "de-energized"),
        };

        void DrawTooltip()
        {
            string tip = view.panel != null && view.panel.hoverTip != null ? view.panel.hoverTip : view.tooltip;
            if (string.IsNullOrEmpty(tip)) return;
            Vector2 mp = view.simScreen.HasValue
                ? new Vector2(view.simScreen.Value.x, Screen.height - view.simScreen.Value.y) / scale
                : Event.current.mousePosition;
            float w = 400;
            if (!ReferenceEquals(tip, tipMeasured) && tip != tipMeasured)
            {
                tipMeasured = tip;
                tipContent.text = tip;
                tipHeight = sTip.CalcHeight(tipContent, w);
            }
            float h = tipHeight;
            float W = Screen.width / scale, H = Screen.height / scale;
            float x = mp.x + 18, y = mp.y + 18;
            if (x + w > W - 4) x = mp.x - w - 12;
            if (y + h > H - 4) y = mp.y - h - 12;
            x = Mathf.Clamp(x, 4, Mathf.Max(4, W - w - 4));
            y = Mathf.Clamp(y, 4, Mathf.Max(4, H - h - 4));
            GUI.Box(new Rect(x, y, w, h), tipContent, sTip);
        }

        string tipMeasured;
        readonly GUIContent tipContent = new();
        float tipHeight;
    }
}
