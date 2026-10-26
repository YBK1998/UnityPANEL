using UnityEngine;

namespace A11Sim
{
    /// HUD for the tools: multimeter, § 7.1 / § 7.2 procedures and the wire-by-wire build guide.
    public partial class Hud
    {
        Rect toolbarRect, procRect, guideRect, meterRect;
        string meterVal, meterUnit, meterText = "";
        bool meterLive, meterBeep;
        // Wrapped text is measured only when it changes (CalcHeight on rich text is the costly part of these panels).
        string procBody, procTodo, guideBody;
        float procBodyH, procTodoH, guideBodyH, measuredW;
        int procKey = -1, guideKey = -1;
        float toolsBottom = 28;
        bool procMenu;
        GUIStyle sMeterBig;

        bool OverTools(Vector2 g) =>
            toolbarRect.Contains(g) || procRect.Contains(g) || guideRect.Contains(g) || meterRect.Contains(g);

        Multimeter Meter => view.meter;
        Commissioning Proc => view.proc;
        BuildGuide Guide => view.guide;

        void ToolKeys()
        {
            if (view.meter == null) return;
            if (Input.GetKeyDown(KeyCode.T)) Meter.active = !Meter.active;
            if (Input.GetKeyDown(KeyCode.P)) ToggleProcedures();
            if (Input.GetKeyDown(KeyCode.B)) ToggleGuide();
            if (Guide.active && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) GuideStep(+1);
            if (Proc.Active && Proc.Current != null && Proc.Current.passed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Proc.Next();
        }

        void ToggleProcedures()
        {
            if (Proc.Active || procMenu)
            {
                Proc.Stop();
                procMenu = false;
                return;
            }
            Guide.active = false;
            procMenu = true;
        }

        void ToggleGuide()
        {
            Guide.active = !Guide.active;
            if (!Guide.active) return;
            Proc.Stop();
            procMenu = false;
            var p = view.panel;
            if (!p.full) p.SetFull(true);
            p.FocusInterior();
            FrameGuideWire();
        }

        void GuideStep(int d)
        {
            Guide.Go(Guide.index + d);
            FrameGuideWire();
        }

        void FrameGuideWire()
        {
            var it = Guide.Current;
            if (it == null || !view.panel.Cabinet.WireFocus(it.w, out var c, out float size)) return;
            // Both ends in view, and the wire below the guide panel (aim a little above its centre).
            float d = Mathf.Clamp(size * 2.3f, 0.55f, 2.4f);
            view.panel.FocusSmooth(c + new Vector3(0, 0.1f * d, 0), d);
        }

        // ------------------------------------------------------------------ layout

        void DrawTools(float W, float H)
        {
            if (view.meter == null) return;
            sMeterBig ??= new GUIStyle(sLcd) { fontSize = 32, alignment = TextAnchor.MiddleRight, padding = new RectOffset(12, 14, 2, 2) };
            float cx0 = left.xMax + 16, cx1 = right.xMin - 16, cw = cx1 - cx0;
            toolbarRect = Rect.zero;
            float top0 = bar.yMax + 14;
            toolsBottom = bar.yMax;

            float pw = Mathf.Min(560, cw);
            var top = new Rect(cx0 + cw / 2 - pw / 2, top0, pw, 0);
            procRect = guideRect = Rect.zero;
            if (procMenu && !Proc.Active) DrawProcMenu(top);
            else if (Proc.Active) DrawProc(top);
            else if (Guide.active) DrawGuide(top);
            toolsBottom = Mathf.Max(bar.yMax, procRect.yMax, guideRect.yMax);

            meterRect = Rect.zero;
            if (Meter.active)
            {
                float mh = 196, mw = Mathf.Min(340, cw);
                DrawMeter(new Rect(cx0 + cw / 2 - mw / 2, foot.y - 14 - mh, mw, mh));
            }
        }

        /// Card header: title, muted meta on the right, close button. Returns the y below the header.
        float CardHeader(Rect r, string title, string meta, out bool close)
        {
            GUI.Box(r, GUIContent.none, sPanel);
            float x = r.x + 16, y = r.y + 12;
            GUI.Label(new Rect(x, y, r.width - 80, 20), title, sHead);
            if (meta != null) GUI.Label(new Rect(x, y + 3, r.width - 72, 18), meta, sRightDim);
            close = Button(new Rect(r.xMax - 36, r.y + 9, 26, 24), "×", sBtnGhost);
            y += 26;
            HLine(r.x + 1, y, r.width - 2);
            return y + 10;
        }

        // ------------------------------------------------------------------ multimeter

        void DrawMeter(Rect r)
        {
            meterRect = r;
            float y = CardHeader(r, "Multimeter", null, out bool close);
            if (close) Meter.active = false;
            float x = r.x + 16, w = r.width - 32;
            // Segmented mode control.
            float mb = (w - 4) / 3;
            for (int i = 0; i < 3; i++)
            {
                var m = (MeterMode)i;
                if (Button(new Rect(x + i * (mb + 2), y, mb, 24), ModeLabels[i], Meter.mode == m ? sBtnSegOn : sBtnSeg)) Meter.mode = m;
            }
            y += 30;
            var rd = Meter.Read();
            if (rd.value != meterVal || rd.unit != meterUnit || rd.live != meterLive || rd.beep != meterBeep)
            {
                meterVal = rd.value;
                meterUnit = rd.unit;
                meterLive = rd.live;
                meterBeep = rd.beep;
                string val = rd.live ? $"<color=#ff5a4a>{rd.value}</color>" : rd.beep ? $"<color=#9dffb8>{rd.value}</color>" : rd.value;
                meterText = $"{val} <size=16>{rd.unit}</size>";
            }
            GUI.Box(new Rect(x, y, w, 50), meterText, sMeterBig);
            y += 54;
            GUI.Label(new Rect(x, y, w, 16), rd.note ?? "", sSmall);
            y += 20;
            ProbeLine(x, y, w - 120, new Color(1f, 0.25f, 0.2f), "Red", Meter.red, !Meter.nextBlack);
            if (Button(new Rect(x + w - 112, y - 2, 54, 22), "Swap", sBtnSeg)) Meter.Swap();
            if (Button(new Rect(x + w - 54, y - 2, 54, 22), "Clear", sBtnSeg)) Meter.Clear();
            y += 20;
            ProbeLine(x, y, w, new Color(0.85f, 0.88f, 0.95f), "Black", Meter.black, Meter.nextBlack);
        }

        static readonly string[] ModeLabels = { "V DC", "V AC", "Ω  beep" };

        void ProbeLine(float x, float y, float w, Color c, string name, Terminal t, bool next)
        {
            Dot(new Rect(x, y + 4, 9, 9), c, true);
            string where = t == null ? $"<color={Hex(Ui.Text3)}>click a terminal</color>" : $"<color={Hex(Ui.Text)}>{t.dev.tag} {t.name}</color>";
            GUI.Label(new Rect(x + 16, y, w - 16, 18), $"{name}  {where}{(next ? $"   <color={Hex(Ui.Accent)}>next click</color>" : "")}", sText);
        }

        // ------------------------------------------------------------------ procedures

        void DrawProcMenu(Rect r)
        {
            r.height = 138;
            procRect = r;
            float y = CardHeader(r, "Commissioning procedures", "manual § 7", out bool close);
            if (close) procMenu = false;
            float x = r.x + 16, w = r.width - 32;
            GUI.Label(new Rect(x, y, w, 18), "Step by step with the multimeter: each step names the probes, the range and the expected reading.", sSmall);
            y += 26;
            float b = (w - 8) / 2;
            for (int i = 0; i < Commissioning.Names.Length; i++)
                if (Button(new Rect(x + i * (b + 8), y, b, 34), Commissioning.Names[i], sBtn))
                {
                    procMenu = false;
                    Proc.Start(i);
                }
        }

        void DrawProc(Rect r)
        {
            float x = r.x + 16, w = r.width - 32;
            var s = Proc.Current;
            string body, todo;
            if (s == null)
            {
                body = "<b>All steps done.</b> Every measurement matched the manual.";
                todo = "<color=#7dffa0><b>COMPLETE</b></color>";
            }
            else
            {
                int key = Proc.proc * 1000 + Proc.index;
                if (key != procKey || procBody == null)
                {
                    procKey = key;
                    body = s.text;
                    if (s.IsMeasure)
                        body += $"\n<color=#8fa3bf>Red → {Proc_Name(s.red)} · black → {Proc_Name(s.black)} · meter {Multimeter.ModeName(s.mode)} · expect {s.expectText}</color>";
                }
                else body = procBody;
                todo = Proc.Todo();
            }
            if (body != procBody || w != measuredW)
            {
                procBody = body;
                procBodyH = sText.CalcHeight(new GUIContent(body), w);
            }
            if (todo != procTodo || w != measuredW)
            {
                procTodo = todo;
                procTodoH = sText.CalcHeight(new GUIContent(todo), w);
            }
            measuredW = w;
            float bh = procBodyH, th = procTodoH;
            r.height = 48 + bh + 10 + th + 18 + 14 + 30 + 16;
            procRect = r;
            string head = Commissioning.Names[Proc.proc];
            string pos = s == null ? "complete" : $"{s.title} · {Proc.index + 1} of {Proc.steps.Count}";
            float y = CardHeader(r, head, pos, out bool close);
            if (close) Proc.Stop();
            GUI.Label(new Rect(x, y, w, bh), body, sText);
            y += bh + 10;
            // Status line on a tinted strip: green when passed, amber while waiting.
            bool ok = s == null || s.passed;
            var strip = new Rect(x - 6, y - 4, w + 12, th + 10);
            Rounded(strip, ok ? new Color(Ui.Ok.r, Ui.Ok.g, Ui.Ok.b, 0.10f) : new Color(Ui.Warn.r, Ui.Warn.g, Ui.Warn.b, 0.08f), 4);
            Rounded(new Rect(strip.x, strip.y + 3, 3, strip.height - 6), ok ? Ui.Ok : Ui.Warn, 1.5f);
            GUI.Label(new Rect(x + 4, y, w - 4, th), todo, sText);
            y += th + 18;
            // Progress through the procedure.
            var bar2 = new Rect(x, y, w, 4);
            Rounded(bar2, Ui.Surface3, 2);
            Rounded(new Rect(bar2.x, bar2.y, Mathf.Max(4, bar2.width * Mathf.Clamp01((Proc.index + (s != null && s.passed ? 1 : 0)) / (float)Mathf.Max(1, Proc.steps.Count))), 4), Ui.Accent, 2);
            y += 14;
            float b = 90, bx = x;
            if (Button(new Rect(bx, y, b, 30), "‹  Back", sBtn)) Proc.Prev();
            bx += b + 6;
            if (s != null && s.IsMeasure)
            {
                if (Button(new Rect(bx, y, b, 30), "Show me", sBtn)) Proc.ShowMe();
                bx += b + 6;
            }
            if (s != null && s.lever)
                Hold(new Rect(bx, y, 140, 30), "Hold K1 test lever", HoldLever, sBtn, sBtnOn);
            if (s != null)
            {
                if (Button(new Rect(r.xMax - 16 - 110, y, 110, 30), s.passed ? "Next  ›" : "Skip  ›", s.passed ? sBtnPrimary : sBtnGhost)) Proc.Next();
            }
            else if (Button(new Rect(r.xMax - 16 - 110, y, 110, 30), "Restart", sBtnPrimary)) Proc.Start(Proc.proc);
        }

        string Proc_Name(string id)
        {
            var t = view.circuit.T(id);
            return $"{t.dev.tag} {t.name}";
        }

        // ------------------------------------------------------------------ build guide

        void DrawGuide(Rect r)
        {
            float x = r.x + 16, w = r.width - 32;
            var it = Guide.Current;
            string body = guideBody;
            int key = Guide.index * 4096 + (int)w;
            if (key == guideKey) { }
            else if (it == null) body = $"<b>All {Guide.items.Count} wires fitted.</b>\nNext: § 7.1 tests with no power (Procedures, P).";
            else
            {
                var wr = it.w;
                string num = wr.number != "" ? $"Wire {wr.number}" : wr.desc;
                body =
                    $"<size=16><color={Hex(Ui.Text)}><b>{num}</b></color></size>    {BuildGuide.Spec(wr)}    <color={Hex(Ui.Accent)}><b>cut {BuildGuide.Metres(BuildGuide.Cut(it.routed))}</b></color>" +
                    $"   <color={Hex(Ui.Text3)}>route {BuildGuide.Metres(it.routed)}</color>\n" +
                    (wr.number != "" ? $"<color={Hex(Ui.Text3)}>{wr.desc}</color>\n" : "") +
                    $"\n<color={Hex(Ui.Text3)}>FROM</color>   {BuildGuide.Where(wr.a)}\n           {Guide.Termination(wr.a, wr)}\n" +
                    $"<color={Hex(Ui.Text3)}>TO</color>       {BuildGuide.Where(wr.b)}\n           {Guide.Termination(wr.b, wr)}" +
                    (it.door ? $"\n\n<color={Hex(Ui.Warn)}>Door wire: run it through the service loop in the spiral wrap (≈ 0.5 m slack).</color>" : "");
            }
            if (key != guideKey)
            {
                guideKey = key;
                guideBody = body;
                guideBodyH = sText.CalcHeight(new GUIContent(body), w);
            }
            float bh = guideBodyH;
            r.height = 48 + bh + 14 + 22 + 30 + 16;
            guideRect = r;
            string grp = it != null ? BuildGuide.Groups[it.group] : "complete";
            float y = CardHeader(r, "Build guide", $"{grp} · {Mathf.Min(Guide.index + 1, Guide.items.Count)} of {Guide.items.Count}", out bool close);
            if (close) Guide.active = false;
            GUI.Label(new Rect(x, y, w, bh), body, sText);
            y += bh + 14;

            // Progress bar with group ticks.
            var bar2 = new Rect(x, y, w, 6);
            Rounded(bar2, Ui.Surface3, 3);
            Rounded(new Rect(bar2.x, bar2.y, Mathf.Max(6, bar2.width * Guide.index / Mathf.Max(1, Guide.items.Count)), bar2.height), Ui.Accent, 3);
            for (int g = 1; g < BuildGuide.Groups.Length; g++)
            {
                int gi = Guide.GroupStart(g);
                if (gi > 0) Fill(new Rect(bar2.x + bar2.width * gi / Guide.items.Count - 1, bar2.y, 2, bar2.height), Ui.Surface);
            }
            y += 22;

            float b = 90;
            if (Button(new Rect(x, y, b, 30), "‹  Back", sBtn)) GuideStep(-1);
            if (Button(new Rect(x + b + 6, y, b, 30), "Frame", sBtn)) FrameGuideWire();
            if (Button(new Rect(x + 2 * (b + 6), y, b, 30), "Restart", sBtnGhost))
            {
                Guide.Restart();
                FrameGuideWire();
            }
            if (it != null && Button(new Rect(r.xMax - 16 - 150, y, 150, 30), "Fitted — next  ›", sBtnPrimary)) GuideStep(+1);
        }
    }
}
