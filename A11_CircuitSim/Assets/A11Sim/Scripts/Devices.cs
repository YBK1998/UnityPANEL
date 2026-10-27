using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace A11Sim
{
    using TA = TextAlignmentOptions;

    public class MainsSupply : Device
    {
        Terminal l, n, pe;

        public MainsSupply()
        {
            tag = "Mains";
            title = "Mains 230 V AC (gland)";
            info = "Supply L / N / PE through the cable gland. Wires 101, 102, 140.";
        }

        public override void Build()
        {
            l = Term("L", 10, 296);
            n = Term("N", 14, 296);
            pe = Term("PE", 18, 296);
        }

        public override void Sources()
        {
            C.Source(l, Pot.L);
            C.Source(n, Pot.N);
            C.Source(pe, Pot.PE);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            lb.Add("L", 10, 297.6f, 0.9f, Pal.Text);
            lb.Add("N", 14, 297.6f, 0.9f, Pal.Text);
            lb.Add("PE", 19.2f, 297.6f, 0.9f, Pal.Text, TA.Left);
            lb.Add("Mains 230 V (gland)", 23.5f, 295.6f, 1.1f, Pal.Title, TA.Left, 0, true);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(8.5f, 294.5f, 19.5f, 298.5f)); }
        public override string Status() => "always live";
    }

    public class Breaker : Device
    {
        public bool closed;
        readonly float[] xs;
        readonly float yT, yB;
        readonly Vector2 labelPos;
        readonly TA align;
        readonly string rating;
        Terminal[] tin, tout;

        public Breaker(string tag, string rating, string info, float yT, float yB, bool closed, Vector2 labelPos, TA align, params float[] xs)
        {
            this.tag = tag;
            this.rating = rating;
            this.info = info;
            title = $"{tag} — circuit breaker {rating}";
            this.yT = yT;
            this.yB = yB;
            this.closed = closed;
            this.labelPos = labelPos;
            this.align = align;
            this.xs = xs;
        }

        public override void Build()
        {
            tin = new Terminal[xs.Length];
            tout = new Terminal[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                tin[i] = Term((2 * i + 1).ToString(), xs[i], yT);
                tout[i] = Term((2 * i + 2).ToString(), xs[i], yB);
            }
        }

        public override void Links()
        {
            if (!closed) return;
            for (int i = 0; i < xs.Length; i++) C.Link(tin[i], tout[i]);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            lb.Add(tag, labelPos.x, labelPos.y + 0.75f, 1.05f, Pal.Title, align, 0, true);
            lb.Add(rating, labelPos.x, labelPos.y - 0.75f, 0.85f, Pal.TextDim, align);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            Vector2 prev = default;
            for (int i = 0; i < xs.Length; i++)
            {
                var m = Sym.Contact(g, tin[i].pos, tout[i].pos, closed, ContactStyle.Breaker);
                if (i > 0) g.Dashed(prev, m, 0.35f, 0.25f);
                prev = m;
            }
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(xs[0] - 2.2f, yB, xs[^1] + 2.2f, yT), () => closed = !closed);
        }

        public override string Status() => closed ? "ON (closed)" : "OFF (open) — click to switch";
    }

    public class Fuse : Device
    {
        public bool inserted = true;
        readonly float x, yT, yB;
        readonly Vector2 labelPos;
        Terminal tin, tout;

        public Fuse(string tag, string info, float x, float yT, float yB, Vector2 labelPos)
        {
            this.tag = tag;
            this.info = info;
            title = $"{tag} — fuse 10×38 gG 2 A (RT18-32 holder)";
            this.x = x;
            this.yT = yT;
            this.yB = yB;
            this.labelPos = labelPos;
        }

        public override void Build()
        {
            tin = Term("in", x, yT);
            tout = Term("out", x, yB);
        }

        public override void Links()
        {
            if (inserted) C.Link(tin, tout);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            lb.Add(tag + " 2 A", labelPos.x, labelPos.y, 0.95f, Pal.Title, TA.Left, 0, true);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            float cy = (yT + yB) / 2;
            var min = new Vector2(x - 0.75f, cy - 2.2f);
            var max = new Vector2(x + 0.75f, cy + 2.2f);
            g.Line(new Vector2(x, yT - Sym.TermR), new Vector2(x, max.y));
            g.Line(new Vector2(x, yB + Sym.TermR), new Vector2(x, min.y));
            if (inserted)
            {
                g.Rect(min, max);
                g.Line(new Vector2(x, min.y), new Vector2(x, max.y));
            }
            else
            {
                var c = g.color;
                g.color = Pal.Caution;
                g.DashedRect(min, max, 0.4f, 0.3f);
                g.color = c;
            }
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(x - 2, yB, x + 2, yT), () => inserted = !inserted);
        }

        public override string Status() => inserted ? "fuse in place" : "FUSE REMOVED — click to insert";
    }

    public class EmiFilter : Device
    {
        public EmiFilter()
        {
            tag = "Z1";
            title = "Z1 — EMI filter HD2AC-20A";
            info = "Mains filter, 20 A — fixed (no higher-current version available). Rating is less than the C25 main CB and the VFD full-load input (C14): cap the spindle at ≈ 1.5 kW.";
        }

        public override void Build()
        {
            var lin = Term("LIN", 10, 270);
            var nin = Term("NIN", 14, 270);
            var lout = Term("LOUT", 10, 262);
            var nout = Term("NOUT", 14, 262);
            Term("GND", 30, 266);
            C.Fixed(lin, lout);
            C.Fixed(nin, nout);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.Box;
            g.Rect(new Vector2(6, 262), new Vector2(30, 270));
            g.color = Pal.Sym;
            lb.Add("Z1  EMI filter", 22, 267.3f, 1.0f, Pal.Title, TA.Center, 0, true);
            lb.Add("HD2AC-20A", 22, 264.7f, 0.8f, Pal.TextDim);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(6, 262, 30, 270)); }
    }

    /// Terminal block or bus anchor.
    public class NodeT : Device
    {
        readonly Vector2 p;
        readonly bool hidden;
        readonly string label;
        readonly Vector2 labelPos;
        readonly TA align;

        public NodeT(string tag, string title, string info, float x, float y, bool hidden, string label, Vector2 labelPos, TA align)
        {
            this.tag = tag;
            this.title = title;
            this.info = info;
            p = new Vector2(x, y);
            this.hidden = hidden;
            this.label = label;
            this.labelPos = labelPos;
            this.align = align;
        }

        public override void Build() => Term("T", p.x, p.y, hidden);

        public override void DrawStatic(Gfx g, Labels lb)
        {
            if (!string.IsNullOrEmpty(label)) lb.Add(label, labelPos.x, labelPos.y, 0.9f, Pal.Title, align, 0, true);
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            if (!hidden) yield return Spot(new Rect(p.x - 1, p.y - 1, 2, 2));
        }
    }

    public class Contactor : Device
    {
        public bool on;
        Terminal l1, t1, l2, t2, a1, a2, a43, a44;

        public Contactor()
        {
            tag = "KM1";
            title = "KM1 — contactor Hyundai UMC65 22NS (220 VAC coil)";
            info = "Main power contactor. Rev 0.7: switches L only (1L1-2T1 → L distribution block XL); pole 3L2-4T2 is not used,\n" +
                   "N goes straight to the N distribution block XN. Coil A2 ← K1 pole 4 (131), A1 ← XN (132).\nAux 43-44 NO → yellow lamp. 13-14 is not used (K1 does the latch).";
        }

        public override void Build()
        {
            l1 = Term("1L1", 96, 246);
            t1 = Term("2T1", 96, 238);
            l2 = Term("3L2", 100, 246);
            t2 = Term("4T2", 100, 238);
            a2 = Term("A2", 60, 232);
            a1 = Term("A1", 60, 224);
            a43 = Term("43", 60, 124);
            a44 = Term("44", 60, 116);
        }

        public override bool Evaluate()
        {
            bool e = C.Has(a2, Pot.L) && C.Has(a1, Pot.N);
            bool ch = e != on;
            on = e;
            return ch;
        }

        public override void Links()
        {
            if (!on) return;
            C.Link(l1, t1);
            C.Link(l2, t2);
            C.Link(a43, a44);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            Sym.CoilV(g, a2.pos, a1.pos, 3.2f);
            lb.Add("A2", 61.0f, 231.1f, 0.65f, Pal.TextDim, TA.Left);
            lb.Add("A1", 61.0f, 224.9f, 0.65f, Pal.TextDim, TA.Left);
            lb.Add("KM1 coil", 57.6f, 228f, 0.9f, Pal.Title, TA.Right, 0, true);
            lb.Add("KM1", 102.2f, 243.4f, 1.05f, Pal.Title, TA.Left, 0, true);
            lb.Add("UMC65 22NS", 102.2f, 241.6f, 0.8f, Pal.TextDim, TA.Left);
            lb.Add("1L1", 95.2f, 247.2f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("3L2", 100.8f, 247.2f, 0.6f, Pal.TextDim, TA.Left);
            lb.Add("2T1", 95.2f, 236.8f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("4T2", 100.8f, 236.8f, 0.6f, Pal.TextDim, TA.Left);
            lb.Add("43", 59.2f, 124.6f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("44", 59.2f, 115.4f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("KM1 aux", 63.2f, 120.8f, 0.9f, Pal.Title, TA.Left, 0, true);
            lb.Add("43-44 NO", 63.2f, 119.0f, 0.75f, Pal.TextDim, TA.Left);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            var m1 = Sym.Contact(g, l1.pos, t1.pos, on);
            var m2 = Sym.Contact(g, l2.pos, t2.pos, on);
            g.Dashed(m1, m2, 0.35f, 0.25f);
            Sym.Contact(g, a43.pos, a44.pos, on);
            if (on)
            {
                glow.Line(new Vector2(60, 226.6f), new Vector2(60, 229.4f), 2.6f, Pal.L);
                g.Line(new Vector2(58.8f, 228), new Vector2(61.2f, 228), 0.5f, Pal.L);
            }
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(94, 238, 102, 246));
            yield return Spot(Rect.MinMaxRect(57.5f, 225, 62.5f, 231));
            yield return Spot(Rect.MinMaxRect(58, 116, 62, 124));
        }

        public override string Status() => on ? "ENERGIZED — L/N bus is live" : "de-energized — L/N bus is dead";
    }

    public class RelayK1 : Device
    {
        public bool on;
        public bool testLever; // § 7.1 tests 9 / 10: closes all poles by hand, coil not energized
        Terminal a1, a2, a2b, c11, c14, c41, c44;
        bool Closed => on || testLever;

        public RelayK1()
        {
            tag = "K1";
            title = "K1 — relay 24 VDC, 4 CO (Finder 55.34.9.024.0000 on an RXZE2S114M-type socket (look-alike, exact part not confirmed), no coil suppression)";
            info = "Pole 1 (11-14): self-latch (24 V). Pole 2: spare (24 V use only).\n" +
                   "Pole 3: empty — the gap between 24 V and 230 V (the 55.34 has basic insulation only). Pole 4 (41-44): 230 V to the KM1 coil.\n" +
                   "No diode fitted: the coil gives a voltage spike when OFF or the E-stop opens the chain. If the socket has a module slot and is a\n" +
                   "genuine RXZE2S114M, a Schneider RXM040W diode module plugs in (C11). Limits assumed from the Schneider datasheet: 10 A / 250 V,\n" +
                   "2 conductors per screw only up to 1 mm² each.\n" +
                   "Socket numbers (IEC = NEMA): A1 = 13, A2 = 14 (two screws, for bus jumpers) · COM 11/21/31/41 = 9/10/11/12 · NC 12/22/32/42 = 1/2/3/4 · NO 14/24/34/44 = 5/6/7/8.\n" +
                   "Rows, left to right: coil A2 A2 A1 · NC 42 32 22 12 · NO 44 34 24 14 · COM 41 31 21 11 (pole 4 on the left, pole 1 on the right).\n" +
                   "Read the TOP label of each screw (IEC, arrow up): IEC 14 (pole 1 NO) has NEMA 5 below it — NEMA 14 is the coil A2.\n" +
                   "After a mains failure K1 stays OFF — the machine does not restart by itself.";
        }

        public override void Build()
        {
            a1 = Term("A1", 36, 82);
            a2 = Term("A2", 36, 74);
            a2b = Term("A2b", 36, 70); // the socket has two A2 screws (both NEMA 14), joined inside
            C.Fixed(a2, a2b);
            c11 = Term("11", 46, 98);
            c14 = Term("14", 46, 90);
            c41 = Term("41", 60, 246);
            c44 = Term("44", 60, 238);
        }

        public override bool Evaluate()
        {
            bool e = C.Has(a1, Pot.P24) && C.Has(a2, Pot.Z0);
            bool ch = e != on;
            on = e;
            return ch;
        }

        public override void Links()
        {
            if (!Closed) return;
            C.Link(c11, c14);
            C.Link(c41, c44);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            Sym.CoilV(g, a1.pos, a2.pos, 3.2f); // no suppression module fitted (relay + socket only)
            lb.Add("A1 (13)", 34.8f, 81.4f, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("A2 (14)", 34.8f, 74.6f, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("A2 (14) 2nd screw", 34.8f, 70.6f, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("K1", 33.2f, 78f, 1.05f, Pal.Title, TA.Right, 0, true);
            lb.Add("24 VDC · Finder 55.34", 37.5f, 68.0f, 0.75f, Pal.TextDim, TA.Left);
            lb.Add("no diode", 38.0f, 76.2f, 0.65f, Pal.TextDim, TA.Left);
            lb.Add("11 (9)", 45.2f, 98.6f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("14 (5)", 45.2f, 89.4f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("K1 pole 1", 48.6f, 95.0f, 0.9f, Pal.Title, TA.Left, 0, true);
            lb.Add("latch 11-14", 48.6f, 93.3f, 0.75f, Pal.TextDim, TA.Left);
            lb.Add("41 (12)", 59.2f, 246.6f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("44 (8)", 59.2f, 237.4f, 0.6f, Pal.TextDim, TA.Right);
            lb.Add("K1 pole 4", 58.4f, 243.0f, 0.9f, Pal.Title, TA.Right, 0, true);
            lb.Add("41-44 → KM1 coil", 58.4f, 241.3f, 0.75f, Pal.TextDim, TA.Right);
            lb.Add("(pole 3 empty: gap to 24 V)", 58.4f, 239.8f, 0.6f, Pal.TextDim, TA.Right);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            Sym.Contact(g, c11.pos, c14.pos, Closed);
            Sym.Contact(g, c41.pos, c44.pos, Closed);
            if (on)
            {
                glow.Line(new Vector2(36, 76.6f), new Vector2(36, 79.4f), 2.6f, Pal.P24);
                g.Line(new Vector2(34.8f, 78), new Vector2(37.2f, 78), 0.5f, Pal.P24);
            }
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(33.5f, 74.5f, 43, 81.5f), () => testLever = true, () => testLever = false);
            yield return Spot(Rect.MinMaxRect(44, 90, 48, 98));
            yield return Spot(Rect.MinMaxRect(58, 238, 62, 246));
        }

        public override string Status() =>
            (on ? "ENERGIZED (latched)" : "de-energized") + (testLever ? " · TEST LEVER pushed" : "") + " · hold the coil to push the test lever";
    }

    public class Psu : Device
    {
        public bool on;
        readonly Rect box;
        readonly Vector2 lPos, nPos, pePos;
        readonly Vector2[] plusPos, minusPos;
        readonly Pot pPlus, pMinus;
        readonly string boxTitle, boxSub;
        Terminal tl, tn;
        Terminal[] plus, minus;

        public Psu(string tag, string title, string info, Rect box, string boxTitle, string boxSub,
            Vector2 l, Vector2 n, Vector2 pe, Vector2[] plusPos, Vector2[] minusPos, Pot pPlus, Pot pMinus)
        {
            this.tag = tag;
            this.title = title;
            this.info = info;
            this.box = box;
            this.boxTitle = boxTitle;
            this.boxSub = boxSub;
            lPos = l;
            nPos = n;
            pePos = pe;
            this.plusPos = plusPos;
            this.minusPos = minusPos;
            this.pPlus = pPlus;
            this.pMinus = pMinus;
        }

        public override void Build()
        {
            tl = Term("L", lPos.x, lPos.y);
            tn = Term("N", nPos.x, nPos.y);
            Term("PE", pePos.x, pePos.y);
            plus = new Terminal[plusPos.Length];
            minus = new Terminal[minusPos.Length];
            for (int i = 0; i < plusPos.Length; i++)
            {
                string sfx = plusPos.Length > 1 ? (i + 1).ToString() : "";
                plus[i] = Term("+V" + sfx, plusPos[i].x, plusPos[i].y);
                minus[i] = Term("-V" + sfx, minusPos[i].x, minusPos[i].y);
            }
            // Output screws of one polarity are joined inside the PSU.
            for (int i = 1; i < plus.Length; i++) C.Fixed(plus[0], plus[i]);
            for (int i = 1; i < minus.Length; i++) C.Fixed(minus[0], minus[i]);
        }

        public override bool Evaluate()
        {
            bool e = C.Has(tl, Pot.L) && C.Has(tn, Pot.N);
            bool ch = e != on;
            on = e;
            return ch;
        }

        public override void Sources()
        {
            if (!on) return;
            foreach (var t in plus) C.Source(t, pPlus);
            foreach (var t in minus) C.Source(t, pMinus);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.Box;
            g.Rect(box.min, box.max);
            g.color = Pal.Sym;
            lb.Add(boxTitle, box.center.x, box.center.y + 1.0f, 1.0f, Pal.Title, TA.Center, 0, true);
            lb.Add(boxSub, box.center.x, box.center.y - 1.0f, 0.8f, Pal.TextDim);
            lb.Add("L", lPos.x, lPos.y - 1.1f, 0.65f, Pal.TextDim);
            lb.Add("N", nPos.x, nPos.y - 1.1f, 0.65f, Pal.TextDim);
            for (int i = 0; i < plusPos.Length; i++)
            {
                lb.Add("+V", plusPos[i].x, plusPos[i].y + 1.1f, 0.6f, Pal.TextDim);
                lb.Add("−V", minusPos[i].x, minusPos[i].y + 1.1f, 0.6f, Pal.TextDim);
            }
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            var led = new Vector2(box.xMax - 1.2f, box.yMax - 1.2f);
            if (on)
            {
                glow.Disc(led, 1.4f, Pal.LampGreen);
                g.Disc(led, 0.45f, Pal.LampGreen);
            }
            else g.Circle(led, 0.45f, 10);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(box); }
        public override string Status() => on ? "ON — output is live" : "OFF — no input power";
    }

    public class PushButton : Device
    {
        // Momentary: held by the mouse on the schematic, by a key, or by the HUD button.
        public bool spotHold, keyHold, uiHold, panelHold;
        public bool pressed => spotHold || keyHold || uiHold || panelHold;
        readonly bool nc;
        readonly float x, yT, yB;
        readonly Vector2 labelPos;
        readonly string label1, label2;
        Terminal top, bot;

        public PushButton(string tag, string title, string info, bool nc, float x, float yT, float yB, Vector2 labelPos, string l1, string l2)
        {
            this.tag = tag;
            this.title = title;
            this.info = info;
            this.nc = nc;
            this.x = x;
            this.yT = yT;
            this.yB = yB;
            this.labelPos = labelPos;
            label1 = l1;
            label2 = l2;
        }

        bool Closed => nc ? !pressed : pressed;

        public override void Build()
        {
            top = Term("1", x, yT);
            bot = Term("2", x, yB);
        }

        public override void Links()
        {
            if (Closed) C.Link(top, bot);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            lb.Add(label1, labelPos.x, labelPos.y + 0.8f, 0.95f, Pal.Title, TA.Right, 0, true);
            lb.Add(label2, labelPos.x, labelPos.y - 0.8f, 0.75f, Pal.TextDim, TA.Right);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            var mid = Sym.Contact(g, top.pos, bot.pos, Closed);
            Sym.PushActuator(g, mid, Vector2.left, pressed);
            if (pressed) glow.Disc(mid + Vector2.left * 2.4f, 1.6f, Pal.Sig);
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(x - 5.5f, yB, x + 2.5f, yT), () => spotHold = true, () => spotHold = false);
        }

        public override string Status() => (pressed ? "PRESSED" : "released") + (Closed ? " — contact closed" : " — contact open") + " (hold the mouse button)";
    }

    public class EStop : Device
    {
        public bool pressed;
        Terminal n1a, n1b, n2a, n2b;

        public EStop()
        {
            tag = "S0";
            title = "S0 — E-stop Schneider XB2-BS542 + added ZB2-BE102 (2× NC)";
            info = "NC1 (built in) opens the 24 V K1 chain. NC2 (added ZB2-BE102 block) gives the E-stop signal to A11 X7 (level NC),\n" +
                   "in series with the tool-setter over-travel (347). OFF does not open X7, so only the E-stop raises the A11 E-STOP alarm.\n" +
                   "Click to push / twist-release (latching).";
        }

        public override void Build()
        {
            n1a = Term("1", 36, 124);
            n1b = Term("2", 36, 116);
            n2a = Term("3", 164, 126);
            n2b = Term("4", 172, 126);
        }

        public override void Links()
        {
            if (pressed) return;
            C.Link(n1a, n1b);
            C.Link(n2a, n2b);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            lb.Add("S0 E-stop", 30.6f, 117.2f, 0.95f, Pal.Title, TA.Right, 0, true);
            lb.Add("NC1", 30.6f, 115.6f, 0.75f, Pal.TextDim, TA.Right);
            lb.Add("S0 E-stop NC2", 168f, 122.6f, 0.9f, Pal.Title, TA.Center, 0, true);
            lb.Add("(added ZB2-BE102 block)", 168f, 121.1f, 0.7f, Pal.TextDim);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            var m1 = Sym.Contact(g, n1a.pos, n1b.pos, !pressed);
            Sym.Mushroom(g, m1, Vector2.left, pressed);
            var m2 = Sym.Contact(g, n2a.pos, n2b.pos, !pressed);
            Sym.Mushroom(g, m2, Vector2.up, pressed);
            if (pressed)
            {
                glow.Disc(m1 + Vector2.left * 3.4f, 2.4f, Pal.LampRed);
                glow.Disc(m2 + Vector2.up * 3.4f, 2.4f, Pal.LampRed);
            }
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(31, 116, 38.5f, 124), () => pressed = !pressed);
            yield return Spot(Rect.MinMaxRect(163, 125, 173, 132), () => pressed = !pressed);
        }

        public override string Status() => pressed ? "PUSHED — chain open (NC1), A11 X7 open (NC2)" : "released — NC contacts closed";
    }

    public class Lamp : Device
    {
        public bool lit;
        readonly Vector2 c;
        readonly Color col;
        readonly string l1, l2;
        Terminal p, m;

        public Lamp(string tag, string title, string info, float x, float y, Color col, string l1, string l2)
        {
            this.tag = tag;
            this.title = title;
            this.info = info;
            c = new Vector2(x, y);
            this.col = col;
            this.l1 = l1;
            this.l2 = l2;
        }

        public override void Build()
        {
            p = Term("+", c.x, c.y + 3);
            m = Term("-", c.x, c.y - 3);
        }

        public override bool Evaluate()
        {
            lit = C.Has(p, Pot.P24) && C.Has(m, Pot.Z0);
            return false;
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.Line(p.pos - new Vector2(0, Sym.TermR), c + new Vector2(0, 2.2f));
            g.Line(m.pos + new Vector2(0, Sym.TermR), c - new Vector2(0, 2.2f));
            lb.Add(l1, c.x + 2.8f, c.y + 0.8f, 0.9f, Pal.Title, TA.Left, 0, true);
            lb.Add(l2, c.x + 2.8f, c.y - 0.8f, 0.75f, Pal.TextDim, TA.Left);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            if (lit)
            {
                glow.Disc(c, 5.5f, col);
                g.Disc(c, 2.1f, col);
                g.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
                float k = 2.2f * 0.7071f;
                g.Line(c + new Vector2(-k, -k), c + new Vector2(k, k));
                g.Line(c + new Vector2(-k, k), c + new Vector2(k, -k));
                g.color = Pal.Sym;
            }
            Sym.LampSymbol(g, c, 2.2f);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(new Rect(c.x - 2.5f, c.y - 2.5f, 5, 5)); }
        public override string Status() => lit ? "ON" : "off";
    }

    public class Fan : Device
    {
        public bool running;
        float angle;
        Terminal p, m;

        public Fan()
        {
            tag = "M2";
            title = "M2 — enclosure fan 24 V";
            info = "Rev 0.6: supplied from +24 V CONTROL (KM1 aux 43, behind F1, wire 170) and 0 V (171). Rev 0.5 had it unfused on TB24+.";
        }

        public override void Build()
        {
            p = Term("+", 16, 124);
            m = Term("-", 16, 118);
        }

        public override bool Evaluate()
        {
            running = C.Has(p, Pot.P24) && C.Has(m, Pot.Z0);
            return false;
        }

        public override void Tick(float dt)
        {
            if (running) angle -= 540 * dt;
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.Circle(new Vector2(16, 121), 2.58f, 24);
            lb.Add("M2", 19.2f, 121.8f, 0.9f, Pal.Title, TA.Left, 0, true);
            lb.Add("fan", 19.2f, 120.2f, 0.75f, Pal.TextDim, TA.Left);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            var c = new Vector2(16, 121);
            Sym.Rotor(g, c, 1.9f, angle, 3);
            if (running) glow.Disc(c, 2.8f, Pal.P24 * 0.6f);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(new Rect(13, 118, 6, 6)); }
        public override string Status() => running ? "running" : "stopped";
    }

    public class Vfd : Device
    {
        public static readonly float[] Table = { 100f, 133.3f, 166.7f, 200f, 250f, 300f, 350f, 400f };
        public bool powered, run, braking;
        public int bits;
        public float freq, target;
        Terminal L, N, PP, PB, U, V, W, COM;
        readonly Terminal[] X = new Terminal[5];

        public Vfd()
        {
            tag = "U1";
            title = "U1 — VFD BEST FC300-2.2G-S2-B4C";
            info = "P064 = 0 (panel digital), P065 = 1 (run from terminals), P091 = 8 (X1 forward),\nP092–P094 = 1/2/3 (multi-speed bits), P095–P097 = 0, P030–P037 = 100…400 Hz, P021 = 400, P022 = 100.\nInput on = X connected to COM (sink).";
        }

        public float Rpm => freq * 60f;

        public override void Build()
        {
            L = Term("L", 136, 188);
            N = Term("N", 142, 188);
            PP = Term("P+", 128, 182);
            PB = Term("PB", 128, 174);
            U = Term("U", 172, 184);
            V = Term("V", 172, 180);
            W = Term("W", 172, 176);
            var pe = Term("PE", 172, 170);
            var peb = Term("PEb", 172, 166);
            var pec = Term("PEc", 172, 162);
            C.Fixed(pe, peb);
            C.Fixed(pe, pec);
            for (int i = 1; i <= 4; i++) X[i] = Term("X" + i, 130 + 6 * i, 160);
            COM = Term("COM", 162, 160);
        }

        public override bool Evaluate()
        {
            bool p = C.Has(L, Pot.L) && C.Has(N, Pot.N);
            run = p && C.Same(X[1], COM);
            bits = p ? (C.Same(X[2], COM) ? 1 : 0) | (C.Same(X[3], COM) ? 2 : 0) | (C.Same(X[4], COM) ? 4 : 0) : 0;
            bool ch = p != powered;
            powered = p;
            return ch;
        }

        public override void Tick(float dt)
        {
            target = run ? Table[bits] : 0f;
            if (powered)
            {
                if (freq < target) freq = Mathf.Min(target, freq + 140f * dt);
                else if (freq > target) freq = Mathf.Max(target, freq - 170f * dt);
                braking = freq > target + 0.5f;
            }
            else
            {
                freq = Mathf.Max(0, freq - 30f * dt);
                braking = false;
            }
        }

        public override void Sources()
        {
            if (!powered) return;
            C.Source(PP, Pot.DCB);
            if (freq > 0.5f)
            {
                C.Source(U, Pot.PH);
                C.Source(V, Pot.PH);
                C.Source(W, Pot.PH);
            }
            if (braking) C.Source(PB, Pot.BRK);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.Box;
            g.Rect(new Vector2(128, 160), new Vector2(172, 188));
            g.color = Pal.Sym;
            lb.Add("U1  VFD FC300-2.2G", 150, 184.6f, 1.15f, Pal.Title, TA.Center, 0, true);
            lb.Dyn(VfdLine1, 150, 180.6f, 1.0f, Pal.Sig, TA.Center, true);
            lb.Dyn(VfdLine2, 150, 178.4f, 0.8f, Pal.Text);
            lb.Add("P064=0 · P065=1 · P091=8 · P092–94=1/2/3", 150, 174.4f, 0.65f, Pal.TextDim);
            lb.Add("P030–P037 = 100…400 Hz · P021=400 · P022=100", 150, 172.9f, 0.65f, Pal.TextDim);
            lb.Add("L", 136, 186.8f, 0.65f, Pal.TextDim);
            lb.Add("N", 142, 186.8f, 0.65f, Pal.TextDim);
            lb.Add("P+", 129.2f, 182, 0.65f, Pal.TextDim, TA.Left);
            lb.Add("PB", 129.2f, 174, 0.65f, Pal.TextDim, TA.Left);
            lb.Add("U", 170.8f, 184, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("V", 170.8f, 180, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("W", 170.8f, 176, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("PE", 170.8f, 170, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("PE", 170.8f, 166, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("PE", 170.8f, 162, 0.65f, Pal.TextDim, TA.Right);
            for (int i = 1; i <= 4; i++) lb.Add("X" + i, 130 + 6 * i, 161.3f, 0.65f, Pal.TextDim);
            lb.Add("COM", 162, 161.3f, 0.65f, Pal.TextDim);
            lb.Add("FWD  MS1  MS2  MS3", 145, 163.0f, 0.55f, Pal.TextDim);
        }

        string VfdLine1()
        {
            if (!powered) return freq > 0.5f ? "NO POWER — coasting" : "OFF";
            if (braking) return $"DECEL {freq:0.0} Hz";
            if (run) return $"RUN FWD {freq:0.0} Hz";
            return "READY  0.0 Hz";
        }

        string VfdLine2()
        {
            if (!powered) return "";
            int grade = bits + 1;
            return run ? $"multi-speed S{grade} (X4X3X2 = {Bits3(bits)}) → P03{bits} = {Table[bits]:0.0} Hz" : "X1 open — waiting for A11 Y1";
        }

        public static string Bits3(int b) => $"{(b >> 2) & 1}{(b >> 1) & 1}{b & 1}";

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(128, 160, 172, 188)); }

        public override string Status() => VfdLine1() + (powered ? $" · {Rpm:0} rpm" : "");
    }

    public class BrakeResistor : Device
    {
        public Vfd vfd;

        public BrakeResistor()
        {
            tag = "R1";
            title = "R1 — braking resistor 100 Ω / 800 W";
            info = "Connect to VFD P+ and PB only (CAUTION C12). Glows while the VFD brakes the spindle.";
        }

        public override void Build()
        {
            Term("T1", 118, 182);
            Term("T2", 118, 174);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.Line(new Vector2(118, 182 - Sym.TermR), new Vector2(118, 180));
            g.Line(new Vector2(118, 174 + Sym.TermR), new Vector2(118, 176));
            g.Rect(new Vector2(117, 176), new Vector2(119, 180));
            lb.Add("R1", 116.2f, 178.8f, 1.0f, Pal.Title, TA.Right, 0, true);
            lb.Add("100 Ω / 800 W", 116.2f, 177.1f, 0.75f, Pal.TextDim, TA.Right);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            if (vfd != null && vfd.braking)
            {
                glow.Line(new Vector2(118, 176.5f), new Vector2(118, 179.5f), 4.5f, Pal.Brake);
                g.Line(new Vector2(118, 176.5f), new Vector2(118, 179.5f), 1.4f, Pal.Brake);
            }
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(116, 175, 120, 181)); }
        public override string Status() => vfd != null && vfd.braking ? "BRAKING — hot" : "cold";
    }

    public class SpindleConnector : Device
    {
        public SpindleConnector()
        {
            tag = "X1";
            title = "X1 — spindle connector HDC-HA-004 (metal hood, EMC gland)";
            info = "Pins 1-2-3 = U-V-W, PE. Pin 4 is not used: the cable shield is clamped 360° in the metal hood (SH)\n" +
                   "on both sides of the connector — EMC clamp at the VFD (wire 127) and EMC gland at the spindle.";
        }

        public override void Build()
        {
            Term("1", 182, 184);
            Term("2", 182, 180);
            Term("3", 182, 176);
            Term("PE", 182, 170);
            Term("SH", 182, 166);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.SymDim;
            g.DashedRect(new Vector2(180.6f, 163.8f), new Vector2(183.4f, 186.2f));
            g.color = Pal.Sym;
            lb.Add("X1 HDC-HA", 182, 187.4f, 0.8f, Pal.Title, TA.Center, 0, true);
            string[] n = { "1", "2", "3", "PE", "SH" };
            float[] y = { 184, 180, 176, 170, 166 };
            for (int i = 0; i < 5; i++) lb.Add(n[i], 183.9f, y[i] + 0.9f, 0.6f, Pal.TextDim, TA.Left);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(180.6f, 163.8f, 183.4f, 186.2f)); }
    }

    public class SpindleMotor : Device
    {
        public Vfd vfd;
        float angle;
        static readonly Vector2 C0 = new(196, 178);

        public SpindleMotor()
        {
            tag = "M1";
            title = "M1 — spindle 2.2 kW, 2-pole (400 Hz = 24000 rpm)";
            info = "Speed grades S1–S8 = 6000…24000 rpm (spec § 7.3).";
        }

        public override void Build()
        {
            Term("U", 192.1f, 180.25f, true);
            Term("V", 191.5f, 178f, true);
            Term("W", 192.1f, 175.75f, true);
            Term("PE", 196f, 173.5f, true);
        }

        public override void Tick(float dt)
        {
            if (vfd != null) angle -= vfd.freq * 3.2f * dt;
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.Circle(C0, 4.5f, 36);
            lb.Add("M1 spindle", 201.6f, 180.0f, 0.95f, Pal.Title, TA.Left, 0, true);
            lb.Dyn(() => vfd == null ? "" : $"{vfd.Rpm:0} rpm", 201.6f, 178.2f, 0.85f, Pal.Phase, TA.Left);
            lb.Add("2.2 kW · 3~", 201.6f, 176.6f, 0.7f, Pal.TextDim, TA.Left);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            Sym.Rotor(g, C0, 3.4f, angle, 3);
            g.Circle(C0, 1.0f, 12);
            if (vfd != null && vfd.freq > 0.5f) glow.Disc(C0, 5.5f, Pal.Phase * Mathf.Clamp01(vfd.freq / 200f + 0.25f));
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(new Rect(191, 173, 10, 10)); }
        public override string Status() => vfd == null ? "" : $"{vfd.Rpm:0} rpm";
    }

    public class SchukoPump : Device
    {
        public bool running;
        float angle;
        Terminal l, n;

        public SchukoPump()
        {
            tag = "XS1";
            title = "XS1 — Schuko outlet (coolant pump)";
            info = "Own breaker QF3 (before: the pump had no breaker). De-energized when KM1 opens.";
        }

        public override void Build()
        {
            l = Term("L", 290, 188);
            n = Term("N", 296, 188);
            Term("PE", 302, 183);
            var cl = Term("cL", 291, 178, true);
            var cn = Term("cN", 297, 178, true);
            C.Fixed(l, cl);
            C.Fixed(n, cn);
            Term("pL", 292.4f, 169.1f, true);
            Term("pN", 295.6f, 169.1f, true);
        }

        public override bool Evaluate()
        {
            running = C.Has(l, Pot.L) && C.Has(n, Pot.N);
            return false;
        }

        public override void Tick(float dt)
        {
            if (running) angle -= 400 * dt;
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.Box;
            g.Rect(new Vector2(286, 178), new Vector2(302, 188));
            g.color = Pal.Sym;
            lb.Add("XS1 Schuko", 294, 184.4f, 0.95f, Pal.Title, TA.Center, 0, true);
            lb.Add("pump outlet", 294, 181.6f, 0.75f, Pal.TextDim);
            g.Circle(new Vector2(294, 166), 3.5f, 28);
            lb.Add("Coolant pump", 298.6f, 166.6f, 0.85f, Pal.Title, TA.Left, 0, true);
            lb.Dyn(() => running ? "running" : "stopped", 298.6f, 165.0f, 0.75f, Pal.TextDim, TA.Left);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            Sym.Rotor(g, new Vector2(294, 166), 2.6f, angle, 4);
            if (running) glow.Disc(new Vector2(294, 166), 4.2f, Pal.L * 0.7f);
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(286, 178, 302, 188));
            yield return Spot(new Rect(290, 162, 8, 8));
        }

        public override string Status() => running ? "pump running" : "no power";
    }

    public class StepperDrive : Device
    {
        public bool powered, fault, signalOk;
        public readonly int axis;
        readonly float y0;
        Terminal ac1, ac2, pulP, almP, almM, ap, am, bp, bm;
        static readonly string[] Ax = { "X", "Y", "Z" };

        public StepperDrive(int axis, float y0)
        {
            this.axis = axis;
            this.y0 = y0;
            tag = "A" + (axis + 2);
            title = $"{tag} — Leadshine DMA860H, {Ax[axis]} axis";
            info = "DIP: 1600 pulse/rev, 5.14 A peak. Power 48 V from G1 (range 26–113 V DC).\nInputs need 7–16 mA at 5 V (A11 supplies 8 mA max — CAUTION C5).\nClick to inject / clear a drive fault (ALM output conducts → A11 X5).";
        }

        public bool CanMove => powered && !fault && signalOk;

        public override void Build()
        {
            pulP = Term("PUL+", 250, y0 + 22);
            Term("PUL-", 250, y0 + 18);
            Term("DIR+", 250, y0 + 14);
            Term("DIR-", 250, y0 + 10);
            almM = Term("ALM-", 250, y0 + 6);
            almP = Term("ALM+", 250, y0 + 2);
            ac1 = Term("AC1", 272, y0 + 28);
            ac2 = Term("AC2", 278, y0 + 28);
            ap = Term("A+", 286, y0 + 22);
            am = Term("A-", 286, y0 + 18);
            bp = Term("B+", 286, y0 + 14);
            bm = Term("B-", 286, y0 + 10);
        }

        public override bool Evaluate()
        {
            bool p = C.Has(ac1, Pot.P48) && C.Has(ac2, Pot.Z48);
            signalOk = C.Has(pulP, Pot.P5);
            bool ch = p != powered;
            powered = p;
            return ch;
        }

        public override void Links()
        {
            if (fault && powered) C.Link(almP, almM);
        }

        public override void Sources()
        {
            if (!powered) return;
            C.Source(ap, Pot.MOT);
            C.Source(am, Pot.MOT);
            C.Source(bp, Pot.MOT);
            C.Source(bm, Pot.MOT);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.Box;
            g.Rect(new Vector2(250, y0), new Vector2(286, y0 + 28));
            g.color = Pal.Sym;
            lb.Add($"{tag}  DMA860H-{Ax[axis]}", 269, y0 + 23.6f, 1.05f, Pal.Title, TA.Center, 0, true);
            lb.Add("1600 pulse/rev · 5.14 A pk", 269, y0 + 21.6f, 0.65f, Pal.TextDim);
            lb.Dyn(StatusLine, 269, y0 + 16.4f, 0.95f, Pal.Sig, TA.Center, true);
            string[] left = { "PUL+", "PUL−", "DIR+", "DIR−", "ALM−", "ALM+" };
            for (int i = 0; i < 6; i++) lb.Add(left[i], 253.6f, y0 + 22 - 4 * i, 0.65f, Pal.TextDim, TA.Left);
            string[] right = { "A+", "A−", "B+", "B−" };
            for (int i = 0; i < 4; i++) lb.Add(right[i], 284.6f, y0 + 22 - 4 * i, 0.65f, Pal.TextDim, TA.Right);
            lb.Add("AC1", 272, y0 + 26.8f, 0.6f, Pal.TextDim);
            lb.Add("AC2", 278, y0 + 26.8f, 0.6f, Pal.TextDim);
            lb.Add("click: inject fault", 269, y0 + 2.0f, 0.6f, Pal.TextDim);
        }

        string StatusLine()
        {
            if (!powered) return "NO POWER";
            if (fault) return "FAULT (ALM)";
            if (!signalOk) return "ON · no 5 V signal";
            return "READY";
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            var led = new Vector2(283.6f, y0 + 26);
            Color c = !powered ? new Color(0, 0, 0, 0) : fault ? Pal.LampRed : Pal.LampGreen;
            if (powered)
            {
                bool blink = !fault || Mathf.Repeat(time, 0.5f) < 0.3f;
                if (blink)
                {
                    glow.Disc(led, 1.5f, c);
                    g.Disc(led, 0.5f, c);
                }
            }
            g.Circle(led, 0.5f, 10);
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(255, y0 + 3, 281, y0 + 25), () => fault = !fault);
        }

        public override string Status() => StatusLine();
    }

    /// Motor connector GX16-6 plus the stepper motor.
    public class AxisMotor : Device
    {
        public Machine m;
        public readonly int axis;
        readonly float y0;
        float angle;
        static readonly string[] Ax = { "X", "Y", "Z" };
        Vector2 Center => new(314, y0 + 16);

        public AxisMotor(int axis, float y0)
        {
            this.axis = axis;
            this.y0 = y0;
            tag = "XM-" + Ax[axis];
            title = $"XM-{Ax[axis]} GX16-6 and {Ax[axis]}-axis stepper";
            info = "Motor cores A+/A−/B+/B− → pins 1–4. Do not connect or disconnect when QF2 is ON (CAUTION C4).";
        }

        public static readonly float[] Angles = { 150, 170, 190, 210 };

        public override void Build()
        {
            for (int i = 0; i < 4; i++) Term((i + 1).ToString(), 298, y0 + 22 - 4 * i);
            for (int i = 0; i < 4; i++)
            {
                var p = Center + Gfx.Dir(Angles[i]) * 5f;
                Term("m" + (i + 1), p.x, p.y, true);
            }
        }

        public override void Tick(float dt)
        {
            if (m != null) angle -= m.vel[axis] * 7f * dt;
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            lb.Add($"XM-{Ax[axis]} GX16-6", 298, y0 + 24.4f, 0.75f, Pal.Title, TA.Center, 0, true);
            for (int i = 0; i < 4; i++) lb.Add((i + 1).ToString(), 298.9f, y0 + 22.9f - 4 * i, 0.55f, Pal.TextDim, TA.Left);
            g.Circle(Center, 5f, 36);
            lb.Add($"{Ax[axis]} axis", 320.4f, y0 + 16.8f, 0.95f, Pal.Title, TA.Left, 0, true);
            lb.Add("stepper", 320.4f, y0 + 15.1f, 0.75f, Pal.TextDim, TA.Left);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            Sym.Rotor(g, Center, 3.6f, angle, 4);
            g.Circle(Center, 1.1f, 12);
            if (m != null && Mathf.Abs(m.vel[axis]) > 0.01f) glow.Disc(Center, 6f, Pal.Mot * 0.8f);
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(new Rect(308.5f, y0 + 10.5f, 11, 11)); }
        public override string Status() => m == null ? "" : $"{Ax[axis]} = {m.pos[axis]:0.00} mm (actual)";
    }

    public class Sensor
    {
        public string name;
        public Terminal pin;
        public Func<bool> physical;
        public bool manual;
        public bool active;
        public Rect rect;
    }

    /// GX16-4 limit connector with two NPN NO proximity sensors.
    public class SensorConnector : Device
    {
        public readonly Sensor[] sensors = new Sensor[2];
        public bool powered;
        readonly float y0;
        readonly bool zStyle;
        readonly string axisTitle;
        readonly string[] names;
        readonly Func<bool>[] phys;
        Terminal p3, p4;

        public SensorConnector(string tag, string axisTitle, float y0, bool zStyle, string upperName, Func<bool> upperPhys, string lowerName, Func<bool> lowerPhys)
        {
            this.tag = tag;
            this.axisTitle = axisTitle;
            this.y0 = y0;
            this.zStyle = zStyle;
            names = new[] { upperName, lowerName };
            phys = new[] { upperPhys, lowerPhys };
            title = $"{tag} — {axisTitle} limit connector GX16-4";
            info = "Pin 4 BN = +24 V, pin 3 BU = 0 V (measure first — CAUTION C10). NPN NO sensors:\nthe output connects to 0 V when the target is near. Click a sensor to operate it by hand.";
        }

        public override void Build()
        {
            p4 = Term("4", 140, y0 + 12);
            p3 = Term("3", 140, y0 + 2);
            Terminal upper, lower;
            if (!zStyle)
            {
                upper = Term("1", 160, y0 + 10);
                lower = Term("2", 160, y0 + 4);
            }
            else
            {
                upper = Term("2", 160, y0 + 10);
                lower = Term("1", 160, y0 + 4);
            }
            sensors[0] = new Sensor { name = names[0], pin = upper, physical = phys[0], rect = Rect.MinMaxRect(144.5f, y0 + 8.7f, 157, y0 + 11.3f) };
            sensors[1] = new Sensor { name = names[1], pin = lower, physical = phys[1], rect = Rect.MinMaxRect(144.5f, y0 + 2.7f, 157, y0 + 5.3f) };
        }

        public override bool Evaluate()
        {
            powered = C.Has(p4, Pot.P24) && C.Has(p3, Pot.Z0);
            bool ch = false;
            foreach (var s in sensors)
            {
                bool a = powered && (s.manual || s.physical());
                if (a != s.active)
                {
                    s.active = a;
                    ch = true;
                }
            }
            return ch;
        }

        public override void Links()
        {
            foreach (var s in sensors)
                if (s.active) C.Link(s.pin, p3);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.SymDim;
            g.DashedRect(new Vector2(140, y0), new Vector2(160, y0 + 14));
            g.color = Pal.Sym;
            lb.Add($"{tag} · {axisTitle}", 150, y0 + 15.4f, 0.9f, Pal.Title, TA.Center, 0, true);
            lb.Add("4 BN", 141.0f, y0 + 12.9f, 0.6f, Pal.TextDim, TA.Left);
            lb.Add("3 BU", 141.0f, y0 + 1.1f, 0.6f, Pal.TextDim, TA.Left);
            foreach (var s in sensors)
            {
                g.Rect(s.rect.min, s.rect.max);
                g.Line(new Vector2(s.rect.xMax, s.pin.pos.y), s.pin.pos - new Vector2(Sym.TermR, 0));
                lb.Add(s.name, s.rect.center.x - 0.6f, s.rect.center.y, 0.7f, Pal.Text);
                lb.Add(s.pin.name, 159.0f, s.pin.pos.y + 0.9f, 0.55f, Pal.TextDim, TA.Right);
            }
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            foreach (var s in sensors)
            {
                var led = new Vector2(s.rect.xMax - 0.9f, s.rect.center.y);
                if (s.active)
                {
                    glow.Disc(led, 1.5f, Pal.Sig);
                    g.Disc(led, 0.45f, Pal.Sig);
                }
                else g.Circle(led, 0.45f, 10);
                if (s.manual) g.Line(new Vector2(s.rect.xMin, s.rect.yMin - 0.35f), new Vector2(s.rect.xMax, s.rect.yMin - 0.35f), 0.12f, Pal.Caution);
            }
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            foreach (var s in sensors)
            {
                var sensor = s;
                yield return new Hotspot
                {
                    dev = this,
                    rect = s.rect,
                    onDown = () => sensor.manual = !sensor.manual,
                    tip = () => $"<b>{sensor.name}</b> — NPN NO proximity sensor ({tag} pin {sensor.pin.name})\n" +
                                $"<color=#9fe8ff>State: {(sensor.active ? "ACTIVE (output pulled to 0 V)" : powered ? "inactive" : "NO SUPPLY")}" +
                                $"{(sensor.manual ? " · forced by hand" : "")}</color>\nClick to operate it by hand."
                };
            }
            yield return Spot(Rect.MinMaxRect(140, y0, 160, y0 + 14));
        }

        public override string Status() => powered ? "sensors supplied" : "NO SUPPLY (check F2 and the A11 J8 24V / GND screws)";
    }

    public class ToolSetter : Device
    {
        public Machine m;
        public bool manual, manualOver, touch, over;
        Terminal p1, p2, p3, p4;

        public ToolSetter()
        {
            tag = "XT";
            title = "XT — tool setter GX16-5";
            info = "Pin 1-2: touch contact (NO) → A11 X4. Pin 3-4: over-travel (NC) in series with E-stop NC2 → A11 X7 (wire 347):\n" +
                   "over-travel (or the setter unplugged) = E-stop on the A11. Fit a dummy plug (pins 3-4 bridged) if the setter is off.\n" +
                   "Click the plate to touch it by hand; click the lower contact to push the over-travel switch.";
        }

        public override void Build()
        {
            p2 = Term("2", 140, 54);
            p4 = Term("4", 140, 44);
            p1 = Term("1", 160, 54);
            p3 = Term("3", 160, 44);
        }

        public override bool Evaluate()
        {
            bool t = manual || (m != null && m.ToolTouch);
            bool o = manualOver || (m != null && m.ToolOver);
            bool ch = t != touch || o != over;
            touch = t;
            over = o;
            return ch;
        }

        public override void Links()
        {
            if (touch) C.Link(p1, p2);
            if (!over) C.Link(p3, p4);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.color = Pal.SymDim;
            g.DashedRect(new Vector2(140, 40), new Vector2(160, 58));
            g.color = Pal.Sym;
            lb.Add("XT tool setter · GX16-5", 150, 59.4f, 0.9f, Pal.Title, TA.Center, 0, true);
            lb.Add("touch (NO)", 150, 56.6f, 0.65f, Pal.TextDim);
            lb.Add("over-travel (NC)", 150, 41.2f, 0.65f, Pal.TextDim);
            lb.Add("2", 141.0f, 54.9f, 0.55f, Pal.TextDim, TA.Left);
            lb.Add("1", 159.0f, 54.9f, 0.55f, Pal.TextDim, TA.Right);
            lb.Add("4", 141.0f, 44.9f, 0.55f, Pal.TextDim, TA.Left);
            lb.Add("3", 159.0f, 44.9f, 0.55f, Pal.TextDim, TA.Right);
        }

        public override void DrawDynamic(Gfx g, Gfx glow, float time)
        {
            Sym.Contact(g, p2.pos, p1.pos, touch);
            Sym.Contact(g, p4.pos, p3.pos, !over);
            if (touch) glow.Disc(new Vector2(150, 54), 2.2f, Pal.Sig);
        }

        public override IEnumerable<Hotspot> Hotspots()
        {
            yield return Spot(Rect.MinMaxRect(142, 50, 158, 57.5f), () => manual = !manual);
            yield return Spot(Rect.MinMaxRect(142, 41.5f, 158, 47), () => manualOver = !manualOver);
            yield return Spot(Rect.MinMaxRect(140, 40, 160, 58));
        }

        public override string Status() => (touch ? "TOUCH" : "open") + (manual ? " (by hand)" : "") + (over ? " · OVER-TRAVEL → A11 E-stop" : "");
    }

    public class PeBar : Device
    {
        // 139, 141, 142 and 147 are real wires (A11Layout); the other drops mark the far end of a device-side stub.
        static readonly (float x, string l, bool line)[] Drops =
        {
            (157.5f, "139 Enclosure", false), (165, "141 Backplate", false), (180, "142 Door strap", false), (195, "143 Z1 lug", true), (210, "144 U1 PE", true),
            (225, "145 G1 PE", true), (240, "146 G2 PE", true), (255, "147 DIN-rail TB", false), (270, "148 TB0V (PELV)", true),
        };

        public PeBar()
        {
            tag = "PEBAR";
            title = "PE bars 1 + 2 (earth bars)";
            info = "Two copper bars, 150 × 25 mm, each on two 17 mm red insulators with 4 points, linked by 138. All protective-earth conductors. " +
                   "Up to 2 conductors per point (owner decision 2026-10-26) — a deviation from IEC 60204-1 (one protective conductor per terminal point); " +
                   "a 12-point bar would remove it. Wire 148 is the only 0 V to PE bond (PELV).";
        }

        public override void Build()
        {
            Term("BAR", 150, 280, true);
            Term("BAR2", 221, 280, true);
        }

        public override void DrawStatic(Gfx g, Labels lb)
        {
            g.Line(new Vector2(286, 280), new Vector2(286, 277.5f), 0.2f, Pal.PE);
            g.color = Pal.PE;
            Sym.Ground(g, new Vector2(286, 277.5f));
            g.color = Pal.Sym;
            lb.Add("PE bar 1", 152, 281.6f, 1.05f, Pal.Title, TA.Left, 0, true);
            lb.Add("PE bar 2", 223, 281.6f, 1.05f, Pal.Title, TA.Left, 0, true);
            foreach (var (x, l, line) in Drops)
            {
                if (line) g.Dashed(new Vector2(x, 280), new Vector2(x, 276.5f), 0.4f, 0.3f);
                lb.Add(l, x, 276.0f, 0.75f, Pal.Text, TA.Left, -90);
            }
        }

        public override IEnumerable<Hotspot> Hotspots() { yield return Spot(Rect.MinMaxRect(150, 279, 286, 281)); }
    }

    public class Machine
    {
        public readonly float[] pos = { 320f, 240f, -30f };
        public readonly float[] vel = new float[3];
        public readonly float[] min = { 0, 0, -120 };
        public readonly float[] max = { 800, 600, 0 };
        public const float Over = 4f;

        public bool XHome => pos[0] <= 0f;
        public bool XLimit => pos[0] >= max[0];
        public bool YHome => pos[1] <= 0f;
        public bool YLimit => pos[1] >= max[1];
        public bool ZHome => pos[2] >= 0f;
        public bool ZLimit => pos[2] <= min[2];
        public bool ToolTouch => pos[2] <= -95f;
        public bool ToolOver => pos[2] <= -105f;

        public void Move(int i, float v, float dt)
        {
            vel[i] = v;
            pos[i] = Mathf.Clamp(pos[i] + v * dt, min[i] - Over, max[i] + Over);
        }
    }
}
