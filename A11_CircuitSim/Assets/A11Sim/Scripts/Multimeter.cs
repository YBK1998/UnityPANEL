using System;
using System.Collections.Generic;
using UnityEngine;

namespace A11Sim
{
    public enum MeterMode { VDC, VAC, Ohm }

    /// Hand-held multimeter with two probes on any terminal, read from the solved nets.
    /// The solver knows which supply a net carries, not its impedance, so:
    /// - voltages are the nominal source values (G2 24.1 V, G1 48.3 V, mains 231 V);
    /// - a load (coil, lamp, fan) passes the voltage of one side to an otherwise open net, as on a real meter;
    /// - continuity follows conductors, fixed links and closed contacts only, so a load reads OL.
    public class Multimeter
    {
        public bool active;
        public MeterMode mode = MeterMode.Ohm;
        public Terminal red, black;
        public bool nextBlack;

        public struct Reading
        {
            public string value, unit, note;
            public float num;
            public bool beep, ol, live, valid;
        }

        readonly Circuit C;
        readonly Vfd vfd;
        readonly A11Board a11;
        readonly List<(Terminal a, Terminal b)> loads = new();
        readonly List<(Terminal t, Terminal refT, float volts, Func<bool> on)> pullUps = new();
        readonly Dictionary<Terminal, int> conductorNet = new();
        readonly Terminal[] phase;

        struct Val
        {
            public bool known, unstable;
            public int dom;
            public float dc;
            public Vector2 ac;
            public string note;
        }

        readonly Dictionary<int, Val> vals = new();
        int computedFrame = -1;

        public Multimeter(Circuit c)
        {
            C = c;
            vfd = c.Get<Vfd>("U1");
            a11 = c.Get<A11Board>("A1");
            phase = new[] { c.T("U1.U"), c.T("U1.V"), c.T("U1.W") };
            foreach (var (a, b) in new[] { ("K1.A1", "K1.A2"), ("KM1.A1", "KM1.A2"), ("H1.+", "H1.-"), ("H2.+", "H2.-"), ("H3.+", "H3.-"), ("M2.+", "M2.-") })
                loads.Add((c.T(a), c.T(b)));
            for (int i = 1; i <= 8; i++)
                if (c.byId.ContainsKey("A1.X" + i)) pullUps.Add((c.T("A1.X" + i), c.T("A1.GND"), 24.1f, () => a11.sensorSupply));
            for (int i = 1; i <= 4; i++) pullUps.Add((c.T("U1.X" + i), c.T("U1.COM"), 24f, () => vfd.powered));
            pullUps.Add((c.T("U1.PB"), c.T("U1.P+"), 0f, () => vfd.powered));

            // Physical conductor nets (wires and fixed links only): a probe anywhere on the right conductor counts.
            var parent = new Dictionary<Terminal, Terminal>();
            Terminal Find(Terminal t)
            {
                while (parent.TryGetValue(t, out var p) && p != t) t = p;
                return t;
            }
            void Union(Terminal a, Terminal b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }
            foreach (var w in c.wires)
                if (!w.notConnected && !w.isBus) Union(w.a, w.b);
            foreach (var (a, b) in c.FixedLinks) Union(a, b);
            var ids = new Dictionary<Terminal, int>();
            foreach (var t in c.terminals)
            {
                var r = Find(t);
                if (!ids.TryGetValue(r, out int id)) ids[r] = id = ids.Count;
                conductorNet[t] = id;
            }
        }

        /// True if the probe sits on the same conductor as the target terminal.
        public bool OnConductor(Terminal probe, string targetId) =>
            probe != null && C.byId.TryGetValue(targetId, out var t) && conductorNet[probe] == conductorNet[t];

        public void Place(Terminal t)
        {
            if (nextBlack) black = t;
            else red = t;
            nextBlack = !nextBlack;
        }

        public void Clear()
        {
            red = black = null;
            nextBlack = false;
        }

        public void Swap() => (red, black) = (black, red);

        // ------------------------------------------------------------------ net values

        static bool Has(Pot p, Pot f) => (p & f) != 0;

        const Pot LiveMask = Pot.L | Pot.N | Pot.P24 | Pot.P48 | Pot.P5 | Pot.PH | Pot.DCB | Pot.BRK | Pot.MOT;

        Val Strong(Terminal t)
        {
            var p = C.PotOf(t);
            if (Has(p, Pot.L)) return new Val { known = true, ac = new Vector2(231f, 0) };
            if (Has(p, Pot.PH))
            {
                float vll = 220f * Mathf.Clamp01(vfd.freq / 400f);
                for (int i = 0; i < 3; i++)
                    if (C.Same(t, phase[i]))
                    {
                        float a = -i * 120f * Mathf.Deg2Rad;
                        return new Val { known = true, ac = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (vll / 1.732f), note = "VFD PWM output: use a true-RMS meter" };
                    }
            }
            if (Has(p, Pot.DCB)) return new Val { known = true, dc = 155f, note = "VFD DC bus: ≈ 310 V between P+ and DC−" };
            if (Has(p, Pot.BRK)) return new Val { known = true, dc = -155f, unstable = true, note = "brake chopper switching" };
            if (Has(p, Pot.N) || Has(p, Pot.PE) || Has(p, Pot.Z0)) return new Val { known = true };
            if (Has(p, Pot.P24)) return new Val { known = true, dc = 24.1f };
            if (Has(p, Pot.P5)) return new Val { known = true, dc = 5.02f };
            if (Has(p, Pot.P48)) return new Val { known = true, dom = 1, dc = 48.3f };
            if (Has(p, Pot.Z48)) return new Val { known = true, dom = 1 };
            if (Has(p, Pot.MOT)) return new Val { known = true, dom = 1, dc = 24f, unstable = true, note = "stepper phase PWM: the reading jumps" };
            if (Has(p, Pot.ACT))
            {
                bool pulse = t.name.EndsWith("_PUL") || t.name.StartsWith("PUL");
                if (!pulse)
                    foreach (var o in C.terminals)
                        if (o.name.EndsWith("_PUL") && C.Same(t, o)) pulse = true;
                return pulse ? new Val { known = true, dc = 2.5f, unstable = true, note = "step pulses (average)" } : new Val { known = true };
            }
            return default;
        }

