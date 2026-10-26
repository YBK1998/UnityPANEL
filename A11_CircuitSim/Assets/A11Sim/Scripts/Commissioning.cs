using System;
using System.Collections.Generic;
using UnityEngine;

namespace A11Sim
{
    /// § 7.1 and § 7.2 as click-through procedures with the multimeter.
    /// Terminal names follow rev 0.7 (direct wiring); the rev 0.5 terminal-block name is given in brackets.
    public class Commissioning
    {
        public class Step
        {
            public string title, text;
            public string red, black;          // measure step: probe targets (any terminal on the same conductor counts)
            public MeterMode mode;
            public Func<Multimeter.Reading, bool> expect;
            public string expectText;
            public Func<bool> pre;             // must hold while measuring (e.g. ON held)
            public string preText;
            public Func<bool> done;            // action step
            public Action enter;
            public bool lever;                 // offer a "hold K1 test lever" button
            public bool passed;
            public bool IsMeasure => red != null;
        }

        public static readonly string[] Names = { "§ 7.1 Tests with no power", "§ 7.2 Power-up in steps" };

        public int proc = -1;
        public int index;
        public List<Step> steps = new();
        public bool Active => proc >= 0;
        public Step Current => Active && index < steps.Count ? steps[index] : null;
        public bool Finished => Active && index >= steps.Count;

        readonly Circuit C;
        readonly Multimeter meter;
        readonly Breaker qf0, qf1, qf2, qf3;
        readonly EStop s0;
        readonly PushButton s1, s2;
        readonly RelayK1 k1;
        readonly ToolSetter xt;
        readonly Contactor km1;
        readonly Lamp h1;
        readonly A11Board a11;
        readonly Vfd vfd;
        readonly StepperDrive[] drives;

        public Commissioning(Circuit c, Multimeter m)
        {
            C = c;
            meter = m;
            qf0 = c.Get<Breaker>("QF0");
            qf1 = c.Get<Breaker>("QF1");
            qf2 = c.Get<Breaker>("QF2");
            qf3 = c.Get<Breaker>("QF3");
            s0 = c.Get<EStop>("S0");
            s1 = c.Get<PushButton>("S1");
            s2 = c.Get<PushButton>("S2");
            k1 = c.Get<RelayK1>("K1");
            xt = c.Get<ToolSetter>("XT");
            km1 = c.Get<Contactor>("KM1");
            h1 = c.Get<Lamp>("H1");
            a11 = c.Get<A11Board>("A1");
            vfd = c.Get<Vfd>("U1");
            drives = new[] { c.Get<StepperDrive>("A2"), c.Get<StepperDrive>("A3"), c.Get<StepperDrive>("A4") };
        }

        public void Start(int p)
        {
            proc = p;
            steps = p == 0 ? DeadTests() : PowerUp();
            index = 0;
            meter.active = true;
            steps[0].enter?.Invoke();
        }

        public void Stop() => proc = -1;

        public void Next()
        {
            if (!Active || index >= steps.Count) return;
            index++;
            if (index < steps.Count) steps[index].enter?.Invoke();
        }

        public void Prev()
        {
            if (!Active || index == 0) return;
            index--;
            steps[index].passed = false;
            steps[index].enter?.Invoke();
        }

        /// Put the probes on the targets and set the mode.
        public void ShowMe()
        {
            var s = Current;
            if (s == null || !s.IsMeasure) return;
            meter.red = C.T(s.red);
            meter.black = C.T(s.black);
            meter.mode = s.mode;
            meter.nextBlack = false;
        }

        public IEnumerable<Terminal> Targets()
        {
            var s = Current;
            if (s == null || !s.IsMeasure || s.passed) yield break;
            yield return C.T(s.red);
            yield return C.T(s.black);
        }

        public void Update()
        {
            var s = Current;
            if (s == null || s.passed) return;
            if (s.pre != null && !s.pre()) return;
            if (s.IsMeasure)
            {
                if (meter.mode != s.mode || !ProbesOn(s, out _)) return;
                var r = meter.Read();
                if (r.valid && s.expect(r)) s.passed = true;
            }
            else if (s.done != null && s.done()) s.passed = true;
        }

