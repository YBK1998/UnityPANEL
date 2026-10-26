# A11 circuit — verification (manual rev 0.5 → wiring rev 0.7)

The simulator now runs **rev 0.7**: the rev 0.5 circuit wired directly, without terminal blocks except two 230 V
distribution blocks (see *Rev 0.6* and *Rev 0.7* below).
The PDF cross-check is kept against the frozen rev 0.5 netlist (`netlist_rev05.json`).

Three independent checks run against the same model the app uses (`A11Layout.Build()`):

| Check | How | Result |
|---|---|---|
| **Netlist vs PDF** | `verify_netlist.py` reads every § 8 table row from the PDF with pdfplumber. It maps each simulator terminal to the exact From / To text the manual uses, then compares row by row. | **115 / 115 rows match** (see `A11_Netlist_vs_PDF.md`) |
| **Circuit behaviour** | `A11SelfTest` (Unity: *A11 Sim → 4 · Run circuit self-test*) runs the § 7.1 dead tests, the § 7.2 power-up stages, the § 4 / § 5 / § 7.3 functions and fault cases. After every 20 ms step it scans for a short circuit. | **62 / 62 pass** (see `A11_SelfTest_Report.md`) |
| **Drawing geometry** | Same self-test, part A. Every drawn wire starts and ends on its own terminal or net. No line runs over another net's terminal. No two nets overlap. No wire end touches another net. All wire numbers are present once. | **pass** |
| **Wiring rules** | `wiring_rules.py`: R1 conductors per terminal, R2 PE coverage, R3 cross-section vs upstream protection (IEC 60204-1 Table 6, B2), R4 every endpoint is a real part, R5 electrical equivalence with rev 0.5, R6 neutral conductors (return current of every branch behind them). | rev 0.5: **21 problems**; rev 0.7: **9** (all the supply path behind the C25 main: reminder C14) |

To re-run: open the project in Unity, run *A11 Sim → 4*, then
`python -I Verification/verify_netlist.py United_Box_A11_Manual_rev0.5.pdf Verification/netlist_rev05.json Verification/A11_Netlist_vs_PDF.md`
and `python -I Verification/wiring_rules.py` (see its header for arguments).

## Errors found in the simulator and fixed

1. **Wire 171 direction.** The simulator had *Fan − → TB0V*; § 8 has *TB0V → Fan −*. Same conductor, but the From/To order did not match the schedule. Fixed.
2. **J8 24 V not modelled.** The A11 input stage is supplied from J8 24V (wire 168). The model now needs it. Without 168, no input can be active, so X7 reads open and the A11 shows E-STOP. This is fail-safe and agrees with § 5.1.
3. **141, 142 and 147** (backplate, door strap and DIN-rail earth) were only drawn as PE-bar labels. They are now real conductors, so all 115 § 8 numbers exist as wires.

## Findings in the manual / design (for review — the simulator follows the manual as written)

| # | Finding | Where | Suggestion |
|---|---|---|---|
| F1 | **The fan branch is not fused.** Wires 170 / 171 (0.5 mm²) connect to TB24+ ahead of F1 and F2. The only protection is the 20 A PSU current limit, and a 0.5 mm² wire can overheat before G2 folds back. 162 / 164 (TB24+ → fuse inputs) are also unfused, but they are short jumpers. | § 4.1, § 8.3, sheet E2 | **Done in rev 0.6:** the fan now hangs behind F1 (170 from KM1.43). |
| F2 | **F2 (2 A) feeds a board specified as 24 V / 3 A**, plus the six sensors and the tool setter. If "3 A" is the A11's real demand and not just a supply recommendation, F2 can nuisance-blow at power-up. | § 5 (J10), § 2.2 | Measure the A11 + sensor current at power-up (inrush) and size F2 from that. Get the real J10 current from the supplier with item 3. |
| F3 | **E-stop / OFF is a stop category 0 for the spindle.** KM1 removes the VFD supply, so R1 cannot brake. A 2.2 kW spindle at 24 000 rpm coasts for several seconds; the simulator shows this as "NO POWER — coasting". | § 3, § 4.2 | Accept and document, or use a category 1 stop. In that case Y1 / an E-stop contact stops the VFD with braking, and an off-delay drops KM1 after the ramp. |
| F4 | **The Z axis may drop on E-stop / OFF.** The 48 V PSU is on the KM1 bus, so the drives lose holding torque. A 5 mm-pitch SFU2005 under the spindle weight can back-drive. | § 3 | Check on the machine with power removed. If Z falls: use a brake motor, a counterbalance, or keep G1 unswitched and stop motion via the drive enable. |
| F5 | **The home ends have no hard limit.** X−, Y− and Z+ have only the home sensors (X1–X3). Only X+, Y+ and Z− are wired to X6. Outside homing, travel past the home sensor is caught only by soft limits. | § 5.1, sheet E3 | Set the soft limits in § 7.3 and always home after power-up. Optionally add the home sensors to the hard-limit chain through diodes (not on the A11 — check A11 behaviour first). |
| F6 | **Wire count text.** § 8 says "approx. 117 numbered wires". The schedule has **115** numbered rows. | § 8 end | Change the text to 115. |
| F7 | **KM1 coil orientation.** The L side goes to A2 and N to A1. This is harmless for an AC coil, but most drawings put L on A1. | § 4.2, wires 131 / 132 | No change needed. Keep the drawing and § 8 the same (they agree now). |

