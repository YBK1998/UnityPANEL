using UnityEngine;

namespace A11Sim
{
    /// Live status pills in the app bar, and the keyboard / mouse help card (F1, shown once on first start).
    public partial class Hud
    {
        bool helpOpen;
        bool helpChecked;
        Rect helpRect;
        readonly System.Collections.Generic.Dictionary<string, float> chipW = new();
        readonly GUIContent chipContent = new();
        readonly (string t, Color c, bool on, bool alarm)[] items = new (string, Color, bool, bool)[7];
        readonly bool[] shown = new bool[7];
        static readonly int[] DropOrder = { 2, 1, 4, 5 }; // K1, 24 V, VFD, drives go first when the bar is narrow
        int vfdHz = -1;
        string vfdText = "";

        const string HelpSeenKey = "a11.help.seen";

        void HelpKeys()
        {
            if (!helpChecked)
            {
                helpChecked = true;
                try { helpOpen = PlayerPrefs.GetInt(HelpSeenKey, 0) == 0 && !Application.isBatchMode && !HasTestArgs(); }
                catch { helpOpen = false; }
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-a11showhelp") >= 0) helpOpen = true;
            }
            if (Input.GetKeyDown(KeyCode.F1)) SetHelp(!helpOpen);
            if (helpOpen && Input.GetKeyDown(KeyCode.Escape)) SetHelp(false);
        }

        static bool HasTestArgs()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("-a11")) return true;
            return false;
        }

        void SetHelp(bool on)
        {
            helpOpen = on;
            if (!on)
            {
                PlayerPrefs.SetInt(HelpSeenKey, 1);
                PlayerPrefs.Save();
            }
        }

        float ChipWidth(string text)
        {
            if (!chipW.TryGetValue(text, out float w))
            {
                chipContent.text = text;
                chipW[text] = w = sPill.CalcSize(chipContent).x;
            }
            return w;
        }

        // ------------------------------------------------------------------ status pills

        void DrawStatusPills(float xMin, float xMax)
        {
            var c = view.circuit;
            var k1 = c.Get<RelayK1>("K1");
            var km1 = c.Get<Contactor>("KM1");
            bool drives = drv[0].powered && drv[1].powered && drv[2].powered;
            bool fault = drv[0].fault || drv[1].fault || drv[2].fault;
            string alarm = a11.powered ? a11.Alarm : null;
            int hz = Mathf.RoundToInt(vfd.freq);
            if (hz != vfdHz)
            {
                vfdHz = hz;
                vfdText = $"VFD {hz} Hz";
            }
            items[0] = ("Mains", Pal.L, qf0.closed, false);
            items[1] = ("24 V", Pal.P24, c.Has(c.T("G2.+V1"), Pot.P24), false);
            items[2] = ("K1", Ui.Ok, k1.on, false);
            items[3] = (km1.on ? "KM1 · bus live" : "KM1", Pal.L, km1.on, false);
            items[4] = (vfd.freq > 0.5f ? vfdText : vfd.powered ? "VFD ready" : "VFD", Pal.Phase, vfd.powered, false);
            items[5] = (fault ? "Drive fault" : "Drives 48 V", Pal.P48, drives, fault);
            items[6] = (s0.pressed ? "E-STOP" : alarm != null ? "Alarm" : "No alarm", Ui.Ok, true, s0.pressed || alarm != null);
            float total = 0;
            for (int i = 0; i < items.Length; i++)
            {
                shown[i] = true;
                total += ChipWidth(items[i].t) + 6;
            }
            foreach (int i in DropOrder)
            {
                if (total <= xMax - xMin) break;
                shown[i] = false;
                total -= ChipWidth(items[i].t) + 6;
            }
            float x = Mathf.Max(xMin, (xMin + xMax) / 2 - total / 2);
            for (int i = 0; i < items.Length; i++)
                if (shown[i]) Pill(ref x, 12, items[i].t, items[i].c, items[i].on, items[i].alarm);
        }

        // ------------------------------------------------------------------ help card

        static readonly (string key, string what)[] HelpSchematic =
        {
            ("Wheel", "zoom towards the cursor"), ("Drag  WASD", "pan"), ("F", "fit the whole circuit"),
            ("Click", "breakers, fuses, E-stop, sensors, drives"), ("1  0", "hold ON / OFF push-buttons"),
            ("Q  E", "main CB QF0 · E-stop"), ("Space  H  Esc", "spindle · home all · stop"), ("Hover", "wire data, live state, cautions"),
        };

        static readonly (string key, string what)[] Help3D =
        {
            ("V", "3D enclosure ↔ schematic"), ("Drag", "orbit"), ("Middle  Wheel", "pan · zoom"),
            ("O  I", "open / close the door · look inside"), ("C  M", "duct covers · dimensions"), ("F  G", "door · handheld"),
        };

        static readonly (string key, string what)[] HelpTools =
        {
            ("T", "multimeter — click terminals for the probes"), ("P", "§ 7.1 / § 7.2 procedures"),
            ("B", "build guide, wire by wire"), ("Enter", "next step"), ("Tab  F11", "hide panels · full screen"), ("Ctrl Q", "quit"),
        };

        void DrawHelp(float W, float H)
        {
            if (!helpOpen)
            {
                helpRect = Rect.zero;
                return;
            }
            float w = Mathf.Min(860, W - 40), h = 490;
            helpRect = new Rect(W / 2 - w / 2, H / 2 - h / 2, w, h);
            Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.018f, 0.62f));
            GUI.Box(helpRect, GUIContent.none, sPanel);
            float x = helpRect.x + 24, y = helpRect.y + 18;
            GUI.Label(new Rect(x, y, w - 48, 22), "A11 Control Box — circuit simulator", sTitle);
            GUI.Label(new Rect(x, y + 22, w - 48, 18),
                "Start: switch QF0 on (Q), then hold ON (1). K1 latches, KM1 closes and the bus lights up. Glowing wires are energized.", sSub);
            y += 52;
            HLine(x, y - 8, w - 48);
            float colW = (w - 72) / 2;
            Column(x, y, colW, "SCHEMATIC", HelpSchematic);
            float yr = Column(x + colW + 24, y, colW, "3D ENCLOSURE", Help3D);
            Column(x + colW + 24, yr + 10, colW, "TOOLS", HelpTools);
            if (Button(new Rect(helpRect.xMax - 132, helpRect.yMax - 46, 108, 30), "Got it", sBtnPrimary)) SetHelp(false);
            // A click outside the card closes it and does not reach the circuit behind.
            if (Event.current.type == EventType.MouseDown && !helpRect.Contains(Event.current.mousePosition)) SetHelp(false);
        }

        float Column(float x, float y, float w, string title, (string key, string what)[] rows)
        {
            GUI.Label(new Rect(x, y, w, 16), title, sSection);
            y += 22;
            foreach (var (k, what) in rows)
            {
                float kx = x;
                foreach (var part in k.Split("  "))
                    kx = KeyCap(kx, y, part);
                GUI.Label(new Rect(x + 128, y + 1, w - 128, 18), what, sLabel);
                y += 24;
            }
            return y;
        }
    }
}
