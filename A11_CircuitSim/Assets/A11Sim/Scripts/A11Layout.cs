using TMPro;
using UnityEngine;

namespace A11Sim
{
    using TA = TextAlignmentOptions;
    using K = WireKind;

    /// Sheets E1–E4 of the United Box A11 manual (rev 0.5) merged into one schematic.
    /// Wire numbers, ends and specs follow § 8. Coordinates are schematic units (y up).
    public static class A11Layout
    {
        public static Circuit Build()
        {
            var c = new Circuit { bounds = Rect.MinMaxRect(-4, -2, 372, 314) };
            var m = new Machine();
            c.machine = m;

            // ================= E1 — 220 V power distribution =================
            c.Add(new MainsSupply());
            c.Add(new Breaker("QF0", "Main CB C25 2P", "Incomer. Switches L and N. Every conductor behind QF0 (103–106, 109, 111, 114, 117, 120) is 4 mm² (25 A). REMINDER C14 (open): the FC300's 11 A is its OUTPUT current; it draws ≈ 22–24 A input at full load, so C25 is marginal with everything at full load (≈ 28–30 A).",
                288, 280, false, new Vector2(17, 284), TA.Left, 10, 14));
            c.Add(new EmiFilter());
            c.Add(new Breaker("QF4", "CB-24V C6A", "24 V PSU branch — on the UNSWITCHED side so the A11 stays on and senses the E-stop.",
                246, 238, true, new Vector2(27.6f, 242), TA.Right, 30));
            var g2 = c.Add(new Psu("G2", "G2 — PSU 24 V / 20 A", "Power for the A11, sensors, panel, K1 and fan. −V is bonded to PE once (wire 148, PELV).",
                Rect.MinMaxRect(26, 218, 48, 230), "G2  PSU 24 V / 20 A", "unswitched",
                new Vector2(30, 230), new Vector2(38, 230), new Vector2(48, 224),
                new[] { new Vector2(42, 218), new Vector2(46, 218) }, new[] { new Vector2(30, 218), new Vector2(34, 218) }, Pot.P24, Pot.Z0));
            c.Add(new RelayK1());
            c.Add(new Contactor());
            c.Add(new Breaker("QF1", "CB-VFD C16A", "VFD branch. REMINDER C14 (open): the FC300 nameplate's 11 A is the OUTPUT current; input ≈ 22–24 A at full spindle load, so C16 trips on long cuts near 2.2 kW (light cuts ≈ 10–12 A are fine). Z1 is fixed at 20 A (no larger filter available), so a bigger QF1 does not help: cap the spindle at ≈ 1.5 kW (≈ 15 A input) in the VFD instead — C16 then fits.", 202, 194, true, new Vector2(134, 198), TA.Right, 136));
            var vfd = c.Add(new Vfd());
            var r1 = c.Add(new BrakeResistor());
            r1.vfd = vfd;
            c.Add(new SpindleConnector());
            var m1 = c.Add(new SpindleMotor());
            m1.vfd = vfd;
            c.Add(new Breaker("QF2", "CB-48V C10A", "48 V PSU branch (drives).", 202, 194, true, new Vector2(332, 198), TA.Right, 334));
            c.Add(new Psu("G1", "G1 — PSU 48 V / 15 A", "Power for the three DMA860H drives. Measure 46–50 V before you connect 150–155 (CAUTION C3).",
                Rect.MinMaxRect(330, 168, 362, 188), "G1  PSU 48 V / 15 A", "switched by KM1",
                new Vector2(334, 188), new Vector2(340, 188), new Vector2(362, 178),
                new[] { new Vector2(334, 168), new Vector2(344, 168), new Vector2(354, 168) },
                new[] { new Vector2(338, 168), new Vector2(348, 168), new Vector2(358, 168) }, Pot.P48, Pot.Z48));
            c.Add(new Breaker("QF3", "CB-PUMP C10A", "Pump outlet branch (the old CB-PSU48Z).", 202, 194, true, new Vector2(288, 198), TA.Right, 290));
            c.Add(new SchukoPump());
            c.Add(new PeBar());
            c.Add(new NodeT("BODY", "Enclosure body bond", "Earth stud on the enclosure body (left side wall). The earth bar sits on insulators, so this is the body's PE path.",
                157.5f, 276.5f, true, "", default, TA.Left));
            c.Add(new NodeT("BKP", "Backplate bond", "Steel backplate earth stud.", 165, 276.5f, true, "", default, TA.Left));
            c.Add(new NodeT("DOOR", "Door bond strap", "Braided strap from the door earth stud to the earth bar.", 180, 276.5f, true, "", default, TA.Left));
            c.Add(new NodeT("RAIL", "DIN-rail earth stud", "Ring lug on the DIN rail fixing screw (no terminal block).", 255, 276.5f, true, "", default, TA.Left));
            c.Add(new NodeT("XL", "XL — L busbar (230 V, 12 points)", "Switched L after KM1 2T1 → CB-VFD, CB-48V, CB-PUMP (one conductor per point, 8 spare). 160 × 16 × 42 mm on the plate, right of the right vertical duct.",
                124, 210, false, "XL", new Vector2(124, 212.2f), TA.Center));
            c.Add(new NodeT("XN", "XN — N busbar (230 V, 12 points)", "Every N conductor: EMI N-OUT in → PSU 24 V, KM1 coil A1, VFD, PSU 48 V, pump outlet (one conductor per point, 6 spare). 160 × 16 × 42 mm, beside XL.\nKM1 switches L only.",
                112, 205, false, "XN", new Vector2(110.6f, 202.4f), TA.Right));

            // ================= E2 — 24 V control =================
            c.Add(new Fuse("F1", "Control fuse: E-stop chain, K1, lamps and fan (+24 V CONTROL).", 24, 144, 136, new Vector2(25.6f, 140)));
            c.Add(new Fuse("F2", "Signal fuse: A11 board and sensors (+24 V SIGNAL).", 56, 144, 136, new Vector2(57.6f, 140)));
            c.Add(new Fan());
            c.Add(new EStop());
            c.Add(new PushButton("S1", "S1 — OFF push-button ZB2-BE102C (NC)", "Momentary NC. Opens the K1 chain.", true, 36, 112, 104, new Vector2(30.6f, 108), "S1 OFF", "NC · hold"));
            c.Add(new PushButton("S2", "S2 — ON push-button ZB2-BE101C (NO)", "Momentary NO. Energizes K1; K1 pole 1 then latches.", false, 36, 98, 90, new Vector2(30.6f, 94), "S2 ON", "NO · hold"));
            c.Add(new Lamp("H1", "H1 — yellow LED pilot lamp 24 V", "POWER ON: lit through KM1 aux 43-44.", 60, 105, Pal.LampYellow, "H1 yellow", "POWER ON"));
            c.Add(new Lamp("H2", "H2 — green LED pilot lamp 24 V", "WORK: lamp − sinks into A11 Y6 (CAUTION C6).", 78, 105, Pal.LampGreen, "H2 green", "WORK"));
            c.Add(new Lamp("H3", "H3 — red LED pilot lamp 24 V", "ALARM: lamp − sinks into A11 Y5 (CAUTION C6).", 94, 105, Pal.LampRed, "H3 red", "ALARM"));

            // ================= E3 / E4 — A11, sensors, drives =================
            var a11 = c.Add(new A11Board());
            a11.m = m;
            a11.vfd = vfd;
            c.Add(new SensorConnector("X-LIM", "X axis", 108, false, "B1 X home (X−)", () => m.XHome, "B2 X+ limit", () => m.XLimit));
            c.Add(new SensorConnector("Y-LIM", "Y axis", 88, false, "B3 Y home (Y−)", () => m.YHome, "B4 Y+ limit", () => m.YLimit));
            c.Add(new SensorConnector("Z-LIM", "Z axis", 68, true, "B5 Z home (Z+)", () => m.ZHome, "B6 Z− limit", () => m.ZLimit));
            var ts = c.Add(new ToolSetter());
            ts.m = m;
            float[] dy = { 112, 76, 40 };
            for (int i = 0; i < 3; i++)
            {
                var d = c.Add(new StepperDrive(i, dy[i]));
                a11.drives[i] = d;
                var mot = c.Add(new AxisMotor(i, dy[i]));
                mot.m = m;
            }
            a11.SyncDro();

            Wires(c, m, vfd);
            Cautions(c);
            Titles(c);
            return c;
        }