These were checked and agree with the manual: the K1 latch through S1 OFF (NC) and S0 NC1, no auto-restart after mains loss, the unswitched 24 V side, and the single PELV bond (148). Also: the 48 V side stays isolated from 0 V (no path through ALM or the opto inputs), the common-anode 5 V wiring with the PUL+ → DIR+ jumpers, the Y2–Y4 bit order → P030–P037 (S1 = 000 … S8 = 111), the NPN NO sensors → X1–X4, the far limits wire-OR on X6, the drive ALM wire-OR on X5 (parallel / NO assumed — open item 5), and X7 E-stop as NC.

## Rev 0.6 — wiring bugs in rev 0.5 and direct wiring

`wiring_rules.py` on the rev 0.5 schedule found **21 problems** (`A11_WiringRules_rev05.md`):

| Rule | Rev 0.5 problem |
|---|---|
| R1 | **TB24C carries 5 conductors** (163, 201, 220, 223, 225) on a 4-point block. **A1.X5 carries 3** (315, 317, 319) on a screw for 2. |
| R2 | **The Schuko outlet XS1 has no PE conductor.** The pump is a Class I load. Fixed with new wire **149** (XS1.PE → earth bar). |
| R3 | 14 conductors are thinner than their protection allows. The worst cases are **162 / 164 / 170 at 0.5 mm² on the unfused 20 A PSU output**, 160 at 1.5 mm² on 20 A, and 1.5 mm² (111, 117, 120, 130, 131) behind the C25 main. |
| R4 | **The "L bus" and "N bus" (4 conductors each) are not real parts.** § 2 and § 6 list no busbar or terminal for them. |

**Rev 0.6 removes every terminal block** (TB1, TB2, L/N bus, TB24+, TB24C, TB24S, TB0V, TB-HL, DIN PE). Each former block becomes a
daisy chain from device screw to device screw, with at most 2 conductors per screw (twin ferrule). The 24 V PSU uses its
duplicated +V / −V screws (fixed links in the model). The rev 0.6 schedule had 108 wires (superseded by `A11_Wire_Schedule_rev07.md`), 9 removed
(107, 108, 120, 122, 160, 161, 163, 165, 333), 2 new (149, and 139 for the enclosure body), and the 1.5 / 0.5 mm² power
jumpers raised to 2.5 mm². Wire 142 (door strap) is now a braided strap of at least 6 mm². A flexing protective
conductor that is not inside a cable needs more than 2.5 mm².

Proof that the circuit is unchanged (R5 compares every net of both revisions): only **three intended differences**.
1. XS1.PE joins the PE net (the R2 fix).
2. The fan + moves from ahead of F1 to behind F1 (finding F1).
3. BODY.T joins the PE net. The earth bar sits on insulators, so without wire 139 the enclosure body had no defined PE
   path (earth stud on the inside of the left wall).

The self-test ran on rev 0.6 at the time (57 / 57). Rev 0.6 still had **8 R3 FAILs**: the 2.5 mm² supply path behind the C25 main
(103, 105, 109, 111, 114, 117, 130, 131 — 18.3 A under B2). Two WARN groups remain: 150 / 152 / 154 at 1.5 mm² on the 15 A 48 V
supply, and 162 / 164 at 2.5 mm² on the 20 A 24 V supply. These depend on the real VFD input current (≈ 24 A for 2.2 kW
single-phase, more than QF1 C16 and the 20 A filter). They are listed as open items in `A11_Enclosure_Selection.md` and are not
changed, because the fix (a smaller main or 4 mm²) is a design decision.

