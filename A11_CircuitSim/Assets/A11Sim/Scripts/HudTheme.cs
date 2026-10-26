using UnityEngine;

namespace A11Sim
{
    /// Design system for the HUD: one neutral slate theme, a single blue accent, and green / amber / red used only for
    /// state (on, forced / warning, fault / E-stop). Widgets: cards, section headers, switches, LEDs, pills, key caps.
    public partial class Hud
    {
        static class Ui
        {
            public static readonly Color Surface = new(0.067f, 0.078f, 0.102f, 0.97f);
            public static readonly Color Surface2 = new(0.098f, 0.114f, 0.145f, 1f);
            public static readonly Color Surface3 = new(0.135f, 0.155f, 0.195f, 1f);
            public static readonly Color Border = new(0.17f, 0.195f, 0.24f, 1f);
            public static readonly Color BorderHi = new(0.26f, 0.30f, 0.37f, 1f);
            public static readonly Color Text = new(0.90f, 0.92f, 0.95f, 1f);
            public static readonly Color Text2 = new(0.63f, 0.68f, 0.75f, 1f);
            public static readonly Color Text3 = new(0.44f, 0.49f, 0.57f, 1f);
            public static readonly Color Accent = new(0.31f, 0.58f, 1.00f, 1f);
            public static readonly Color AccentDim = new(0.11f, 0.20f, 0.36f, 1f);
            public static readonly Color Ok = new(0.22f, 0.80f, 0.45f, 1f);
            public static readonly Color Warn = new(0.98f, 0.64f, 0.16f, 1f);
            public static readonly Color Danger = new(0.92f, 0.27f, 0.25f, 1f);
            public const float BarH = 46, FootH = 26, SideW = 264, Pad = 14, Row = 26;
        }

        GUIStyle sTitle, sSub, sLabel, sLabelDim, sMono, sMonoSmall, sKey, sBtnPrimary, sBtnGhost, sBtnSeg, sBtnSegOn, sPill, sCard;
        Texture2D texCard;
        Font uiSemi, monoFont;

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        GUIStyle Flat(Color fill, Color border, Color text, Color hoverFill, Color hoverBorder, int radius = 5, int size = 12, Font font = null)
        {
            var s = new GUIStyle
            {
                font = font ?? uiSemi,
                fontStyle = (font ?? uiSemi) == uiSemi ? FontStyle.Bold : FontStyle.Normal,
                fontSize = size,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
                padding = new RectOffset(8, 8, 3, 3),
                richText = true,
                clipping = TextClipping.Clip,
            };
            s.normal.background = RoundTex(fill, border, 16, radius);
            s.hover.background = RoundTex(hoverFill, hoverBorder, 16, radius);
            s.active.background = RoundTex(hoverFill * 0.85f + new Color(0, 0, 0, 0.15f), Ui.Accent, 16, radius);
            s.normal.textColor = s.hover.textColor = s.active.textColor = text;
            return s;
        }