        /// Wire schedule rev 0.7 — direct wiring: no terminal blocks, except the two 230 V distribution blocks XL and XN.
        /// Every other former terminal block / bus is a daisy chain, max. 2 conductors per terminal
        /// (twin ferrule). Each physical wire is drawn along the net lines from its own terminal to the
        /// terminal it lands on. Unnumbered "Bus" lines are drawings of a net only (a = b, no connection).
        /// Colours (IEC 60204-1 13.2.4): L black, N light blue, PE green/yellow, AC control red, DC power black,
        /// DC control and signals dark blue, 0 V blue/white.
        static void Wires(Circuit c, Machine m, Vfd vfd)
        {
            // ---- 220 V power ----
            c.W("101", "Mains.L", "QF0.1", K.AC, "Mains L (gland) → Main CB L in · Mains L", "2.5 mm² black", 10, 296, 10, 288);
            c.W("102", "Mains.N", "QF0.3", K.AC, "Mains N (gland) → Main CB N in · Mains N", "2.5 mm² light blue", 14, 296, 14, 288);
            c.W("103", "QF0.2", "Z1.LIN", K.AC, "Main CB L out → EMI L-IN", "4 mm² black", 10, 280, 10, 270);
            c.W("104", "QF0.4", "Z1.NIN", K.AC, "Main CB N out → EMI N-IN", "4 mm² light blue", 14, 280, 14, 270);
            // L-UNSW: EMI L-OUT feeds KM1 1L1 and QF4. All N conductors meet on the N distribution block XN.
            c.Bus("Z1.LOUT", K.AC, 10, 254, 96, 254);
            c.Bus("XN.T", K.AC, 14, 249, 112, 249);
            c.Bus("XN.T", K.AC, 112, 249, 112, 205);
            c.W("105", "Z1.LOUT", "KM1.1L1", K.AC, "EMI L-OUT → Contactor 1L1 · Unswitched L", "4 mm² black", 10, 262, 10, 254, 96, 254, 96, 246);
            c.W("111", "Z1.LOUT", "QF4.1", K.AC, "EMI L-OUT → CB-24V in · 24 V PSU L (unswitched)", "4 mm² black", 10, 262, 10, 254, 30, 254, 30, 246);
            c.W("106", "Z1.NOUT", "XN.T", K.AC, "EMI N-OUT → N busbar XN · N in", "4 mm² light blue", 14, 262, 14, 249, 112, 249, 112, 205);
            // Switched side: KM1 2T1 → L distribution block XL → the three MCB inputs.
            c.Bus("XL.T", K.AC, 124, 210, 334, 210);
            c.Bus("XN.T", K.AC, 112, 205, 340, 205);
            c.W("109", "KM1.2T1", "XL.T", K.AC, "Contactor 2T1 → L busbar XL · Switched L in", "4 mm² black", 96, 238, 96, 210, 124, 210);
            c.W("114", "XL.T", "QF1.1", K.AC, "XL → CB-VFD in · Switched L", "4 mm² black", 124, 210, 136, 210, 136, 202);
            c.W("117", "XL.T", "QF2.1", K.AC, "XL → CB-48V in · Switched L", "4 mm² black", 124, 210, 334, 210, 334, 202);
            c.W("120", "XL.T", "QF3.1", K.AC, "XL → CB-PUMP in · Switched L", "4 mm² black", 124, 210, 290, 210, 290, 202);
            c.W("113", "XN.T", "G2.N", K.AC, "XN → PSU 24V N", "1.5 mm² light blue", 112, 205, 112, 249, 38, 249, 38, 230);
            c.W("132", "XN.T", "KM1.A1", K.AC, "XN → Contactor A1 · Coil N", "1.5 mm² light blue", 112, 205, 112, 249, 66, 249, 66, 224, 60, 224);
            c.W("116", "XN.T", "U1.N", K.AC, "XN → VFD N", "2.5 mm² light blue", 112, 205, 142, 205, 142, 188);
            c.W("119", "XN.T", "G1.N", K.AC, "XN → PSU 48V N", "1.5 mm² light blue", 112, 205, 340, 205, 340, 188);
            c.W("122", "XN.T", "XS1.N", K.AC, "XN → Schuko N · Pump", "1.5 mm² light blue", 112, 205, 296, 205, 296, 188);
            c.W("112", "QF4.2", "G2.L", K.AC, "CB-24V out → PSU 24V L", "1.5 mm² black", 30, 238, 30, 230);
            c.W("115", "QF1.2", "U1.L", K.AC, "CB-VFD out → VFD L", "2.5 mm² black", 136, 194, 136, 188);
            c.W("118", "QF2.2", "G1.L", K.AC, "CB-48V out → PSU 48V L", "1.5 mm² black", 334, 194, 334, 188);
            c.W("121", "QF3.2", "XS1.L", K.AC, "CB-PUMP out → Schuko L · Pump", "1.5 mm² black", 290, 194, 290, 188);
            // Spindle: one 4G1.5 shielded cable W1 (VFD → HDC-HA) in the left power lane, shield 360° at both ends.
            var w123 = c.W("123", "U1.U", "X1.1", K.Phase, "VFD U → HDC-HA pin 1 · Spindle U (cable W1)", "1.5 mm² brown, core of W1 4G1.5 shielded", 172, 184, 182, 184);
            var w124 = c.W("124", "U1.V", "X1.2", K.Phase, "VFD V → HDC-HA pin 2 · Spindle V (cable W1)", "1.5 mm² black, core of W1 4G1.5 shielded", 172, 180, 182, 180);
            var w125 = c.W("125", "U1.W", "X1.3", K.Phase, "VFD W → HDC-HA pin 3 · Spindle W (cable W1)", "1.5 mm² grey, core of W1 4G1.5 shielded", 172, 176, 182, 176);
            c.W("126", "U1.PE", "X1.PE", K.PE, "VFD PE → HDC-HA PE · Spindle PE (cable W1)", "1.5 mm² G/Y, core of W1 4G1.5 shielded", 172, 170, 182, 170);
            c.W("127", "X1.SH", "U1.PEb", K.PE, "Cable W1 shield: 360° in the HDC-HA metal hood → 360° EMC clamp at the VFD (bonded to VFD PE). Both ends, no pigtail",
                "W1 braided shield, 360° clamp both ends", 182, 166, 172, 166);
            c.W("128", "U1.P+", "R1.T1", K.Brake, "VFD P+ → Braking resistor T1", "1.5 mm² black, silicone", 128, 182, 118, 182);
            c.W("129", "U1.PB", "R1.T2", K.Brake, "VFD PB → Braking resistor T2", "1.5 mm² black, silicone", 128, 174, 118, 174);
            // KM1 coil: from QF4 out (C6), through K1 pole 4 (pole 3 left empty as the gap to the 24 V poles).
            c.W("130", "QF4.2", "K1.41", K.AC, "CB-24V out → K1 pole-4 COM · Coil supply L (protected by QF4 C6)", "1.5 mm² red", 30, 238, 30, 234, 56, 234, 56, 250, 60, 250, 60, 246);
            c.W("131", "K1.44", "KM1.A2", K.AC, "K1 pole-4 NO → Contactor A2 · Switched coil L", "1.5 mm² red", 60, 238, 60, 232);

            // Spindle cable cores, shield and pump cord (not numbered).
            foreach (var w in new[]
                     {
                         c.Core("X1.1", "M1.U", K.Phase, "Spindle cable core U", 182, 184, 186, 184, 192.1f, 180.25f),
                         c.Core("X1.2", "M1.V", K.Phase, "Spindle cable core V", 182, 180, 186, 180, 191.5f, 178f),
                         c.Core("X1.3", "M1.W", K.Phase, "Spindle cable core W", 182, 176, 186, 176, 192.1f, 175.75f),
                         w123, w124, w125,
                     })
                w.speedOf = () => vfd.freq / 25f;
            c.Core("X1.PE", "M1.PE", K.PE, "Spindle PE core", 182, 170, 196, 170, 196, 173.5f);
            c.Core("X1.SH", "M1.PE", K.PE, "Spindle cable shield → spindle housing (EMC gland, 360°)", 182, 166, 199, 166, 199, 173.5f, 196, 173.5f);
            c.Core("XS1.cL", "XS1.pL", K.AC, "Pump cord L", 291, 178, 291, 172, 292.4f, 169.1f);
            c.Core("XS1.cN", "XS1.pN", K.AC, "Pump cord N", 297, 178, 297, 172, 295.6f, 169.1f);

            // ---- PE (earth bar is a busbar, not a terminal block) ----
            c.W("140", "Mains.PE", "PEBAR.BAR", K.PE, "Mains PE (gland) → Earth bar", "4 mm² G/Y", 18, 296, 18, 302, 150, 302, 150, 280);
            // Two 4-point earth bars on insulators (bar 1: 150–213, bar 2: 221–286), linked by 138.
            c.Bus("PEBAR.BAR", K.PE, 150, 280, 213, 280);
            c.Bus("PEBAR.BAR2", K.PE, 221, 280, 286, 280);
            NoLabel(c.W("139", "BODY.T", "PEBAR.BAR", K.PE, "Enclosure body (earth stud) → Earth bar · NEW: the body had no PE conductor", "4 mm² G/Y", 157.5f, 276.5f, 157.5f, 280));
            NoLabel(c.W("141", "BKP.T", "PEBAR.BAR", K.PE, "Backplate bond → Earth bar", "4 mm² G/Y", 165, 276.5f, 165, 280));
            NoLabel(c.W("142", "DOOR.T", "PEBAR.BAR", K.PE, "Door bond strap → Earth bar", "6 mm² braided strap", 180, 276.5f, 180, 280));
            NoLabel(c.W("147", "RAIL.T", "PEBAR.BAR2", K.PE, "DIN-rail earth stud → Earth bar (ring lug, no terminal block)", "2.5 mm² G/Y", 255, 276.5f, 255, 280));
            Stub(c, "143", "Z1.GND", "EMI filter ground lug → Earth bar", "4 mm² G/Y", 30, 266, 34, 266);
            Stub(c, "144", "U1.PEc", "VFD PE → Earth bar", "2.5 mm² G/Y", 172, 162, 176, 162);
            Stub(c, "145", "G1.PE", "PSU 48V PE → Earth bar 2", "1.5 mm² G/Y", 362, 178, 366, 178, "PEBAR.BAR2");
            Stub(c, "146", "G2.PE", "PSU 24V PE → Earth bar 2", "1.5 mm² G/Y", 48, 224, 52, 224, "PEBAR.BAR2");
            Stub(c, "149", "XS1.PE", "Schuko outlet PE → Earth bar 2 · NEW: missing in rev 0.5 (pump was unearthed)", "1.5 mm² G/Y", 302, 183, 306, 183, "PEBAR.BAR2");
            var w148 = c.W("148", "G2.-V2", "PEBAR.BAR2", K.PE, "PSU 24V −V (screw 2) → Earth bar · PELV bond: the only 0 V to PE bond", "1.5 mm² G/Y", 10, 8, 10, 4);
            w148.labelText = "148 PELV bond → PE bar";
            w148.labelAt = new Vector2(11.2f, 4.6f);
            w148.stub = true;
            var w138 = c.W("138", "PEBAR.BAR", "PEBAR.BAR2", K.PE, "Earth bar 1 → earth bar 2 · link (both bars sit on insulators)", "4 mm² G/Y", 213, 280, 221, 280);
            w138.labelText = "138";
            w138.labelAt = new Vector2(217, 281.2f);

            // ---- DC power ----
            c.W("150", "G1.+V1", "A2.AC1", K.DC48, "PSU 48V +V (screw 1) → DMA860H-X AC 1 · 48 V", "1.5 mm² black", 334, 168, 334, 150, 272, 150, 272, 140);
            c.W("151", "G1.-V1", "A2.AC2", K.DC48, "PSU 48V −V (screw 1) → DMA860H-X AC 2", "1.5 mm² black", 338, 168, 338, 147, 278, 147, 278, 140);
            c.W("152", "G1.+V2", "A3.AC1", K.DC48, "PSU 48V +V (screw 2) → DMA860H-Y AC 1", "1.5 mm² black", 344, 168, 344, 110, 272, 110, 272, 104);
            c.W("153", "G1.-V2", "A3.AC2", K.DC48, "PSU 48V −V (screw 2) → DMA860H-Y AC 2", "1.5 mm² black", 348, 168, 348, 107, 278, 107, 278, 104);
            c.W("154", "G1.+V3", "A4.AC1", K.DC48, "PSU 48V +V (screw 3) → DMA860H-Z AC 1", "1.5 mm² black", 354, 168, 354, 74, 272, 74, 272, 68);
            c.W("155", "G1.-V3", "A4.AC2", K.DC48, "PSU 48V −V (screw 3) → DMA860H-Z AC 2", "1.5 mm² black", 358, 168, 358, 71, 278, 71, 278, 68);
            // +24 V straight from the PSU screws to the two fuses (no TB24+).
            c.Bus("G2.+V1", K.DC24, 24, 148, 56, 148);
            c.W("162", "G2.+V1", "F1.in", K.DC24, "PSU 24V +V → F1 in · Control fuse (short, 2.5 mm²: unfused PSU side)", "2.5 mm² dark blue", 42, 218, 42, 148, 24, 148, 24, 144);
            c.W("164", "G2.+V2", "F2.in", K.DC24, "PSU 24V +V (screw 2) → F2 in · Signal fuse (short, 2.5 mm²: unfused PSU side)", "2.5 mm² dark blue", 46, 218, 46, 148, 56, 148, 56, 144);
            // +24 V CONTROL (after F1) and +24 V SIGNAL (after F2).
            c.Bus("F1.out", K.DC24, 16, 128, 94, 128);
            c.Bus("F2.out", K.DC24, 56, 136, 124, 136, 124, 80);
            c.W("166", "F2.out", "A1.DC24V", K.DC24, "F2 out → A11 board power + (J10)", "0.5 mm² dark blue", 56, 136, 124, 136, 200, 136);
            c.W("168", "F2.out", "A1.24V", K.DC24, "F2 out → A11 input 24V+ (J8) · Sensor/input supply", "0.5 mm² dark blue", 56, 136, 124, 136, 124, 132, 200, 132);
            // 0 V straight from the PSU −V screws, then daisy-chained (no TB0V).
            c.Bus("G2.-V1", K.DC24, 6, 8, 236, 8);
            c.Bus("G2.-V1", K.DC24, 128, 8, 128, 126);
            c.Bus("G2.-V1", K.DC24, 30, 218, 30, 214, 6, 214, 6, 8);
            c.W("167", "G2.-V1", "A1.DCGND", K.DC24, "PSU 24V −V → A11 board power − (J10)", "0.5 mm² blue/white", 30, 218, 30, 214, 6, 214, 6, 8, 128, 8, 128, 34, 200, 34);
            c.W("169", "G2.-V2", "A1.GND", K.DC24, "PSU 24V −V (screw 2) → A11 input GND (J8)", "0.5 mm² blue/white", 34, 218, 34, 214, 6, 214, 6, 8, 128, 8, 128, 38, 200, 38);
            c.W("170", "KM1.43", "M2.+", K.DC24, "Contactor aux 43 → Fan + · NEW: fan behind F1 (was unfused on TB24+)", "0.5 mm² dark blue", 60, 124, 60, 128, 16, 128, 16, 124);
            c.W("171", "A1.DCGND", "M2.-", K.DC24, "A11 J10 GND → Fan − (loop)", "0.5 mm² blue/white", 200, 34, 128, 34, 128, 8, 16, 8, 16, 118);

            // ---- 24 V control and lamps ----
            const string dc = "0.5 mm² dark blue", z0 = "0.5 mm² blue/white";
            c.W("201", "F1.out", "S0.1", K.DC24, "F1 out → E-stop NC in · Chain", dc, 24, 136, 24, 128, 36, 128, 36, 124);
            c.W("202", "S0.2", "S1.1", K.DC24, "E-stop NC out → OFF (NC) in · Chain", dc, 36, 116, 36, 112);
            c.W("203", "S1.2", "S2.1", K.DC24, "OFF out (Node X) → ON (NO) in · Chain", dc, 36, 104, 36, 98);
            c.W("204", "S2.2", "K1.A1", K.DC24, "ON out → K1 coil A1 · Start", dc, 36, 90, 36, 82);
            c.W("205", "S1.2", "K1.11", K.DC24, "Node X (OFF out) → K1 pole-1 COM · Latch", dc, 36, 104, 46, 104, 46, 98);
            c.W("206", "K1.14", "K1.A1", K.DC24, "K1 pole-1 NO → K1 coil A1 · Latch", dc, 46, 90, 46, 86, 36, 86);
            c.W("207", "G2.-V1", "K1.A2", K.DC24, "PSU 24V −V → K1 coil A2 · Coil return", z0, 30, 218, 30, 214, 6, 214, 6, 8, 36, 8, 36, 74);
            // X7 loop: XT pin 4 (0 V) → tool-setter over-travel NC → pin 3 → 347 → E-stop NC2 (added ZB2-BE102) → 210 → X7.
            // OFF does not touch this loop, so only the E-stop (or over-travel) raises the A11 E-STOP alarm.
            c.W("210", "S0.4", "A1.X7", K.Sig, "E-stop NC2 → A11 X7 · E-stop signal to the controller", dc, 172, 126, 200, 126);
            c.W("220", "F1.out", "KM1.43", K.DC24, "F1 out → Contactor aux 43 · Yellow lamp supply", dc, 24, 136, 24, 128, 60, 128, 60, 124);
            c.W("221", "KM1.44", "H1.+", K.DC24, "Contactor aux 44 → Yellow lamp +", dc, 60, 116, 60, 108);
            c.W("222", "K1.A2b", "H1.-", K.DC24, "K1 coil A2 (2nd screw) → Yellow lamp − · 0 V to the door", z0, 36, 70, 36, 8, 60, 8, 60, 102);
            c.W("223", "S0.1", "H2.+", K.DC24, "E-stop NC in → Green lamp + · +24 V CONTROL (door loop)", dc, 36, 124, 36, 128, 78, 128, 78, 108);
            c.W("224", "H2.-", "A1.Y6", K.Sig, "Green lamp − → A11 Y6 · Work", dc, 78, 102, 78, 16, 216, 16, 216, 30);
            c.W("225", "H2.+", "H3.+", K.DC24, "Green lamp + → Red lamp + (door loop)", dc, 78, 108, 78, 128, 94, 128, 94, 108);
            c.W("226", "H3.-", "A1.Y5", K.Sig, "Red lamp − → A11 Y5 · Alarm", dc, 94, 102, 94, 22, 210, 22, 210, 30);

            // ---- signals: axis outputs ----
            string[] ax = { "X", "Y", "Z" };
            float[] y0 = { 112, 76, 40 };
            int n = 301;
            for (int i = 0; i < 3; i++)
            {
                string a = ax[i], d = "A" + (i + 2);
                float y = y0[i];
                int axis = i;
                c.W((n).ToString(), $"A1.{a}_5V", $"{d}.PUL+", K.Sig5, $"A11 {a}-axis 5V → DMA860H-{a} PUL+", "0.5 mm² dark blue, twisted", 226, y + 22, 250, y + 22);
                var pul = c.W((n + 1).ToString(), $"A1.{a}_PUL", $"{d}.PUL-", K.Pulse, $"A11 {a}-axis PULSE → DMA860H-{a} PUL−", "0.5 mm² dark blue, twisted", 226, y + 18, 250, y + 18);
                pul.speedOf = () => Mathf.Abs(m.vel[axis]) + 1f;
                c.W((n + 2).ToString(), $"A1.{a}_DIR", $"{d}.DIR-", K.Dir, $"A11 {a}-axis DIR → DMA860H-{a} DIR−", "0.5 mm² dark blue, twisted", 226, y + 10, 250, y + 10);
                var j = c.W((n + 3).ToString(), $"{d}.PUL+", $"{d}.DIR+", K.Sig5, $"DMA860H-{a} PUL+ → DMA860H-{a} DIR+ · Jumper", dc, 250, y + 22, 252, y + 22, 252, y + 14, 250, y + 14);
                j.labelAt = new Vector2(250.4f, y + 12.6f);
                n += 4;
                string[] dt = { "A+", "A-", "B+", "B-" };
                for (int k = 0; k < 4; k++)
                {
                    float yy = y + 22 - 4 * k;
                    var mp = new Vector2(314, y + 16) + Gfx.Dir(AxisMotor.Angles[k]) * 5f;
                    // Shielded LiYCY 4 × 1.0 from the drive to the motor, in the right motor lane (≥ 10 cm from PUL/DIR where possible).
                    var c1 = c.Core($"{d}.{dt[k]}", $"XM-{a}.{k + 1}", K.Motor, $"Motor core {dt[k]} → GX16-6 pin {k + 1} (LiYCY 4 × 1.0 shielded, right motor lane)", 286, yy, 298, yy);
                    var c2 = c.Core($"XM-{a}.{k + 1}", $"XM-{a}.m{k + 1}", K.Motor, $"Motor core {dt[k]} (LiYCY 4 × 1.0 shielded cable)", 298, yy, 302, yy, mp.x, mp.y);
                    c1.speedOf = c2.speedOf = () => Mathf.Abs(m.vel[axis]) * 0.05f + 0.6f;
                }
            }

            // ---- drive alarms: parallel (X5 = NO assumed), chained so X5 takes one conductor ----
            // The drive alarms are present on the drives on hand (2026-10). ALM− chain from the A11 output GND (J7), so the
            // sensor 0 V no longer passes through the drive connectors.
            c.W("315", "A2.ALM+", "A1.X5", K.Sig, "DMA860H-X ALM+ → A11 X5 · Drive alarm (wire-OR)", dc, 250, 114, 240, 114, 240, 36, 226, 36);
            c.W("317", "A3.ALM+", "A2.ALM+", K.Sig, "DMA860H-Y ALM+ → DMA860H-X ALM+ (loop) · NEW: X5 had 3 conductors", dc, 250, 78, 240, 78, 240, 114, 250, 114);
            c.W("319", "A4.ALM+", "A3.ALM+", K.Sig, "DMA860H-Z ALM+ → DMA860H-Y ALM+ (loop)", dc, 250, 42, 240, 42, 240, 78, 250, 78);
            c.W("316", "A1.OGND", "A2.ALM-", K.DC24, "A11 J7 output GND → DMA860H-X ALM−", z0, 222, 142, 222, 140, 243, 140, 243, 118, 250, 118);
            c.W("318", "A2.ALM-", "A3.ALM-", K.DC24, "DMA860H-X ALM− → DMA860H-Y ALM− (loop)", z0, 250, 118, 236, 118, 236, 82, 250, 82);
            c.W("320", "A3.ALM-", "A4.ALM-", K.DC24, "DMA860H-Y ALM− → DMA860H-Z ALM− (loop)", z0, 250, 82, 236, 82, 236, 46, 250, 46);

            // ---- spindle run and speed (multi-speed) ----
            c.W("325", "A1.Y1", "U1.X1", K.Sig, "A11 Y1 (S0) → VFD X1 · Spindle run (forward)", dc, 204, 142, 204, 144, 136, 144, 136, 160);
            c.W("326", "A1.OGND", "U1.COM", K.DC24, "A11 output GND → VFD COM (digital) · Output common", z0, 222, 142, 222, 152, 162, 152, 162, 160);
            c.W("327", "A1.Y2", "U1.X2", K.Sig, "A11 Y2 (S1) → VFD X2 · Multi-speed bit 1", dc, 208, 142, 208, 146, 142, 146, 142, 160);
            c.W("328", "A1.Y3", "U1.X3", K.Sig, "A11 Y3 (S2) → VFD X3 · Multi-speed bit 2", dc, 212, 142, 212, 148, 148, 148, 148, 160);
            c.W("329", "A1.Y4", "U1.X4", K.Sig, "A11 Y4 (S3) → VFD X4 · Multi-speed bit 3", dc, 216, 142, 216, 150, 154, 150, 154, 160);

            // ---- sensors (NPN NO): direct to the A11, limits chained at the connectors (no TB-HL) ----
            // Sensor 0 V straight from the A11 J8 GND screw (rev 0.6 looped it through the three drive ALM− screws).
            c.W("330", "X-LIM.1", "A1.X1", K.Sig, "X-LIM pin 1 → A11 X1 · X home (X−)", dc, 160, 118, 200, 118);
            c.W("331", "Y-LIM.1", "A1.X2", K.Sig, "Y-LIM pin 1 → A11 X2 · Y home (Y−)", dc, 160, 98, 200, 98);
            c.W("332", "Z-LIM.2", "A1.X3", K.Sig, "Z-LIM pin 2 → A11 X3 · Z home (Z+)", dc, 160, 78, 200, 78);
            c.Bus("A1.X6", K.Sig, 176, 112, 176, 62);
            c.W("336", "X-LIM.2", "A1.X6", K.Sig, "X-LIM pin 2 → A11 X6 · Hard limit (wire-OR)", dc, 160, 112, 176, 112, 176, 62, 200, 62);
            c.W("334", "X-LIM.2", "Y-LIM.2", K.Sig, "X-LIM pin 2 → Y-LIM pin 2 · Y+ limit (loop)", dc, 160, 112, 176, 112, 176, 92, 160, 92);
            c.W("335", "Y-LIM.2", "Z-LIM.1", K.Sig, "Y-LIM pin 2 → Z-LIM pin 1 · Z− limit (loop)", dc, 160, 92, 176, 92, 176, 72, 160, 72);
            c.W("337", "A1.GND", "X-LIM.3", K.DC24, "A11 J8 GND → X-LIM pin 3 · Sensor 0 V (BU)", z0, 200, 38, 128, 38, 128, 110, 140, 110);
            c.W("338", "X-LIM.3", "Y-LIM.3", K.DC24, "X-LIM pin 3 → Y-LIM pin 3 · Sensor 0 V (loop)", z0, 140, 110, 128, 110, 128, 90, 140, 90);
            c.W("339", "Y-LIM.3", "Z-LIM.3", K.DC24, "Y-LIM pin 3 → Z-LIM pin 3 · Sensor 0 V (loop)", z0, 140, 90, 128, 90, 128, 70, 140, 70);
            c.W("340", "A1.24V", "X-LIM.4", K.DC24, "A11 J8 24V → X-LIM pin 4 · Sensor +24 V (BN)", dc, 200, 132, 124, 132, 124, 120, 140, 120);
            c.W("341", "X-LIM.4", "Y-LIM.4", K.DC24, "X-LIM pin 4 → Y-LIM pin 4 · Sensor +24 V (loop)", dc, 140, 120, 124, 120, 124, 100, 140, 100);
            c.W("342", "Y-LIM.4", "Z-LIM.4", K.DC24, "Y-LIM pin 4 → Z-LIM pin 4 · Sensor +24 V (loop)", dc, 140, 100, 124, 100, 124, 80, 140, 80);
            c.W("345", "XT.1", "A1.X4", K.Sig, "TS pin 1 (red) → A11 X4 · Tool-setter touch", dc, 160, 54, 200, 54);
            c.W("346", "Z-LIM.3", "XT.2", K.DC24, "Z-LIM pin 3 → TS pin 2 (black) · 0 V (loop)", z0, 140, 70, 128, 70, 128, 54, 140, 54);
            c.W("347", "XT.3", "S0.3", K.Sig, "TS pin 3 (blue) → E-stop NC2 · Tool-setter over-travel (NC) in series with the E-stop NC2 → A11 X7 (door loop)",
                dc, 160, 44, 184, 44, 184, 130, 164, 130, 164, 126);
            c.W("348", "XT.2", "XT.4", K.DC24, "TS pin 2 → TS pin 4 (yellow) · 0 V to the over-travel switch (jumper in the connector)", z0, 140, 54, 128, 54, 128, 44, 140, 44);
        }