## Rev 0.7 — red-team fixes (2026-10-26)

A wire-by-wire review against the datasheets of the parts on hand (Finder 55 / 94 series, Hyundai U-series contactor
manual, Leadshine DMA860H manual) found the items below. Rev 0.7 fixes them; `A11_Wire_Schedule_rev07.md` is the
schedule, `A11_WiringRules_rev07.md` the rule check.

| # | Finding | Rev 0.7 |
|---|---|---|
| 2 | **Hard limits fail silently.** X+, Y+ and Z− are NPN NO sensors wire-ORed on X6 (they must stay NPN NO), and their 0 V ran through A11 GND → the three drive ALM− screws → the connectors: unplugging one drive signal connector removed the supply of every sensor. | Sensor 0 V (337) now comes straight from the A11 J8 GND screw; the ALM− chain (316) starts at the A11 J7 output GND. Self-test F6 proves no sensor depends on a drive ALM screw. A broken NO wire still cannot be detected electrically: do the hard-limit check (§ 7.2 step 23) at commissioning and after any connector work, and set the soft limits. |
| 3 | Leadshine's DMA860H manual shows no ALM output. | The drives on hand have ALM+ / ALM− (checked by the owner). Wires 315–320 stay; the polarity test (C9, § 7.4 test 3) stays. |
| 4 | **Spindle power and unfiltered mains shared ducts** with the step / sensor wires (manual § 6 rule 4; Leadshine: PUL / DIR ≥ 10 cm from motor wires). | New **power lane VP** (40 × 60 duct on the spare plate width, far left): only the incoming mains (101, 102, 140) and the VFD cable W1. Mains gland and HDC-HA moved to the bottom wall under it. New **motor lane VM** (far right): only the three stepper motor cables, GX16-6 connectors under it. Sensor connectors moved next to the A11 board. |
| 5 | Spindle cable shield bonded at the VFD end only (pin 4). | 123–126 are the cores of **one 4G1.5 shielded cable W1**; its shield (127) is clamped **360° at both ends**: EMC clamp on the plate (bonded to VFD PE) and the HDC-HA metal hood; EMC gland at the spindle. Pin 4 is not used. |
| 6 | Supply path rating (2.5 mm² behind C25, 20 A filter, C16 for a ≈ 22–27 A VFD). | **Not changed — reminder C14** on the schematic, in the QF0 / QF1 / VFD tooltips, the parts list and here. R3 / R6 list 103–106, 109, 111, 114, 117, 120. |
| 8 | Neutral 116 (1.5 mm²) carried the N of the pump outlet and the 48 V PSU; the 48 V PSU N passed through the outlet screw. | **N distribution block XN** (6 points): every N conductor has its own point (106 in; 113, 116, 119, 122, 132 out). **L distribution block XL** (switched L): 109 in; 114, 117, 120 out. KM1 switches L only (pole 3L2-4T2 unused; allowed in TN, the loads are dead when L opens). New rule R6 checks every N conductor against the branches behind it. |
| 10 | KM1 coil circuit protected only by the C25 main (130 / 131 had to be 2.5 mm², too big for the M3.5 coil terminal, 1.25–2 mm²). | 130 now starts at **QF4 out (C6)**; 130 / 131 are 1.5 mm². Self-test F7. |
| 11 | K1 (55.34, 4-pole) has **basic** insulation only between adjacent contacts (2.5 kV, OVC II) and coil–contacts; 24 V on pole 1 sat next to 230 V on pole 2. | Cheap improvement: **230 V moved to pole 4** (41-44), **pole 3 left empty** as the gap, pole 2 spare. Self-test F8. |
| 12 | The E-stop has **one** NC contact (no NC2 for X7). | A first try fed X7 through K1 pole 2; then OFF also raised the A11 E-STOP alarm and lit the red lamp. Final: an **added ZB2-BE102 NC block** on the E-stop base gives NC2 again: 210 = NC2 → X7. OFF no longer touches X7 (self-test: "OFF drops K1 and KM1 but X7 stays closed"). |
| — | Tool-setter over-travel (347) was not connected. | **347: XT pin 3 → E-stop NC2 (S0.3)**: the over-travel NC is in series with NC2 → X7, so the E-stop, over-travel or an unplugged setter stops the A11. 211 (0 V to NC2) is gone; NC2 gets its 0 V through the over-travel loop. Fit a dummy plug (pins 3-4 bridged) when the setter is not used. Self-test: dead test 10 and the function test. |
| — | Wire colours did not follow IEC 60204-1 13.2.4. | L black, N light blue, PE green/yellow, AC control (KM1 coil) red, DC power (48 V, brake resistor) black, +24 V / DC control / signals dark blue, 0 V blue/white. |