        void InitTheme()
        {
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 12);
            // Headings use the bold weight of the same family ("Segoe UI Semibold" as an OS font name renders stale text in IMGUI).
            uiSemi = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 12);
            // Static fonts only: variable OS fonts (Cascadia, Segoe UI Variable) render stale text through IMGUI's text cache.
            monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, 13);
            texWhite = Tex(Color.white);
            texCircle = new Texture2D(32, 32) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16));
                texCircle.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(15f - d)));
            }
            texCircle.Apply();

            GUIStyle Text(Font f, int size, Color c, FontStyle st = FontStyle.Normal, bool wrap = false) =>
                new() { font = f, fontSize = size, fontStyle = f == uiSemi ? FontStyle.Bold : st, richText = true, wordWrap = wrap, normal = { textColor = c } };

            sPanel = new GUIStyle { border = new RectOffset(7, 7, 7, 7), padding = new RectOffset(12, 12, 10, 10) };
            sPanel.normal.background = RoundTex(Ui.Surface, Ui.Border, 16, 6);
            sCard = new GUIStyle { border = new RectOffset(7, 7, 7, 7) };
            sCard.normal.background = RoundTex(Ui.Surface2, Ui.Border, 16, 6);
            texCard = sCard.normal.background;

            sTitle = Text(uiSemi, 15, Ui.Text);
            sSub = Text(uiFont, 11, Ui.Text3);
            sHead = Text(uiSemi, 14, Ui.Text);
            sSection = Text(uiSemi, 10, Ui.Text3);
            sText = Text(uiFont, 12, Ui.Text2, FontStyle.Normal, true);
            sLabel = Text(uiFont, 12, Ui.Text);
            sLabelDim = Text(uiFont, 11, Ui.Text3);
            sSmall = Text(uiFont, 11, Ui.Text3, FontStyle.Normal, true);
            sSmallCenterTop = new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter };
            sSmallCenter = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            sMono = Text(monoFont, 13, Ui.Text);
            sMonoSmall = Text(monoFont, 11, Ui.Text2);
            sKey = new GUIStyle(Text(monoFont, 11, Ui.Text)) { alignment = TextAnchor.MiddleCenter, border = new RectOffset(5, 5, 5, 5), padding = new RectOffset(6, 6, 1, 2) };
            sKey.normal.background = RoundTex(Ui.Surface3, Ui.BorderHi, 16, 4);
            sPill = new GUIStyle(Text(uiSemi, 11, Ui.Text)) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(20, 10, 0, 0) };

            sBtn = Flat(Ui.Surface2, Ui.Border, Ui.Text, Ui.Surface3, Ui.BorderHi);
            sBtnOn = Flat(Ui.AccentDim, Ui.Accent, Color.white, new Color(0.14f, 0.25f, 0.44f), Ui.Accent);
            sBtnPrimary = Flat(Ui.Accent, Ui.Accent, Color.white, Ui.Accent * 1.12f, Ui.Accent * 1.12f);
            sBtnGhost = Flat(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), Ui.Text2, Ui.Surface3, Ui.Border);
            sBtnWarn = Flat(new Color(0.22f, 0.15f, 0.05f), Ui.Warn, Ui.Warn, new Color(0.28f, 0.19f, 0.06f), Ui.Warn);
            sBtnRed = Flat(Ui.Danger, new Color(1f, 0.45f, 0.42f), Color.white, Ui.Danger * 1.1f, Color.white * 0.9f, 6, 14);
            sBtnRedOn = Flat(new Color(0.28f, 0.05f, 0.05f), Ui.Danger, new Color(1f, 0.62f, 0.58f), new Color(0.34f, 0.07f, 0.07f), Ui.Danger, 6, 13);
            sBtnSeg = Flat(Ui.Surface2, Ui.Border, Ui.Text2, Ui.Surface3, Ui.BorderHi, 4, 11);
            sBtnSegOn = Flat(Ui.AccentDim, Ui.Accent, Color.white, Ui.AccentDim, Ui.Accent, 4, 11);

            sLcd = new GUIStyle { font = monoFont, fontSize = 13, richText = true, padding = new RectOffset(10, 10, 8, 8), border = new RectOffset(6, 6, 6, 6) };
            sLcd.normal.background = RoundTex(new Color(0.03f, 0.075f, 0.09f), new Color(0.12f, 0.24f, 0.28f), 16, 5);
            sLcd.normal.textColor = new Color(0.55f, 0.95f, 0.85f);
            sTip = new GUIStyle(Text(uiFont, 12, Ui.Text, FontStyle.Normal, true)) { padding = new RectOffset(12, 12, 9, 10), border = new RectOffset(7, 7, 7, 7) };
            sTip.normal.background = RoundTex(new Color(0.085f, 0.1f, 0.13f, 0.98f), Ui.BorderHi, 16, 6);
            sLegend = Text(uiFont, 11, Ui.Text2);
            sLegend.alignment = TextAnchor.MiddleLeft;
            legendW = new float[LegendItems.Length];
            for (int i = 0; i < LegendItems.Length; i++) legendW[i] = sLegend.CalcSize(new GUIContent(LegendItems[i].l)).x;
        }

        // ------------------------------------------------------------------ primitives

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, texWhite);
            GUI.color = old;
        }

        // Rounded fills and outlines drawn by IMGUI itself (crisp at any size and UI scale).
        void Rounded(Rect r, Color c, float radius = 5) =>
            GUI.DrawTexture(r, texWhite, ScaleMode.StretchToFill, true, 0, c, 0, radius);

        void Outline(Rect r, Color c, float radius = 5) =>
            GUI.DrawTexture(r, texWhite, ScaleMode.StretchToFill, true, 0, c, 1, radius);

        void HLine(float x, float y, float w) => Fill(new Rect(x, y, w, 1), Ui.Border);

        /// Section header: small caps-style label, optional muted note on the right, hairline below.
        void Section(float x, ref float y, float w, string title, string note = null)
        {
            GUI.Label(new Rect(x, y, w, 16), title, sSection);
            if (note != null) GUI.Label(new Rect(x, y, w, 16), note, sRightDim);
            y += 17;
            HLine(x, y, w);
            y += 7;
        }

        GUIStyle sRightDim;

        void Led(Rect r, Color c, bool on) => Dot(r, c, on);

        /// Switch: a pill track with a knob; returns true when clicked.
        bool Switch(Rect r, bool on, Color onColor)
        {
            var track = new Rect(r.xMax - 32, r.y + (r.height - 16) / 2, 32, 16);
            Rounded(track, on ? onColor : Ui.Surface3, 8);
            if (!on) Outline(track, Ui.BorderHi, 8);
            Rounded(new Rect(on ? track.xMax - 14 : track.x + 2, track.y + 2, 12, 12), on ? Color.white : new Color(0.72f, 0.75f, 0.8f), 6);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// A full-width row: LED, tag, description, switch on the right. Returns true when clicked.
        bool SwitchRow(float x, ref float y, float w, string tag, string desc, bool on, Color onColor, Color ledColor)
        {
            var row = new Rect(x - 6, y, w + 12, Ui.Row);
            if (row.Contains(Event.current.mousePosition)) Rounded(row, new Color(1, 1, 1, 0.04f), 4);
            Led(new Rect(x, y + 9, 8, 8), ledColor, on);
            if (tag.Length > 0)
            {
                GUI.Label(new Rect(x + 16, y + 4, 44, 18), tag, sTagStyle);
                GUI.Label(new Rect(x + 60, y + 4, w - 100, 18), desc, sLabelDim);
            }
            else GUI.Label(new Rect(x + 16, y + 4, w - 56, 18), desc, sLabel);
            bool clicked = Switch(new Rect(x, y, w, Ui.Row), on, onColor);
            y += Ui.Row;
            return clicked;
        }

        GUIStyle sTagStyle, sBtnSm, sBtnSmOn, sBtnGhostSm;

        /// Status pill for the app bar: LED + label; red fill for an alarm.
        void Pill(ref float x, float y, string text, Color c, bool on, bool alarm = false)
        {
            float w = ChipWidth(text);
            var r = new Rect(x, y, w, 22);
            Rounded(r, alarm ? Ui.Danger : Ui.Surface2, 11);
            if (!alarm) Outline(r, Ui.Border, 11);
            Led(new Rect(r.x + 8, r.y + 7, 8, 8), alarm ? Color.white : c, on || alarm);
            sPill.normal.textColor = alarm ? Color.white : on ? Ui.Text : Ui.Text3;
            chipContent.text = text;
            GUI.Label(r, chipContent, sPill);
            x += w + 6;
        }

        /// Key cap (help card, hints).
        float KeyCap(float x, float y, string key)
        {
            chipContent.text = key;
            float w = Mathf.Max(22, sKey.CalcSize(chipContent).x);
            GUI.Box(new Rect(x, y, w, 19), chipContent, sKey);
            return x + w + 4;
        }

        void InitThemeExtras()
        {
            sRightDim = new GUIStyle(sLabelDim) { alignment = TextAnchor.UpperRight, fontSize = 10 };
            sTagStyle = new GUIStyle(sLabel) { font = uiSemi, fontStyle = FontStyle.Bold };
            sMarkStyle = new GUIStyle { font = uiSemi, fontStyle = FontStyle.Bold, fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            sBtnLeft = new GUIStyle(sBtn) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 24, 3, 3), font = uiFont, fontStyle = FontStyle.Normal };
            sBtnSm = new GUIStyle(sBtn) { fontSize = 11, padding = new RectOffset(2, 2, 2, 2) };
            sBtnSmOn = new GUIStyle(sBtnOn) { fontSize = 11, padding = new RectOffset(2, 2, 2, 2) };
            sBtnGhostSm = new GUIStyle(sBtnGhost) { fontSize = 11, padding = new RectOffset(2, 2, 2, 2) };
            sBtnStop = Flat(new Color(0.20f, 0.07f, 0.07f), Ui.Danger, new Color(1f, 0.66f, 0.62f), new Color(0.28f, 0.09f, 0.09f), Ui.Danger);
        }
    }
}