        bool ProbesOn(Step s, out bool swapped)
        {
            swapped = false;
            if (meter.OnConductor(meter.red, s.red) && meter.OnConductor(meter.black, s.black)) return true;
            if (meter.OnConductor(meter.red, s.black) && meter.OnConductor(meter.black, s.red))
            {
                swapped = true;
                return s.mode != MeterMode.VDC; // polarity matters only for DC
            }
            return false;
        }

        /// What the user still has to do for the current step.
        public string Todo()
        {
            var s = Current;
            if (s == null) return "";
            if (s.passed) return "<color=#7dffa0><b>PASS</b></color> — press Next";
            if (s.IsMeasure)
            {
                if (!meter.OnConductor(meter.red, s.red) && !meter.OnConductor(meter.red, s.black)) return "Put the <color=#ff6a5a>red</color> probe on " + Name(s.red);
                if (!ProbesOn(s, out bool sw))
                {
                    if (sw) return "Polarity: swap the probes (red on " + Name(s.red) + ")";
                    return "Put the <color=#c8d0dc>black</color> probe on " + Name(meter.OnConductor(meter.red, s.red) ? s.black : s.red);
                }
                if (meter.mode != s.mode) return "Set the meter to " + Multimeter.ModeName(s.mode);
            }
            if (s.pre != null && !s.pre()) return s.preText;
            if (s.IsMeasure) return "Reading does not match. Expected: " + s.expectText;
            return "Waiting: " + s.text;
        }

        string Name(string id)
        {
            var t = C.T(id);
            return $"<b>{t.dev.tag} {t.name}</b>";
        }

        // ------------------------------------------------------------------ helpers

        static bool Beep(Multimeter.Reading r) => r.beep;
        static bool Open(Multimeter.Reading r) => r.ol;
        static Func<Multimeter.Reading, bool> Between(float lo, float hi) => r => !r.beep && !r.ol && r.num >= lo && r.num <= hi;

        static Step Ohm(string title, string text, string red, string black, bool beep, Func<bool> pre = null, string preText = null) => new()
        {
            title = title, text = text, red = red, black = black, mode = MeterMode.Ohm,
            expect = beep ? Beep : Open, expectText = beep ? "beep (≈ 0 Ω)" : "OL (open)", pre = pre, preText = preText,
        };

        static Step Act(string title, string text, Func<bool> done, Action enter = null) => new() { title = title, text = text, done = done, enter = enter };

        // ------------------------------------------------------------------ § 7.1