        static void Stub(Circuit c, string num, string from, string desc, string spec, float x0, float y0, float x1, float y1, string to = "PEBAR.BAR")
        {
            var w = c.W(num, from, to, K.PE, desc, spec, x0, y0, x1, y1);
            w.labelText = num + (to == "PEBAR.BAR" ? " → PE bar 1" : " → PE bar 2");
            w.labelAt = new Vector2(x1 + 0.5f, y1);
            w.stub = true;
        }

        static void NoLabel(Wire w) => w.noLabel = true; // the PE bar drop label already names it

        static void Cautions(Circuit c)
        {
            c.Caution("C1", 18.5f, 245.2f, "Before you set the Main CB ON for the first time: do the dead tests (§ 7.1 tests 1, 2, 3, 12, 13).\nCorrect: 220 V does not touch 24 V or PE. 0 V is bonded to PE.\nRisk: 220 V on the 24 V side destroys the A11, drives and sensors.");
            c.Caution("C2", 46.5f, 212.5f, "Before you connect A11 power (166–169): measure F2 out to PSU −V.\nCorrect: 23–25 V DC, F2 out positive.\nRisk: damage to the A11 (24 V / 3 A board).");
            c.Caution("C3", 366.5f, 184f, "Before you connect drive power (150–155): measure the G1 output.\nCorrect: 46–50 V DC (drive range 26–113 V DC).\nRisk: damage to the drives.");
            c.Caution("C4", 305f, 138.5f, "Before you connect or disconnect a motor connector: QF2 (CB-48V) is OFF and the drive LEDs are off.\nRisk: damage to the drive output stage.");
            c.Caution("C4", 305f, 102.5f, "Before you connect or disconnect a motor connector: QF2 (CB-48V) is OFF and the drive LEDs are off.\nRisk: damage to the drive output stage.");
            c.Caution("C4", 305f, 66.5f, "Before you connect or disconnect a motor connector: QF2 (CB-48V) is OFF and the drive LEDs are off.\nRisk: damage to the drive output stage.");
            string c5 = "Before you connect axis signals (301–312): A11 5V to GND = 5 V. No 24 V or 48 V wire near 5V, PULSE, DIR.\nAfter: loop current in 301 / 305 / 309 must be 7–8 mA (§ 7.4 test 2).\nRisk: 24 V destroys the A11. Low current gives lost steps.";
            c.Caution("C5", 232f, 137.6f, c5);
            c.Caution("C5", 232f, 101.6f, c5);
            c.Caution("C5", 232f, 65.6f, c5);
            c.Caution("C6", 205f, 25.6f, "Before you connect lamps to Y5 / Y6 (224, 226): measure the lamp current (§ 7.4 test 1)\nand get the Y-output rating from the supplier. Correct: lamp current less than the rating.\nRisk: damage to the A11 output.");
            c.Caution("C7", 167f, 156f, "Before you connect VFD inputs (325–329): with the wires off, X1–X4 to VFD COM ≤ 24 V DC, X positive.\nVFD COM to 0 V (G2 −V) < 1 V (DC and AC ranges).\nRisk: a VFD COM with mains voltage destroys the A11 output stage.");
            c.Caution("C8", 156f, 192f, "Before the first spindle run: read back P003, P021, P022, P064, P092–P097.\nCorrect: P003 = P021 = 400.0, P022 = 100.0, P064 = 0, P092–P094 = 1/2/3, P095–P097 = 0.\nRisk: overspeed of the spindle.");
            c.Caution("C9", 245f, 30f, "Before you connect ALM wires (315–320): do the ALM polarity test (§ 7.4 test 3).\nIf the opto conducts in normal operation: series + X5 = NC; if only on a fault: parallel + X5 = NO.\nThis simulation uses parallel / NO. Risk: false alarms or a hidden drive fault.");
            string c10 = "Before you connect a sensor connector: measure pin 4 (BN) = +24 V and pin 3 (BU) = 0 V.\nMake sure each sensor is NPN.\nRisk: damage to the sensors or the A11 inputs.";
            c.Caution("C10", 134f, 123.4f, c10);
            c.Caution("C10", 134f, 103.4f, c10);
            c.Caution("C10", 134f, 83.4f, c10);
            c.Caution("C10", 134f, 59.6f, c10);
            c.Caution("C11", 44.6f, 80.4f, "K1 socket (looks like a Schneider RXZE2S114M; exact part not confirmed): each screw has two labels, IEC on top (arrow up), NEMA below (arrow down). Use the TOP number: A1 = 13, A2 = 14, 11 = 9, 14 = 5, 41 = 12, 44 = 8.\n" +
                "NEMA 14 is the coil A2 — wire 206 (IEC 14, pole 1 NO) on NEMA 14 shorts the coil when ON is pushed and F1 opens.\n" +
                "Coil suppression (optional): only if the socket has a module slot and is a genuine RXZE2S114M, a Schneider RXM040W diode module plugs in\n" +
                "(observe its polarity marking, A1 = +24 V). Twin ferrules: keep to 2 × 1 mm² per screw (A1 takes 204 + 206, 2 × 0.5 mm²).");
            c.Caution("C12", 112f, 185.5f, "Before you set CB-VFD ON for the first time: R1 connects to VFD terminals P+ and PB only.\nRead the terminal labels on the VFD.\nRisk: damage to the VFD.");
            c.Caution("C14", 40f, 284f, "REMINDER — VFD supply rating (open): the FC300 nameplate's 11 A is the rated OUTPUT current. A 2.2 kW single-phase drive draws ≈ 22–24 A INPUT at full load\n" +
                "(Delta VFD022EL21A: 11 A out, 24 A in). At full spindle load QF1 C16 trips, Z1 (20 A) is overloaded and QF0 C25 is marginal (≈ 28–30 A with the PSUs and pump).\n" +
                "Light cuts (≈ 1 kW, ≈ 10–12 A) are within all ratings. Z1 is fixed at 20 A (no larger filter available): limit the VFD current / power so the spindle stays at ≈ 1.5 kW\n" +
                "(≈ 15 A input + ≈ 4 A PSUs + pump < 20 A) and check with a clamp meter on Z1 LINE L during a heavy cut. The 4 mm² path behind QF0 is fine for C25.\n" +
                "Before you wire Z1: check that its LOAD L terminal takes 105 + 111 (2 × 4 mm², twin ferrule).");
            c.Caution("C13", 162f, 192f, "Before you cut material with S commands: do § 7.4 test 4 (S-grade bit order).\nExpected: S1 = 000 … S8 = 111 (Y4 Y3 Y2). If different, change only P030–P037.\nRisk: wrong spindle speed; tool damage.");
        }