Checked and confirmed from the datasheets: the 55.34 AC15 rating (350 VA) covers the UMC65 AC coil (inrush 200 VA,
sealed 15 VA) — open item 9.1-2 closed; the 94.04 socket takes 2 × 2.5 mm² stranded at 0.5 Nm. K1 sits on a socket that looks like a Schneider RXZE2S114M — exact part **not confirmed**; the Schneider data are used as
the assumed limits (10 A / 250 V; M3 screws, 1 × 0.34–2.5 mm² or 2 × 0.34–1 mm² with ferrules)
marked IEC (top label, arrow up) and NEMA (bottom label, arrow down): A1 = 13, two A2 screws = 14, COM 11–41 = 9–12, NC 12–42 = 1–4, NO 14–44 = 5–8; pole 4 on the left, pole 1 on the right. The two A2 screws take 207 and 222 one each (no twin ferrule). A genuine RXZE2S114M has a slot for a
protection module: a Schneider RXM040W diode module (6–250 V DC) would give K1 coil suppression without extra wiring
(optional; check the slot first). K1 runs without suppression, which is acceptable.
C11 now warns that NEMA 14 is the coil A2: wire 206 (IEC 14) landed there would short the coil when ON is pushed; K1 contact numbers are 11-14 / 21-24 / 41-44 (open item
9.1-8 closed); the drives keep one feed each from the 48 V PSU (Leadshine: do not daisy-chain the drive supplies).

Still open: the supply path (C14), F2 vs the A11 3 A rating, stop category 0 / Z drop (F3, F4), and the items in § 9.1
that need measurements (Y-output rating, pulse current, ALM polarity, S-grade bit order).

## Cabinet fit at real component sizes (from the 3D model)

The 3D cabinet uses **datasheet sizes** and the bought **800 × 1000 × 250 enclosure** (swing door hinged on the left, its
own plate, assumed 750 × 955; the layout uses the centre 539 × 955, the spare width carries the power lane VP and the motor
lane VM). `A11_Enclosure_Selection.md` lists every part size and source, and compares the candidate enclosures. The model
routes 120 conductors (116 in the cabinet, plus the door wires), and a collision test confirms **0 conductors pass through a part**.

Findings from the earlier 600 × 800 model (rev 0.5):

| # | Finding | Status in rev 0.6 |
|---|---|---|
| L1 | **The § 6 row stack does not fit 800 mm at real sizes.** | Solved by the 1000 mm enclosure and by removing the terminal-block row. |
| L2 | **No 100 mm clearance above the VFD** (≈ 25 mm in 800 mm). | Solved: 105 mm above the VFD, and 50 mm beside it. |
| L3 | **The cable entry needs 10 entries, not "GX16 ×4"**: mains, pump cord, HDC-HA, 3 × GX16-6 (motors), 3 × GX16-4 (limits) and 1 × GX16-5 (tool setter). | Still applies. All 10 are modelled, drilled through the bottom wall (the bought enclosure has no gland plate): mains + HDC-HA under the power lane, motors under the motor lane, sensors near the A11. |
| L4 | **TB24C, the L/N bus and the DIN-rail PE terminal (147) are not in the § 6 DIN row.** | Gone: no terminal blocks. 147 now goes to an earth stud on the rail. |
| L5 | **Depth** | 250 mm leaves ≈ 60 mm between the VFD front and the door. The door contact blocks sit in front of row A, not the VFD. |

Open items in § 9.1 stay open (the simulator only assumes results): EMI filter 20 A vs C25 (reminder C14), Y-output rating, pulse current, ALM polarity, S-grade bit order and the 48 V PSU screw count. Closed by rev 0.7: KM1 coil inrush (200 VA ≤ 350 VA AC15), K1 contact numbers (55.34: 11-14 / 21-24 / 41-44), X8 (the over-travel now acts on X7 through K1 pole 2).