        List<Step> DeadTests()
        {
            Func<bool> dead = () => !qf0.closed;
            const string deadText = "Switch the Main CB QF0 OFF (Q).";
            var l = new List<Step>
            {
                Act("Prepare", "Set the Main CB QF0 to OFF. Set the meter to Ω / continuity.", () => !qf0.closed && meter.mode == MeterMode.Ohm),
                Ohm("Test 1", "Line ↔ neutral after the filter: Z1 LOUT (TB1) ↔ Z1 NOUT (TB2). Open, or a high value through the filter bleed resistor.",
                    "Z1.LOUT", "Z1.NOUT", false, dead, deadText),
                Ohm("Test 2a", "220 V must not touch 0 V: Z1 LOUT (TB1) ↔ G2 −V (TB0V).", "Z1.LOUT", "G2.-V1", false, dead, deadText),
                Ohm("Test 2b", "220 V must not touch +24 V: Z1 LOUT (TB1) ↔ G2 +V (TB24+).", "Z1.LOUT", "G2.+V1", false, dead, deadText),
                Ohm("Test 2c", "220 V must not touch earth: Z1 LOUT (TB1) ↔ earth bar (PE).", "Z1.LOUT", "PEBAR.BAR", false, dead, deadText),
                Ohm("Test 3", "PELV bond: G2 −V (TB0V) ↔ earth bar. Wire 148 must beep.", "G2.-V1", "PEBAR.BAR", true, dead, deadText),
                Ohm("Test 4", "Control chain, no button pushed: F1 out (TB24C) ↔ K1 A1.", "F1.out", "K1.A1", false,
                    () => dead() && !s2.pressed && !s1.pressed && !s0.pressed, "Release all buttons (and the E-stop)."),
                Ohm("Test 5", "Same probes, ON pushed and held: the chain must beep.", "F1.out", "K1.A1", true,
                    () => dead() && s2.pressed && !s0.pressed && !s1.pressed, "Hold ON (key 1, or the green button)."),
                Ohm("Test 6", "Same probes, ON held and E-stop pushed: open.", "F1.out", "K1.A1", false,
                    () => dead() && s2.pressed && s0.pressed, "Push the E-stop (E), then hold ON (key 1)."),
                Ohm("Test 7", "Same probes, ON held and OFF pushed: open.", "F1.out", "K1.A1", false,
                    () => dead() && s2.pressed && s1.pressed && !s0.pressed, "Release the E-stop (E). Hold ON (1) and OFF (0) together."),
                Ohm("Test 8", "Contactor coil feed, K1 not energized: QF4 out ↔ KM1 A2 (K1 pole 4, wires 130 / 131).", "QF4.2", "KM1.A2", false,
                    () => dead() && !k1.testLever, "Release the K1 test lever."),
                Ohm("Test 9", "Push the K1 test lever: QF4 out ↔ KM1 A2 must beep.", "QF4.2", "KM1.A2", true,
                    () => dead() && k1.testLever, "Hold the K1 test lever (button below, or the orange lever on K1 in 3D)."),
                Ohm("Test 10a", "E-stop NC2, released: A11 X7 ↔ G2 −V (TB0V) beeps (NC2 and the tool-setter over-travel NC in series, wire 347).", "A1.X7", "G2.-V1", true,
                    () => dead() && !s0.pressed && !xt.manualOver, "Release the E-stop (E) and the tool-setter over-travel switch."),
                Ohm("Test 10b", "E-stop NC2, pushed: A11 X7 ↔ G2 −V is open.", "A1.X7", "G2.-V1", false,
                    () => dead() && s0.pressed, "Push the E-stop (E)."),
                Ohm("Test 10c", "E-stop released, push the tool-setter over-travel switch: A11 X7 ↔ G2 −V is open.", "A1.X7", "G2.-V1", false,
                    () => dead() && !s0.pressed && xt.manualOver, "Release the E-stop (E) and click the over-travel contact of XT (schematic E3)."),
                Ohm("Test 11", "48 V PSU output not shorted: G1 +V ↔ G1 −V. Not 0 Ω.", "G1.+V1", "G1.-V1", false, dead, deadText),
                Ohm("Test 12", "24 V PSU output not shorted: G2 +V (TB24+) ↔ G2 −V (TB0V). Not 0 Ω.", "G2.+V1", "G2.-V1", false, dead, deadText),
                Ohm("Test 13a", "5 V side isolated from 24 V: A11 X 5V ↔ G2 +V (TB24+). Open. (Repeat for Y and Z on the real box.)",
                    "A1.X_5V", "G2.+V1", false, dead, deadText),
                Ohm("Test 13b", "Pulse line isolated from 48 V: A11 X PUL ↔ G1 +V. Open.", "A1.X_PUL", "G1.+V1", false, dead, deadText),
            };
            l[6].enter = () => s0.pressed = false;
            l[10].lever = l[11].lever = true;
            l[15].enter = () => xt.manualOver = false;
            return l;
        }

        // ------------------------------------------------------------------ § 7.2