        void Compute()
        {
            if (computedFrame == Time.frameCount) return;
            computedFrame = Time.frameCount;
            vals.Clear();
            foreach (var t in C.terminals)
            {
                int n = C.NetOf(t);
                if (!vals.ContainsKey(n)) vals[n] = Strong(t);
            }
            int synthetic = 100;
            foreach (var (t, refT, volts, on) in pullUps)
            {
                if (!on()) continue;
                int n = C.NetOf(t), rn = C.NetOf(refT);
                if (vals[n].known) continue;
                var rv = vals[rn];
                if (!rv.known) vals[rn] = rv = new Val { known = true, dom = synthetic++ };
                vals[n] = new Val { known = true, dom = rv.dom, dc = rv.dc + volts, ac = rv.ac, note = "input pull-up (no current)" };
            }
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                foreach (var (a, b) in loads)
                {
                    int na = C.NetOf(a), nb = C.NetOf(b);
                    var va = vals[na];
                    var vb = vals[nb];
                    if (va.known && !vb.known) { vals[nb] = Through(va, b); changed = true; }
                    else if (vb.known && !va.known) { vals[na] = Through(vb, a); changed = true; }
                }
                if (!changed) break;
            }
        }

        static Val Through(Val v, Terminal t)
        {
            v.note = $"through {t.dev.tag} (no current flows)";
            return v;
        }

        Val ValOf(Terminal t)
        {
            Compute();
            return vals[C.NetOf(t)];
        }

        static string Fmt(float v)
        {
            float a = Mathf.Abs(v);
            return a < 20f ? v.ToString("0.00") : a < 200f ? v.ToString("0.0") : v.ToString("0");
        }

        int readFrame = -1;
        Reading cached;
        Terminal cachedRed, cachedBlack;
        MeterMode cachedMode;

        /// The reading for this frame (the HUD, the beeper and the procedures share it).
        public Reading Read()
        {
            if (readFrame == Time.frameCount && cachedRed == red && cachedBlack == black && cachedMode == mode) return cached;
            readFrame = Time.frameCount;
            cachedRed = red;
            cachedBlack = black;
            cachedMode = mode;
            return cached = ReadNow();
        }

        Reading ReadNow()
        {
            if (red == null || black == null)
                return new Reading { value = "- - - -", unit = mode == MeterMode.Ohm ? "Ω" : "V", note = red == null ? "Place the red probe" : "Place the black probe" };
            bool same = C.Same(red, black);
            var a = ValOf(red);
            var b = ValOf(black);
            switch (mode)
            {
                case MeterMode.Ohm:
                {
                    if (Has(C.PotOf(red) | C.PotOf(black), LiveMask))
                        return new Reading { value = "LIVE", live = true, valid = true, note = "Voltage on the circuit. Switch off before an Ω test." };
                    if (same) return new Reading { value = "0.1", unit = "Ω", beep = true, valid = true, note = "continuity (beep)" };
                    return new Reading { value = "OL", unit = "Ω", ol = true, valid = true, note = "open (coils, lamps and the fan read OL here)" };
                }
                case MeterMode.VDC:
                case MeterMode.VAC:
                {
                    bool dcMode = mode == MeterMode.VDC;
                    string unit = dcMode ? "V DC" : "V AC";
                    if (same) return new Reading { value = "0.00", unit = unit, valid = true, note = "both probes on one net" };
                    if (!a.known || !b.known)
                        return new Reading { value = "0.00", unit = unit, valid = true, note = "open circuit: no supply on one probe" };
                    if (a.dom != b.dom)
                        return new Reading { value = "0.0", unit = unit, valid = true, note = "separate supplies with no common reference (reading drifts)" };
                    float dc = a.dc - b.dc;
                    float ac = (a.ac - b.ac).magnitude;
                    float v = dcMode ? dc : ac;
                    string note = a.note ?? b.note ?? "";
                    if (dcMode && ac > 5f && Mathf.Abs(dc) < 1f) note = "AC present: switch to V AC";
                    if (!dcMode && Mathf.Abs(dc) > 1f && ac < 1f) note = "DC present: switch to V DC";
                    if (dcMode && dc < -0.5f && note == "") note = "negative: the probes are swapped";
                    if (a.unstable || b.unstable) note = (note == "" ? "" : note + " · ") + "unstable";
                    return new Reading { value = Fmt(v), unit = unit, num = v, valid = true, note = note };
                }
            }
            return default;
        }

        public static string ModeName(MeterMode m) => m switch { MeterMode.VDC => "V DC", MeterMode.VAC => "V AC", _ => "Ω / beep" };
    }
}