        static void Titles(Circuit c)
        {
            c.titles.Add(("CNC 3-axis router — United Control Box (RichAuto A11) · sheets E1–E4 merged · manual rev 0.5, wired directly (rev 0.7)", new Vector2(-2, 310), 2.4f));
            c.titles.Add(("E1 · 220 V POWER DISTRIBUTION AND CONTACTOR", new Vector2(60, 297.2f), 1.6f));
            c.titles.Add(("E2 · 24 V CONTROL — K1 SELF-HOLD, E-STOP, LAMPS", new Vector2(46, 156), 1.5f));
            c.titles.Add(("E3 · A11 INPUTS", new Vector2(134, 27.6f), 1.5f));
            c.titles.Add(("E4 · A11 OUTPUTS → DRIVES AND SPINDLE", new Vector2(252, 26.0f), 1.5f));
            c.titles.Add(("L-UNSW · live when QF0 is ON", new Vector2(70, 255.2f), 0.85f));
            c.titles.Add(("N · all N conductors meet on XN", new Vector2(70, 250.2f), 0.85f));
            c.titles.Add(("L distribution block XL (switched by KM1)", new Vector2(196, 211.2f), 0.85f));
            c.titles.Add(("N distribution block XN (KM1 switches L only)", new Vector2(196, 206.2f), 0.85f));
            c.titles.Add(("+24 V SIGNAL (F2 out) → A11, sensors", new Vector2(70, 137.3f), 0.7f));
            c.titles.Add(("+24 V (G2, unfused)", new Vector2(26, 149.5f), 0.8f));
            c.titles.Add(("+24 V CONTROL (F1 out)", new Vector2(62.2f, 129.4f), 0.8f));
            c.titles.Add(("0 V (G2 −V) — daisy-chained, no TB0V · wire colour blue/white", new Vector2(70, 9.3f), 0.8f));
            c.titles.Add(("HL wire-OR (chained at the connectors)", new Vector2(150, 64.0f), 0.65f));
            c.titles.Add(("Rev 0.7 — wired directly: no terminal blocks, only the 230 V L / N distribution blocks XL, XN (max. 2 conductors per device terminal)", new Vector2(-2, 306.5f), 1.2f));
        }
    }
}