        List<Step> PowerUp()
        {
            bool sawOn = false;
            var seen = new bool[3];
            return new List<Step>
            {
                Act("Stage 1 · prepare", "§ 7.1 is complete. Set QF1, QF2 and QF3 to OFF. (Real box: also disconnect wires 131, 166 and 168.)",
                    () => !qf1.closed && !qf2.closed && !qf3.closed),
                Act("Stage 1 · power", "Set the Main CB QF0 to ON (Q). Do not touch the terminals: 220 V is on the backplate.", () => qf0.closed),
                new()
                {
                    title = "Caution C2", text = "Measure F2 out (TB24S) to G2 −V (TB0V): 23–25 V DC with F2 out positive. If not, do not connect the A11.",
                    red = "F2.out", black = "G2.-V1", mode = MeterMode.VDC, expect = Between(23f, 25f), expectText = "23–25 V DC",
                    pre = () => qf0.closed, preText = "Main CB QF0 ON (Q).",
                },
                Act("Stage 1 · A11", "The A11 starts (handheld LCD on).", () => a11.powered),
                Act("Step 5", "Push ON (hold key 1): K1 energizes and stays energized.", () => k1.on),
                Act("Step 6", "Push OFF (key 0): K1 de-energizes.", () => !k1.on),
                Act("Step 7", "Push ON again, then push the E-stop (E): K1 de-energizes and stays off.",
                    () => { if (k1.on) sawOn = true; return sawOn && s0.pressed && !k1.on; }, () => sawOn = false),
                Act("Stage 2 · step 8", "Release the E-stop (E). (Real box: Main CB OFF, connect wire 131, Main CB ON.)", () => !s0.pressed),
                Act("Step 9", "Push ON: the contactor closes and the yellow lamp comes on.", () => km1.on && h1.lit),
                new()
                {
                    title = "Check", text = "The switched 230 V after KM1 is live: XL (L block) ↔ XN (N block) reads ≈ 230 V AC.",
                    red = "XL.T", black = "XN.T", mode = MeterMode.VAC, expect = Between(207f, 253f), expectText = "207–253 V AC",
                    pre = () => km1.on, preText = "Push ON so KM1 closes.",
                },
                Act("Step 10", "Push OFF: the contactor opens.", () => !km1.on),
                Act("Stage 3 · prepare", "Push ON again and set QF2 ON. (Real box: disconnect 150–155 at the drives first.)", () => km1.on && qf2.closed),
                new()
                {
                    title = "Caution C3", text = "Measure the 48 V PSU output G1 +V ↔ G1 −V: 46–50 V DC. If not, adjust G1.",
                    red = "G1.+V1", black = "G1.-V1", mode = MeterMode.VDC, expect = Between(46f, 50f), expectText = "46–50 V DC",
                    pre = () => km1.on && qf2.closed, preText = "Machine ON and QF2 ON.",
                },
                Act("Step 11", "Drive LEDs are green on X, Y and Z.", () => Array.TrueForAll(drives, d => d.powered && !d.fault)),
                Act("Step 12", "Jog each axis at low speed with the handheld (X, Y and Z).",
                    () =>
                    {
                        for (int i = 0; i < 3; i++) if (Mathf.Abs(a11.vel[i]) > 0.01f) seen[i] = true;
                        return seen[0] && seen[1] && seen[2];
                    }, () => Array.Clear(seen, 0, 3)),
                Act("Stage 4 · step 15", "Set QF1 ON: the VFD powers up.", () => qf1.closed && vfd.powered),
                new()
                {
                    title = "Caution C7", text = "Measure VFD X1 to VFD COM: 24 V DC or less, X1 positive (spindle off).",
                    red = "U1.X1", black = "U1.COM", mode = MeterMode.VDC, expect = Between(0.5f, 25f), expectText = "0.5–25 V DC",
                    pre = () => vfd.powered && !a11.spindleOn, preText = "VFD powered (QF1 ON) and spindle off.",
                },
                new()
                {
                    title = "Caution C7", text = "Measure VFD COM to G2 −V (TB0V): less than 1 V DC.",
                    red = "U1.COM", black = "G2.-V1", mode = MeterMode.VDC, expect = r => !r.ol && Mathf.Abs(r.num) < 1f, expectText = "< 1 V DC",
                    pre = () => vfd.powered, preText = "VFD powered (QF1 ON).",
                },
                Act("Spindle", "Start the spindle (Space) and check the VFD runs at the S-grade frequency.", () => a11.spindleOn && vfd.freq > 50f),
            };
        }
    }
}
