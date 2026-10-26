# A11 circuit self-test

Generated 2026-10-26 21:40 from `A11Layout.Build()` (the same model the app runs).

**62 passed, 0 failed.**

## A · Drawing geometry (the drawn lines agree with the netlist)

| Result | Check | Detail |
|---|---|---|
| PASS | A1 Every wire starts and ends on its own terminal or on its own net | 154 conductors |
| PASS | A2 No conductor runs over a terminal of a different net |  |
| PASS | A3 No two nets are drawn on top of each other |  |
| PASS | A4 No wire end touches a conductor of a different net |  |
| PASS | A5 No two terminals share one position |  |
| PASS | A6 Every § 8 wire number is present exactly once | 109 numbered wires (rev 0.7 schedule: 109) |
| info | Crossings without connection (normal in a schematic) | 87 |

## B · § 7.1 tests with no power (Main CB OFF, meter on continuity)

| Result | Check | Detail |
|---|---|---|
| PASS | 1 TB1 ↔ TB2 → open |  |
| PASS | 2 TB1 ↔ TB0V, TB24+, PE → open |  |
| PASS | 3 TB0V ↔ PE → beep (PELV bond 148) |  |
| PASS | 4 TB24C ↔ K1 A1, no button → open |  |
| PASS | 5 ON pushed and held → beep |  |
| PASS | 6 ON held and E-stop pushed → open |  |
| PASS | 7 ON held and OFF pushed → open |  |
| PASS | 8 QF4 out ↔ contactor A2 (K1 not energized) → open |  |
| PASS | 9 K1 test lever pushed: QF4 out ↔ contactor A2 → beep (pole 4) |  |
| PASS | 10 E-stop NC2: X7 ↔ TB0V → beep released, open pushed, open on tool-setter over-travel (347 in series) |  |
| PASS | 11 PSU 48V +V ↔ −V → not 0 Ω |  |
| PASS | 12 TB24+ ↔ TB0V → not 0 Ω |  |
| PASS | 13 5V, PULSE, DIR of each axis ↔ TB24+ and PSU 48V +V → open |  |

## C · § 7.2 power-up in steps

| Result | Check | Detail |
|---|---|---|
| PASS | Stage 1 / C2: TB24S +24 V, TB0V 0 V with the Main CB ON |  |
| PASS | Stage 1: the A11 starts (J10 power) |  |
| PASS | Stage 1: K1 and KM1 stay OFF until ON is pushed; L/N bus dead |  |
| PASS | 5 Push ON: K1 energizes and stays energized (latch 11-14) |  |
| PASS | 6 Push OFF: K1 de-energizes |  |
| PASS | 7 ON, then E-stop: K1 de-energizes and stays de-energized after release |  |
| PASS | 9 Push ON: contactor closes, yellow lamp on, L/N bus live |  |
| PASS | 10 Push OFF: contactor opens, yellow lamp off |  |
| PASS | 10 Same test with the E-stop |  |
| PASS | 11 / C3: QF2 ON → G1 48 V on the three drives, drive LEDs green |  |
| PASS | C5: A11 5 V reaches PUL+ / DIR+ of each drive (common anode) |  |
| PASS | Drives: 48 V side stays isolated from 0 V |  |

## D · Functions (§ 3, § 4, § 5, § 7.3) and fault cases

