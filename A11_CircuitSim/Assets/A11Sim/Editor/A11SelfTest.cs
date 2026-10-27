using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace A11Sim.EditorTools
{
    /// Verifies the simulated circuit against the manual:
    ///   A. drawing geometry (every drawn wire really lands on its § 8 terminals, no false joins),
    ///   B. § 7.1 tests with no power (continuity),
    ///   C. § 7.2 power-up in steps,
    ///   D. functions (§ 4, § 5, § 7.3) and fault cases,
    ///   E. no short circuit between potentials in any state.
    /// Writes Verification/A11_SelfTest_Report.md and Verification/netlist.json (for verify_netlist.py).
    public static class A11SelfTest
    {
        class Report
        {
            public readonly StringBuilder md = new();
            public int pass, fail;
            public readonly List<string> fails = new();

            public void Section(string s) => md.AppendLine().AppendLine("## " + s).AppendLine().AppendLine("| Result | Check | Detail |").AppendLine("|---|---|---|");

            public bool Check(string name, bool ok, string detail = "")
            {
                if (ok) pass++;
                else
                {
                    fail++;
                    fails.Add(name + (detail != "" ? " — " + detail : ""));
                }
                md.AppendLine($"| {(ok ? "PASS" : "**FAIL**")} | {name} | {detail.Replace("|", "/")} |");
                return ok;
            }

            public void Info(string name, string detail) => md.AppendLine($"| info | {name} | {detail.Replace("|", "/")} |");
        }

        static string shortsFound = "";

        [MenuItem("A11 Sim/4 · Run circuit self-test")]
        public static void Run()
        {
            var r = new Report();
            Directory.CreateDirectory("Verification");
            try
            {
                Geometry(r);
                DeadTests(r);
                PowerUp(r);
                Functions(r);
                WiringRules(r);
                r.Section("E · No short circuit in any simulated state");
                r.Check("No net carries two different potentials (L/N, L/PE, AC/DC, +24/0 V, +48/48 V return)", shortsFound == "", shortsFound);
                ExportNetlist();
            }
            catch (Exception e)
            {
                r.Check("Self-test ran without exception", false, e.ToString().Replace("\n", " "));
            }

            var head = new StringBuilder();
            head.AppendLine("# A11 circuit self-test");
            head.AppendLine();
            head.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm} from `A11Layout.Build()` (the same model the app runs).");
            head.AppendLine();
            head.AppendLine($"**{r.pass} passed, {r.fail} failed.**");
            if (r.fail > 0)
            {
                head.AppendLine();
                foreach (var f in r.fails) head.AppendLine("- FAIL: " + f);
            }
            File.WriteAllText("Verification/A11_SelfTest_Report.md", head.ToString() + r.md);
            Debug.Log($"[A11] Self-test: {r.pass} passed, {r.fail} failed");
            foreach (var f in r.fails) Debug.Log("[A11] FAIL " + f);
            if (Application.isBatchMode) EditorApplication.Exit(r.fail == 0 ? 0 : 2);
        }

        // ------------------------------------------------------------------ helpers

        static Circuit New() => A11Layout.Build();
        static Terminal T(Circuit c, string id) => c.T(id);

        static void Step(Circuit c, float seconds, string label = "")
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / 0.02f));
            for (int i = 0; i < n; i++)
            {
                foreach (var d in c.devices) d.Tick(0.02f);
                c.Solve();
                ScanShorts(c, label);
            }
        }

        static void ScanShorts(Circuit c, string label)
        {
            if (shortsFound != "") return;
            const Pot DC = Pot.P24 | Pot.Z0 | Pot.P48 | Pot.Z48 | Pot.P5;
            foreach (var t in c.terminals)
            {
                var p = c.PotOf(t);
                string bad = null;
                if (Has(p, Pot.L) && Has(p, Pot.N)) bad = "L and N";
                else if (Has(p, Pot.L) && Has(p, Pot.PE)) bad = "L and PE";
                else if ((Has(p, Pot.L) || Has(p, Pot.N)) && (p & DC) != 0) bad = "mains and DC";
                else if (Has(p, Pot.P24) && Has(p, Pot.Z0)) bad = "+24 V and 0 V";
                else if (Has(p, Pot.P48) && Has(p, Pot.Z48)) bad = "+48 V and its return";
                else if (Has(p, Pot.P5) && Has(p, Pot.Z0)) bad = "+5 V and 0 V";
                else if ((Has(p, Pot.P48) || Has(p, Pot.Z48)) && (Has(p, Pot.P24) || Has(p, Pot.Z0) || Has(p, Pot.P5))) bad = "48 V side and 24 V side";
                if (bad != null)
                {
                    shortsFound = $"{bad} on the net of {t.id} ({label})";
                    return;
                }
            }
        }

        static bool Has(Pot p, Pot f) => (p & f) != 0;

        static void Hold(Circuit c, PushButton b, float s = 0.2f)
        {
            b.uiHold = true;
            Step(c, s, "holding " + b.tag);
            b.uiHold = false;
            Step(c, 0.1f, "released " + b.tag);
        }

        /// Machine on: QF0 ON, push ON.
        static Circuit Running()
        {
            var c = New();
            c.Get<Breaker>("QF0").closed = true;
            Step(c, 0.2f, "QF0 on");
            Hold(c, c.Get<PushButton>("S2"));
            return c;
        }

        // ------------------------------------------------------------------ A · geometry

        class UF
        {
            readonly int[] p;
            public UF(int n) { p = Enumerable.Range(0, n).ToArray(); }
            public int Find(int i) { while (p[i] != i) i = p[i] = p[p[i]]; return i; }
            public void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) p[a] = b; }
        }

        static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-8f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static bool OnPoly(Vector2 p, Vector2[] pts, bool interiorOnly = false)
        {
            if (interiorOnly && ((pts[0] - p).magnitude < 0.01f || (pts[^1] - p).magnitude < 0.01f)) return false;
            for (int i = 0; i < pts.Length - 1; i++)
                if (DistToSeg(p, pts[i], pts[i + 1]) < 0.01f) return true;
            return false;
        }

        static void Geometry(Report r)
        {
            var c = New();
            r.Section("A · Drawing geometry (the drawn lines agree with the netlist)");
            var uf = new UF(c.terminals.Count);
            foreach (var w in c.wires)
                if (!w.notConnected) uf.Union(w.a.index, w.b.index);
            foreach (var (fa, fb) in c.FixedLinks) uf.Union(fa.index, fb.index); // conductors inside one part (PSU screws, PE studs)
            int Net(Terminal t) => uf.Find(t.index);

            // A1 — wire ends land on their own terminal, or on a drawn conductor of the same net.
            var bad = new List<string>();
            foreach (var w in c.wires)
            {
                bool EndOk(Vector2 p, Terminal t)
                {
                    if ((p - t.pos).magnitude < 0.01f) return true;
                    foreach (var o in c.wires)
                        if (o != w && !o.notConnected && Net(o.a) == Net(t) && OnPoly(p, o.pts)) return true;
                    return false;
                }
                if (!EndOk(w.pts[0], w.a)) bad.Add($"{Name(w)} start ≠ {w.a.id}");
                // Stubs end free by design ("→ PE bar"); a bus ends free at its far end.
                if (!w.stub && !w.isBus && !EndOk(w.pts[^1], w.b)) bad.Add($"{Name(w)} end ≠ {w.b.id}");
            }
            r.Check("A1 Every wire starts and ends on its own terminal or on its own net", bad.Count == 0, bad.Count == 0 ? $"{c.wires.Count} conductors" : string.Join("; ", bad));

            // A2 — no conductor passes over a terminal of another net (would look connected).
            bad.Clear();
            foreach (var w in c.wires)
            foreach (var t in c.terminals)
            {
                if (t.hidden || t == w.a || t == w.b || Net(t) == Net(w.a)) continue;
                if (OnPoly(t.pos, w.pts)) bad.Add($"{Name(w)} touches {t.id}");
            }
            r.Check("A2 No conductor runs over a terminal of a different net", bad.Count == 0, string.Join("; ", bad));

            // A3 — no collinear overlap between different nets.
            bad.Clear();
            var segs = new List<(Wire w, Vector2 a, Vector2 b)>();
            foreach (var w in c.wires)
                for (int i = 0; i < w.pts.Length - 1; i++) segs.Add((w, w.pts[i], w.pts[i + 1]));
            for (int i = 0; i < segs.Count; i++)
            for (int j = i + 1; j < segs.Count; j++)
            {
                var (wa, a0, a1) = segs[i];
                var (wb, b0, b1) = segs[j];
                if (Net(wa.a) == Net(wb.a)) continue;
                if (Overlap(a0, a1, b0, b1)) bad.Add($"{Name(wa)} / {Name(wb)}");
            }
            r.Check("A3 No two nets are drawn on top of each other", bad.Count == 0, string.Join("; ", bad.Distinct()));

            // A4 — no wire end touches another net anywhere.
            bad.Clear();
            foreach (var w in c.wires)
            foreach (var p in new[] { w.pts[0], w.pts[^1] })
            foreach (var o in c.wires)
                if (o != w && Net(o.a) != Net(w.a) && OnPoly(p, o.pts)) bad.Add($"{Name(w)} end on {Name(o)}");
            r.Check("A4 No wire end touches a conductor of a different net", bad.Count == 0, string.Join("; ", bad.Distinct()));

            // A5 — visible terminals are distinct.
            var dup = c.terminals.Where(t => !t.hidden).GroupBy(t => new Vector2Int(Mathf.RoundToInt(t.pos.x * 100), Mathf.RoundToInt(t.pos.y * 100)))
                .Where(g => g.Count() > 1).Select(g => string.Join("=", g.Select(t => t.id))).ToList();
            r.Check("A5 No two terminals share one position", dup.Count == 0, string.Join("; ", dup));

            // A6 — completeness against the § 8 number list.
            var expected = new List<int>();
            for (int i = 101; i <= 132; i++) expected.Add(i);
            expected.Add(139); // enclosure body bond (rev 0.6)
            expected.Add(138); // link between the two 4-point earth bars (2026-10-26)
            for (int i = 140; i <= 149; i++) expected.Add(i);
            expected.RemoveAll(i => i is 107 or 108 or 110); // TB1 / TB2 gone; KM1 4T2 not used (N on the XN block, rev 0.7)
            for (int i = 150; i <= 155; i++) expected.Add(i);
            expected.AddRange(new[] { 162, 164, 166, 167, 168, 169, 170, 171 });
            expected.AddRange(new[] { 201, 202, 203, 204, 205, 206, 207, 210, 220, 221, 222, 223, 224, 225, 226 }); // 211 gone: 1-NC E-stop
            for (int i = 301; i <= 312; i++) expected.Add(i);
            for (int i = 315; i <= 320; i++) expected.Add(i);
            for (int i = 325; i <= 342; i++) if (i != 333) expected.Add(i);
            for (int i = 345; i <= 348; i++) expected.Add(i);
            var have = c.wires.Where(w => w.number != "").Select(w => int.Parse(w.number)).ToList();
            var missing = expected.Except(have).ToList();
            var extra = have.Except(expected).ToList();
            var dups = have.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            r.Check("A6 Every § 8 wire number is present exactly once", missing.Count == 0 && extra.Count == 0 && dups.Count == 0,
                $"{have.Count} numbered wires (rev 0.7 schedule: {expected.Count})" + (missing.Count > 0 ? " missing " + string.Join(",", missing) : "") +
                (extra.Count > 0 ? " extra " + string.Join(",", extra) : "") + (dups.Count > 0 ? " duplicated " + string.Join(",", dups) : ""));

            int crossings = 0;
            for (int i = 0; i < segs.Count; i++)
            for (int j = i + 1; j < segs.Count; j++)
                if (Net(segs[i].w.a) != Net(segs[j].w.a) && ProperCross(segs[i].a, segs[i].b, segs[j].a, segs[j].b)) crossings++;
            r.Info("Crossings without connection (normal in a schematic)", crossings.ToString());
        }

        static string Name(Wire w) => w.number != "" ? "wire " + w.number : w.isBus ? "bus " + w.a.id : "core " + w.a.id + "→" + w.b.id;

        static bool Overlap(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            Vector2 da = a1 - a0, db = b1 - b0;
            if (da.sqrMagnitude < 1e-6f || db.sqrMagnitude < 1e-6f) return false;
            float cross = da.x * db.y - da.y * db.x;
            if (Mathf.Abs(cross) > 1e-4f) return false;
            if (DistToLine(b0, a0, a1) > 0.01f) return false;
            Vector2 u = da.normalized;
            float s0 = 0, s1 = Vector2.Dot(da, u);
            float t0 = Vector2.Dot(b0 - a0, u), t1 = Vector2.Dot(b1 - a0, u);
            if (t0 > t1) (t0, t1) = (t1, t0);
            return Mathf.Min(s1, t1) - Mathf.Max(s0, t0) > 0.01f;
        }

        static float DistToLine(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            Vector2 v = p - a;
            return Mathf.Abs(v.x * d.y - v.y * d.x);
        }

        static bool ProperCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Orient(c, d, a), d2 = Orient(c, d, b), d3 = Orient(a, b, c), d4 = Orient(a, b, d);
            return d1 * d2 < -1e-6f && d3 * d4 < -1e-6f;
        }

        static float Orient(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        // ------------------------------------------------------------------ B · § 7.1 tests with no power

        static void DeadTests(Report r)
        {
            r.Section("B · § 7.1 tests with no power (Main CB OFF, meter on continuity)");
            var c = New();
            Step(c, 0.1f, "dead");
            var s0 = c.Get<EStop>("S0");
            var s1 = c.Get<PushButton>("S1");
            var s2 = c.Get<PushButton>("S2");
            var k1 = c.Get<RelayK1>("K1");
            bool Beep(string a, string b) => c.Same(T(c, a), T(c, b));

            r.Check("1 TB1 ↔ TB2 → open", !Beep("Z1.LOUT", "Z1.NOUT"));
            r.Check("2 TB1 ↔ TB0V, TB24+, PE → open", !Beep("Z1.LOUT", "G2.-V1") && !Beep("Z1.LOUT", "G2.+V1") && !Beep("Z1.LOUT", "PEBAR.BAR"));
            r.Check("3 TB0V ↔ PE → beep (PELV bond 148)", Beep("G2.-V1", "PEBAR.BAR"));
            r.Check("4 TB24C ↔ K1 A1, no button → open", !Beep("F1.out", "K1.A1"));
            s2.uiHold = true;
            Step(c, 0.05f, "ON held, dead");
            r.Check("5 ON pushed and held → beep", Beep("F1.out", "K1.A1"));
            s0.pressed = true;
            Step(c, 0.05f, "ON + E-stop, dead");
            r.Check("6 ON held and E-stop pushed → open", !Beep("F1.out", "K1.A1"));
            s0.pressed = false;
            s1.uiHold = true;
            Step(c, 0.05f, "ON + OFF, dead");
            r.Check("7 ON held and OFF pushed → open", !Beep("F1.out", "K1.A1"));
            s1.uiHold = s2.uiHold = false;
            Step(c, 0.05f, "dead");
            r.Check("8 QF4 out ↔ contactor A2 (K1 not energized) → open", !Beep("QF4.2", "KM1.A2"));
            k1.testLever = true;
            Step(c, 0.05f, "K1 test lever");
            r.Check("9 K1 test lever pushed: QF4 out ↔ contactor A2 → beep (pole 4)", Beep("QF4.2", "KM1.A2"));
            k1.testLever = false;
            Step(c, 0.05f, "dead");
            bool released = Beep("A1.X7", "G2.-V1");
            s0.pressed = true;
            Step(c, 0.05f, "E-stop pushed, dead");
            bool pushed = Beep("A1.X7", "G2.-V1");
            s0.pressed = false;
            var xt = c.Get<ToolSetter>("XT");
            xt.manualOver = true;
            Step(c, 0.05f, "tool-setter over-travel, dead");
            bool over = Beep("A1.X7", "G2.-V1");
            xt.manualOver = false;
            Step(c, 0.05f, "dead");
            r.Check("10 E-stop NC2: X7 ↔ TB0V → beep released, open pushed, open on tool-setter over-travel (347 in series)",
                released && !pushed && !over);
            r.Check("11 PSU 48V +V ↔ −V → not 0 Ω", !Beep("G1.+V1", "G1.-V1") && !Beep("G1.+V2", "G1.-V2") && !Beep("G1.+V3", "G1.-V3"));
            r.Check("12 TB24+ ↔ TB0V → not 0 Ω", !Beep("G2.+V1", "G2.-V1"));
            bool ok13 = true;
            foreach (var a in new[] { "X", "Y", "Z" })
            foreach (var s in new[] { "_5V", "_PUL", "_DIR" })
                ok13 &= !Beep("A1." + a + s, "G2.+V1") && !Beep("A1." + a + s, "G1.+V1");
            r.Check("13 5V, PULSE, DIR of each axis ↔ TB24+ and PSU 48V +V → open", ok13);
        }

        // ------------------------------------------------------------------ C · § 7.2 power-up in steps

        static void PowerUp(Report r)
        {
            r.Section("C · § 7.2 power-up in steps");
            var c = New();
            var qf0 = c.Get<Breaker>("QF0");
            var s0 = c.Get<EStop>("S0");
            var s1 = c.Get<PushButton>("S1");
            var s2 = c.Get<PushButton>("S2");
            var k1 = c.Get<RelayK1>("K1");
            var km1 = c.Get<Contactor>("KM1");
            var a11 = c.Get<A11Board>("A1");
            var h1 = c.Get<Lamp>("H1");
            qf0.closed = true;
            Step(c, 0.3f, "QF0 on");
            r.Check("Stage 1 / C2: TB24S +24 V, TB0V 0 V with the Main CB ON", c.Has(T(c, "F2.out"), Pot.P24) && c.Has(T(c, "G2.-V1"), Pot.Z0));
            r.Check("Stage 1: the A11 starts (J10 power)", a11.powered);
            r.Check("Stage 1: K1 and KM1 stay OFF until ON is pushed; L/N bus dead", !k1.on && !km1.on && !c.Has(T(c, "KM1.2T1"), Pot.L));
            Hold(c, s2);
            Step(c, 0.3f, "after ON");
            r.Check("5 Push ON: K1 energizes and stays energized (latch 11-14)", k1.on);
            Hold(c, s1);
            r.Check("6 Push OFF: K1 de-energizes", !k1.on);
            Hold(c, s2);
            s0.pressed = true;
            Step(c, 0.3f, "E-stop");
            bool off = !k1.on;
            s0.pressed = false;
            Step(c, 0.3f, "E-stop released");
            r.Check("7 ON, then E-stop: K1 de-energizes and stays de-energized after release", off && !k1.on);
            Hold(c, s2);
            Step(c, 0.2f, "on");
            r.Check("9 Push ON: contactor closes, yellow lamp on, L/N bus live",
                km1.on && h1.lit && c.Has(T(c, "XL.T"), Pot.L) && c.Has(T(c, "U1.N"), Pot.N));
            Hold(c, s1);
            r.Check("10 Push OFF: contactor opens, yellow lamp off", !km1.on && !h1.lit);
            Hold(c, s2);
            s0.pressed = true;
            Step(c, 0.2f, "E-stop");
            r.Check("10 Same test with the E-stop", !km1.on);
            s0.pressed = false;
            Hold(c, s2);
            var drives = new[] { c.Get<StepperDrive>("A2"), c.Get<StepperDrive>("A3"), c.Get<StepperDrive>("A4") };
            r.Check("11 / C3: QF2 ON → G1 48 V on the three drives, drive LEDs green", drives.All(d => d.powered && !d.fault));
            r.Check("C5: A11 5 V reaches PUL+ / DIR+ of each drive (common anode)", drives.All(d => d.signalOk));
            r.Check("Drives: 48 V side stays isolated from 0 V", !c.Same(T(c, "G1.-V1"), T(c, "G2.-V1")));
        }

        // ------------------------------------------------------------------ D · functions

        static void Functions(Report r)
        {
            r.Section("D · Functions (§ 3, § 4, § 5, § 7.3) and fault cases");

            // § 3: what the contactor switches.
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                var vfd = c.Get<Vfd>("U1");
                var pump = c.Get<SchukoPump>("XS1");
                var g1 = c.Get<Psu>("G1");
                bool onAll = vfd.powered && pump.running && g1.on;
                c.Get<EStop>("S0").pressed = true;
                Step(c, 0.3f, "E-stop while running");
                r.Check("§ 3: E-stop de-energizes VFD, 48 V drives and pump", onAll && !vfd.powered && !pump.running && !g1.on);
                r.Check("§ 3: A11 and the 24 V side stay energized, A11 sees the E-stop", a11.powered && a11.Alarm == "E-STOP" && a11.Y[5]);
                r.Check("§ 4.3: red ALARM lamp lit (Y5 sinks lamp −)", c.Get<Lamp>("H3").lit);
            }

            // OFF must not raise the A11 alarm (only the E-stop does).
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                Hold(c, c.Get<PushButton>("S1"));
                Step(c, 0.3f, "after OFF");
                r.Check("§ 4.2 / 5.1: OFF drops K1 and KM1 but X7 stays closed: no A11 alarm, red lamp off",
                    !c.Get<RelayK1>("K1").on && !c.Get<Contactor>("KM1").on && a11.X[7] && a11.Alarm != "E-STOP" && !c.Get<Lamp>("H3").lit,
                    $"A11 alarm: {a11.Alarm ?? "none"}");
            }

            // § 4.2: mains failure.
            {
                var c = Running();
                var qf0 = c.Get<Breaker>("QF0");
                var k1 = c.Get<RelayK1>("K1");
                qf0.closed = false;
                Step(c, 0.3f, "mains off");
                qf0.closed = true;
                Step(c, 0.5f, "mains back");
                r.Check("§ 4.2: after a mains failure K1 stays OFF — no automatic restart", !k1.on && c.Get<A11Board>("A1").powered);
            }

            // Fuses and the 24 V branch breaker.
            {
                var c = Running();
                c.Get<Fuse>("F1").inserted = false;
                Step(c, 0.3f, "F1 out");
                r.Check("F1 open: K1 / KM1 drop, lamps off, A11 keeps running (F2)", !c.Get<RelayK1>("K1").on && !c.Get<Contactor>("KM1").on && c.Get<A11Board>("A1").powered);
                c = Running();
                c.Get<Fuse>("F2").inserted = false;
                Step(c, 0.3f, "F2 out");
                var a11 = c.Get<A11Board>("A1");
                r.Check("F2 open: A11 and sensors lose power", !a11.powered && !c.Get<SensorConnector>("X-LIM").powered);
                c = Running();
                c.Get<Breaker>("QF4").closed = false;
                Step(c, 0.3f, "QF4 off");
                r.Check("QF4 open: G2 off → K1 drops → KM1 opens", !c.Get<Psu>("G2").on && !c.Get<RelayK1>("K1").on && !c.Get<Contactor>("KM1").on);
            }

            // § 7.3 speed table and § 7.4 test 4 bit order.
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                var vfd = c.Get<Vfd>("U1");
                float[] hz = { 100f, 133.3f, 166.7f, 200f, 250f, 300f, 350f, 400f };
                bool ok = true;
                var detail = new StringBuilder();
                for (int g = 1; g <= 8; g++)
                {
                    a11.SetGrade(g);
                    if (!a11.spindleOn) a11.ToggleSpindle();
                    Step(c, 3.5f, "spindle S" + g);
                    string bitsY = $"{(a11.Y[4] ? 1 : 0)}{(a11.Y[3] ? 1 : 0)}{(a11.Y[2] ? 1 : 0)}";
                    string expect = Vfd.Bits3(g - 1);
                    bool okG = vfd.run && vfd.bits == g - 1 && Mathf.Abs(vfd.freq - hz[g - 1]) < 0.6f && bitsY == expect;
                    ok &= okG;
                    detail.Append($"S{g}: Y4Y3Y2 {bitsY} → {vfd.freq:0.0} Hz {vfd.Rpm:0} rpm{(okG ? "" : " ✗")}; ");
                }
                r.Check("§ 7.3 / test 4: S1…S8 → Y4Y3Y2 000…111 → P030…P037 frequency", ok, detail.ToString());
                c.Get<EStop>("S0").pressed = true;
                Step(c, 0.3f, "E-stop with spindle");
                r.Check("Stage 4 step 19: the E-stop stops the spindle (Y1 off, VFD unpowered, coasting)", !a11.Y[1] && !vfd.powered && !vfd.run);
                r.Info("Note", "On E-stop the VFD loses its supply, so the spindle coasts (no braking through R1). See findings.");
            }

            // § 5.1 inputs.
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                var map = new (string conn, int sensor, int input, string name)[]
                {
                    ("X-LIM", 0, 1, "B1 X home → X1"), ("X-LIM", 1, 6, "B2 X+ limit → X6"),
                    ("Y-LIM", 0, 2, "B3 Y home → X2"), ("Y-LIM", 1, 6, "B4 Y+ limit → X6"),
                    ("Z-LIM", 0, 3, "B5 Z home → X3"), ("Z-LIM", 1, 6, "B6 Z− limit → X6"),
                };
                var detail = new StringBuilder();
                bool ok = true;
                foreach (var (conn, si, input, name) in map)
                {
                    var s = c.Get<SensorConnector>(conn).sensors[si];
                    bool before = a11.X[input];
                    s.manual = true;
                    Step(c, 0.1f, name);
                    bool after = a11.X[input];
                    bool alarm = input != 6 || (a11.Alarm == "HARD LIMIT" && c.Get<Lamp>("H3").lit);
                    s.manual = false;
                    Step(c, 0.1f, name + " released");
                    bool okS = !before && after && alarm && !a11.X[input];
                    ok &= okS;
                    detail.Append(name + (okS ? " ✓; " : " ✗; "));
                }
                r.Check("§ 5.1: each NPN sensor activates its own input; far limits wire-OR on X6 → alarm + red lamp", ok, detail.ToString());
                var ts = c.Get<ToolSetter>("XT");
                ts.manual = true;
                Step(c, 0.1f, "tool touch");
                bool x4 = a11.X[4];
                ts.manual = false;
                Step(c, 0.1f, "tool release");
                r.Check("§ 5.1: tool-setter touch → X4; X8 never active (not connected)", x4 && !a11.X[4] && !a11.X[8]);
                r.Check("§ 5.1: E-stop released → X7 active (NC level)", a11.X[7]);
                ts.manualOver = true;
                Step(c, 0.1f, "tool-setter over-travel");
                bool overStop = !a11.X[7] && a11.Alarm == "E-STOP" && c.Get<RelayK1>("K1").on;
                ts.manualOver = false;
                Step(c, 0.1f, "over-travel released");
                r.Check("Rev 0.7: tool-setter over-travel (347 in series with E-stop NC2) → X7 opens → A11 E-STOP; K1 stays on", overStop && a11.X[7]);
                foreach (var tag in new[] { "A2", "A3", "A4" })
                {
                    var d = c.Get<StepperDrive>(tag);
                    d.fault = true;
                    Step(c, 0.1f, tag + " fault");
                    bool a = a11.X[5] && a11.Alarm == "DRIVER ALARM" && c.Get<Lamp>("H3").lit;
                    d.fault = false;
                    Step(c, 0.1f, tag + " ok");
                    r.Check($"§ 5.1: {tag} ALM → X5 driver alarm (parallel, X5 = NO assumed)", a && !a11.X[5]);
                }
            }

            // Lamps and homing.
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                var m = c.machine;
                a11.HomeAll();
                for (int i = 0; i < 2000 && a11.mode == A11Board.Mode.Homing; i++) Step(c, 0.02f, "homing");
                r.Check("Stage 5 step 22: home the three axes (Z+, X−, Y− sensors)", a11.homed && Mathf.Abs(m.pos[0]) < 6f && Mathf.Abs(m.pos[1]) < 6f && Mathf.Abs(m.pos[2]) < 6f,
                    $"X {m.pos[0]:0.00} Y {m.pos[1]:0.00} Z {m.pos[2]:0.00} · {a11.message}");
                a11.RunJob();
                Step(c, 0.5f, "job");
                r.Check("§ 4.3: green WORK lamp lit while a job runs (Y6)", c.Get<Lamp>("H2").lit && a11.Y[6]);
                c.Get<SensorConnector>("X-LIM").sensors[1].manual = true;
                Step(c, 0.1f, "hard limit during job");
                r.Check("Stage 5 step 23: a hard-limit sensor stops the job and shows an alarm", a11.mode != A11Board.Mode.Job && a11.Alarm == "HARD LIMIT");
            }

            // Open loop: drive without 48 V.
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                c.Get<Breaker>("QF2").closed = false;
                Step(c, 0.2f, "QF2 off");
                float x0 = c.machine.pos[0], d0 = a11.dro[0];
                a11.jog[0] = 1;
                Step(c, 1f, "jog without 48 V");
                a11.jog[0] = 0;
                r.Check("QF2 OFF: A11 still sends pulses (DRO moves) but the axis does not move", a11.dro[0] > d0 + 10 && Mathf.Abs(c.machine.pos[0] - x0) < 0.01f);
            }

            // J8 24 V supply for the inputs.
            {
                var c = Running();
                var a11 = c.Get<A11Board>("A1");
                r.Check("§ 5.1: J8 24V (wire 168) supplies the input stage", a11.sensorSupply);
            }
        }

        // ------------------------------------------------------------------ F · wiring rules (rev 0.7 direct wiring)

        static void WiringRules(Report r)
        {
            r.Section("F · Wiring rules (rev 0.7: wired directly; no terminal blocks, only the 230 V L / N distribution blocks)");
            var c = New();
            var count = new Dictionary<string, List<string>>();
            foreach (var w in c.wires)
            {
                if (w.number == "" || w.notConnected) continue;
                foreach (var t in new[] { w.a.id, w.b.id })
                {
                    if (!count.TryGetValue(t, out var l)) count[t] = l = new List<string>();
                    l.Add(w.number);
                }
            }
            var multi = new Dictionary<string, int> { ["PEBAR.BAR"] = 8, ["PEBAR.BAR2"] = 8, ["XL.T"] = 12, ["XN.T"] = 12 }; // earth bars: 4 points × 2 (owner decision); busbars: 12 points
            var over = count.Where(kv => kv.Value.Count > (multi.TryGetValue(kv.Key, out int cap) ? cap : 2))
                .Select(kv => $"{kv.Key} ({string.Join(",", kv.Value)})").ToList();
            r.Check("F1 Max. 2 conductors per device terminal (twin ferrule); earth bars ≤ 8 each (4 points × 2, owner decision), XL / XN busbars ≤ 12 (one per point)", over.Count == 0,
                over.Count == 0 ? $"{count.Count(kv => kv.Value.Count == 2)} terminals take 2 conductors (twin ferrule)" : string.Join("; ", over));
            var tb = c.terminals.Where(t => t.id.StartsWith("TB") || t.id.Contains("BUS")).Select(t => t.id).ToList();
            r.Check("F2 No terminal blocks or virtual bus nodes in the netlist (only the L / N busbars XL, XN)", tb.Count == 0, string.Join(", ", tb));
            var uf = new UF(c.terminals.Count);
            foreach (var w in c.wires)
                if (!w.notConnected) uf.Union(w.a.index, w.b.index);
            foreach (var (fa, fb) in c.FixedLinks)
                if (fa.dev.tag is "U1" or "G2") uf.Union(fa.index, fb.index); // VFD PE studs, PSU −V screws
            string[] pe = { "Mains.PE", "Z1.GND", "U1.PE", "G1.PE", "G2.PE", "X1.PE", "XS1.PE", "BKP.T", "DOOR.T", "RAIL.T", "BODY.T", "G2.-V1" };
            var missingPe = pe.Where(t => uf.Find(c.T(t).index) != uf.Find(c.T("PEBAR.BAR").index)).ToList();
            r.Check("F3 Every PE point (incl. the pump outlet, wire 149, and the enclosure body, wire 139) and the PELV bond reach the earth bar",
                missingPe.Count == 0, string.Join(", ", missingPe));
            var cc = Running();
            cc.Get<Fuse>("F1").inserted = false;
            Step(cc, 0.2f, "F1 out, fan");
            r.Check("F4 The fan is protected by F1 (rev 0.5: unfused on TB24+)", !cc.Get<Fan>("M2").running);
            cc = Running();
            cc.Get<Breaker>("QF3").closed = true;
            Step(cc, 0.2f, "pump");
            r.Check("F5 The pump outlet and the 48 V PSU each take N from the N block XN (no N looped through the outlet)",
                cc.Get<SchukoPump>("XS1").running && cc.Get<Psu>("G1").on && c.wires.Count(w => w.number is "122" or "119" && w.a.id == "XN.T") == 2);
            // F6: sensor 0 V must not depend on the drive ALM screws (unplugging a drive signal connector).
            var uf6 = new UF(c.terminals.Count);
            foreach (var w in c.wires)
                if (!w.notConnected && !w.a.id.Contains(".ALM") && !w.b.id.Contains(".ALM")) uf6.Union(w.a.index, w.b.index);
            foreach (var (fa, fb) in c.FixedLinks)
                if (fa.dev.tag is "G2" or "A1") uf6.Union(fa.index, fb.index);
            var lost = new[] { "X-LIM.3", "Y-LIM.3", "Z-LIM.3", "XT.2", "XT.4" }.Where(t => uf6.Find(c.T(t).index) != uf6.Find(c.T("G2.-V1").index)).ToList();
            r.Check("F6 Sensor 0 V reaches every sensor connector without passing through a drive ALM screw", lost.Count == 0, string.Join(", ", lost));
            // F7 / F8: KM1 coil circuit behind QF4 (C6); 230 V only on K1 pole 4, pole 3 empty.
            bool coilFeed = c.wires.Any(w => w.number == "130" && w.a.id == "QF4.2" && w.b.id == "K1.41");
            r.Check("F7 KM1 coil circuit fed from QF4 out (C6), not from the unprotected QF4 input", coilFeed);
            cc = Running();
            bool sep = cc.Has(T(cc, "K1.41"), Pot.L) && !new[] { "K1.11", "K1.14" }.Any(t => cc.Has(T(cc, t), Pot.L) || cc.Has(T(cc, t), Pot.N)) &&
                       !c.terminals.Any(t => t.id is "K1.21" or "K1.24" or "K1.31" or "K1.34");
            r.Check("F8 230 V only on K1 pole 4; pole 1 carries 24 V only; poles 2 and 3 empty (pole 3 = gap)", sep);
            r.Info("Cross-section vs protection", "see Verification/A11_WiringRules_rev07.md (IEC 60204-1 Table 6)");
        }

        // ------------------------------------------------------------------ export

        static void ExportNetlist()
        {
            var c = New();
            var sb = new StringBuilder("[\n");
            bool first = true;
            foreach (var w in c.wires)
            {
                if (w.number == "") continue;
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append($"  {{\"no\": \"{w.number}\", \"from\": \"{w.a.id}\", \"to\": \"{w.b.id}\", \"kind\": \"{w.kind}\", \"spec\": \"{Esc(w.spec)}\", \"desc\": \"{Esc(w.desc)}\", \"notConnected\": {(w.notConnected ? "true" : "false")}}}");
            }
            sb.Append("\n]\n");
            File.WriteAllText("Verification/netlist.json", sb.ToString());
            File.WriteAllText("Verification/netlist_rev07.json", sb.ToString());
        }

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