| Result | Check | Detail |
|---|---|---|
| PASS | § 3: E-stop de-energizes VFD, 48 V drives and pump |  |
| PASS | § 3: A11 and the 24 V side stay energized, A11 sees the E-stop |  |
| PASS | § 4.3: red ALARM lamp lit (Y5 sinks lamp −) |  |
| PASS | § 4.2 / 5.1: OFF drops K1 and KM1 but X7 stays closed: no A11 alarm, red lamp off | A11 alarm: none |
| PASS | § 4.2: after a mains failure K1 stays OFF — no automatic restart |  |
| PASS | F1 open: K1 / KM1 drop, lamps off, A11 keeps running (F2) |  |
| PASS | F2 open: A11 and sensors lose power |  |
| PASS | QF4 open: G2 off → K1 drops → KM1 opens |  |
| PASS | § 7.3 / test 4: S1…S8 → Y4Y3Y2 000…111 → P030…P037 frequency | S1: Y4Y3Y2 000 → 100.0 Hz 6000 rpm; S2: Y4Y3Y2 001 → 133.3 Hz 7998 rpm; S3: Y4Y3Y2 010 → 166.7 Hz 10002 rpm; S4: Y4Y3Y2 011 → 200.0 Hz 12000 rpm; S5: Y4Y3Y2 100 → 250.0 Hz 15000 rpm; S6: Y4Y3Y2 101 → 300.0 Hz 18000 rpm; S7: Y4Y3Y2 110 → 350.0 Hz 21000 rpm; S8: Y4Y3Y2 111 → 400.0 Hz 24000 rpm;  |
| PASS | Stage 4 step 19: the E-stop stops the spindle (Y1 off, VFD unpowered, coasting) |  |
| info | Note | On E-stop the VFD loses its supply, so the spindle coasts (no braking through R1). See findings. |
| PASS | § 5.1: each NPN sensor activates its own input; far limits wire-OR on X6 → alarm + red lamp | B1 X home → X1 ✓; B2 X+ limit → X6 ✓; B3 Y home → X2 ✓; B4 Y+ limit → X6 ✓; B5 Z home → X3 ✓; B6 Z− limit → X6 ✓;  |
| PASS | § 5.1: tool-setter touch → X4; X8 never active (not connected) |  |
| PASS | § 5.1: E-stop released → X7 active (NC level) |  |
| PASS | Rev 0.7: tool-setter over-travel (347 in series with E-stop NC2) → X7 opens → A11 E-STOP; K1 stays on |  |
| PASS | § 5.1: A2 ALM → X5 driver alarm (parallel, X5 = NO assumed) |  |
| PASS | § 5.1: A3 ALM → X5 driver alarm (parallel, X5 = NO assumed) |  |
| PASS | § 5.1: A4 ALM → X5 driver alarm (parallel, X5 = NO assumed) |  |
| PASS | Stage 5 step 22: home the three axes (Z+, X−, Y− sensors) | X 3.50 Y 2.70 Z -1.13 · Homed: X−, Y−, Z+ references set |
| PASS | § 4.3: green WORK lamp lit while a job runs (Y6) |  |
| PASS | Stage 5 step 23: a hard-limit sensor stops the job and shows an alarm |  |
| PASS | QF2 OFF: A11 still sends pulses (DRO moves) but the axis does not move |  |
| PASS | § 5.1: J8 24V (wire 168) supplies the input stage |  |

## F · Wiring rules (rev 0.7: wired directly; no terminal blocks, only the 230 V L / N distribution blocks)

| Result | Check | Detail |
|---|---|---|
| PASS | F1 Max. 2 conductors per device terminal (twin ferrule); earth bars ≤ 8 each (4 points × 2, owner decision), XL / XN busbars ≤ 12 (one per point) | 30 terminals take 2 conductors (twin ferrule) |
| PASS | F2 No terminal blocks or virtual bus nodes in the netlist (only the L / N busbars XL, XN) |  |
| PASS | F3 Every PE point (incl. the pump outlet, wire 149, and the enclosure body, wire 139) and the PELV bond reach the earth bar |  |
| PASS | F4 The fan is protected by F1 (rev 0.5: unfused on TB24+) |  |
| PASS | F5 The pump outlet and the 48 V PSU each take N from the N block XN (no N looped through the outlet) |  |
| PASS | F6 Sensor 0 V reaches every sensor connector without passing through a drive ALM screw |  |
| PASS | F7 KM1 coil circuit fed from QF4 out (C6), not from the unprotected QF4 input |  |
| PASS | F8 230 V only on K1 pole 4; pole 1 carries 24 V only; poles 2 and 3 empty (pole 3 = gap) |  |
| info | Cross-section vs protection | see Verification/A11_WiringRules_rev07.md (IEC 60204-1 Table 6) |

## E · No short circuit in any simulated state

| Result | Check | Detail |
|---|---|---|
| PASS | No net carries two different potentials (L/N, L/PE, AC/DC, +24/0 V, +48/48 V return) |  |
