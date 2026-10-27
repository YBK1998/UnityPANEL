# Enclosure selection — rev 0.7 (wired directly)

**Bought: 800 W × 1000 H × 250 D mm steel wall cabinet** with a swing door hinged on the left (seen from the front), no
removable panels, and its own galvanised mounting plate (**730 W × 930 H, measured 2026-10-26**). The layout below needs a
539 × 930 field (the smallest standard size that holds every part at datasheet size with 40 × 60 ducts, the § 6
clearances and no terminal blocks is 600 × 1000 × 250); on the 730-wide plate it sits centred. Rev 0.7 uses the ≈ 95 mm
spare on each side for two extra 40 × 60 ducts: the **power lane VP** (left: incoming mains and the shielded VFD cable W1 only)
and the **motor lane VM** (right: the three stepper motor cables only).

## Part sizes used (W × H × D, mm)

| Part | Size | Source | Note |
|---|---|---|---|
| Contactor KM1, Hyundai UMC65 | 55 × 106 × 111 | Hyundai U-series catalogue gives 55 × 94 × 105 for the UMC40–65 frame ([valtronic.bg](http://valtronic.bg/media/content/HTMLcatalogs/EN/Hyundai/Hyundai_U_series-Contactors/26.html)); the U-series manual gives 55 × 106 × 111 ([automation24.ir](https://www.automation24.ir/media/uploads/files/products/Hyundai_U_series_Contactors_User_Manual.pdf)) | larger value used |
| MCB QF1–QF4 | 18 × 85 × 75 | width **measured** (18 each); height / depth Acti9 iC60 class ([RS](https://za.rs-online.com/web/p/mcbs/7768980)) | |
| Main MCB QF0 C25 2P | 53 × 85 × 75 | width **measured** (53); height / depth Acti9 iC60 class | 2 poles drawn at 26.5 mm pitch |
| Relay K1, Finder 55.34.9.024.0000 on the owner's socket (looks like a Schneider RXZE2S114M, not confirmed; IEC + NEMA marked) | 28 × 76 × ≈ 70 | socket 28 × 76 **measured**; depth with the relay RXZE2S114M class ([Schneider datasheet](https://www.se.com/uk/en/product/RXZE2S114M/socket-separate-contact-10-a-relay-type-rxm2-rxm4-screw-connector-250-v-ac)); socket: [Rapid](https://www.rapidonline.com/finder-94-04-relay-socket-250v-10a-for-55-32-and-55-34-series-relays-51-0638) (for 55.32 / 55.34); [Finder 55 datasheet](https://cdn.findernet.com/app/uploads/S55EN.pdf): 4 CO 7 A, AC15 350 VA, basic insulation between adjacent contacts, 94.04 takes 2 × 2.5 mm²; fitted without the 99.02 LED + diode module (recommended if the socket takes it, C11) | relay body drawn approximately; pole 1 latch, pole 2 → X7, pole 3 empty (gap), pole 4 → KM1 coil |
| L / N busbars XL, XN | 160 × 16 × 42 each, 12 points | **measured** (owner's parts) | vertical on the plate, right of the right vertical duct, top 40 mm below the plate top |
| Earth bars PE 1, PE 2 | 150 × 25 copper on two 17 mm insulators, 4 points each | **measured** (owner's parts) | bottom wall; linked by wire 138; up to 2 conductors per point (owner decision, deviation from IEC 60204-1) |
| Fuse holders F1, F2, RT18-32 1P | 17 × 79 × 60 | width **measured** (17); 10 × 38 mm fuse links, gG 2 A ([RT18-32X listing](https://makerselectronics.com/product/fuse-holders-500v32a-10x38-rt18-32x/)) | height / depth approximate |
| VFD U1, FC300-2.2G-S2 | 140 × 205 × 160 | **measured** | 100 mm clear above, 50 mm to G1 |
| PSU G1 48 V 15 A (S-720-48 class) | 125 × 240 × 65 | **measured** | upright |
| PSU G2 24 V 20 A (S-500-24 class) | 115 × 215 × 50 | **measured** | upright |
| Drives A2–A4, Leadshine DMA860H | 52 × 150 × 97 | **measured**, side-mounted (52 × 150 on the plate, 97 out) | terminals face the door in one column; signal connector at the top (→ D2), power connector at the bottom (→ D3, motor cables straight into VM) — **as on the owner's drives** |
| A11E interface board | 153 × 90 × 20 (35 with the screw terminals) | **measured** | landscape, terminal strips along both long edges (as drawn before) |
| EMI filter Z1, HD2AC-20A | 51 × 51 × 30 | **measured**; plate mounted (not DIN rail): 2 terminals (L, N in) on one side, 3 (L, N out, PE) on the other | far left of row A, LINE side down |
| Braking resistor R1, 800W100RJ (100 Ω 800 W tube) | Ø 60 × 300, axis 60 from the flange | **measured** | vertical on the right side wall below the air outlet, 25 mm below the busbars, ≥ 50 mm in front of the ducts; bare and very hot while braking |
| Fan M2, 24 V DC | 80 × 80 × 25 | 80 × 80 **measured**, 25 deep assumed | inside the left wall behind its filter adapter |
| Filter adapters (fan and outlet), plastic, flanged | ≈ 95 × 95 × 15 outside the wall | owner's parts; "slightly larger than 80 × 80" — **flange size assumed** | one on the fan (left wall), one on the outlet (right wall); 80 × 80 cut-outs |
| Schuko outlet XS1 (DIN rail) | 44 × 75 × 66 | Hager SNS016 class, terminals 2 × 1–2.5 mm² ([lemu.dk](https://www.lemu.dk/en/product/outlet-schuko-for-din-rail-id-7918001408)) | |
| Wire ducts | 40 × 60 | § 2.2 | 5 horizontal + 2 vertical |

## Fit (mounting plate)

Vertical stack (mm): duct 40 + **row A** (Z1 on the plate, then the DIN rail: QF0, KM1, QF1–QF4, K1, F1, F2, XS1; KM1 is the tallest at 106) 110
+ duct 40 + **row B** (VFD 205 + **100 clear above**, PSU 48 V 240) 310 + duct 40 + **row C** (3 × DMA860H, 150 high) 160 + duct 40
+ **row D** (A11E board 90 + 60 spare) 150 + duct 40 = **930 = plate height**. (The measured VFD is 30 mm taller than the
175 first assumed, so ducts D2 and D3 moved 25 mm down; the 90-high A11E board gives those 25 mm from row D.)

Widths inside the two vertical ducts (459 mm usable): row A: Z1 51 (plate) + DIN rail 330 (QF0 53 … XS1, ≈ 26 mm spare between QF0 and KM1) — ≈ 68 mm of plate spare right of the rail;
row B 440 (10 + VFD 140 + **50 clear** + PSU 125 + 10 + PSU 115 + 9 to the right duct), row C 270 (3 × 52 at 109 mm pitch, 57 mm between drives).
Depth: VFD 160 + plate offset → **≈ 75 mm to the door** at the VFD; drives 97 → ≈ 135 mm; the door contact blocks (≈ 45 mm) sit in front of
row A, not the VFD.

| Enclosure (W × H × D) | Plate | Fits? | Why |
|---|---|---|---|
| 600 × 600 × 210 (AE 1060.500) | — | no | height |
| 600 × 760 × 210 (AE 1076.500) | ≈ 539 × 715 | no | stack 955 > 715; depth 210 leaves 20 mm in front of the VFD |
| 600 × 800 × 250 (§ 6) | ≈ 540 × 755 | no | stack 955 > 755 (the § 6 layout also has no room for 100 mm above the VFD) |
| 800 × 800 × 250 | ≈ 740 × 755 | yes, tight | A11E beside the drives (row C 485 mm); 0.16 m³ and 200 mm wider |
| 600 × 1000 × 250 (AE 1090.500) | 539 × 955 | yes | all clearances, 100 mm spare in row D; 0.15 m³ — smallest that fits |
| **800 × 1000 × 250 (bought)** | **730 × 930 (measured)** | **yes** | same layout centred (row D 175), ≈ 95 mm spare each side; 0.20 m³ — **in use** |
| 600 × 1200 × 300 (AE 1260.500) | — | yes | larger than needed |
| 800 × 1200 × 300 (AE 1280.500) | 740 × 1155 | yes | larger than needed |

Sources for the enclosure sizes: [Rittal AE 1060.500 / 1076.500 / 1090.500 table](https://www.rittal.com/pdf-creator/variant/us-en/1060500),
[AE 1090.500 plate 539 × 955](https://www.rittal.com/pdf-creator/variant/au-en/5050445),
[AE 1260.500 / 1280.500](https://www.rittal.com/pdf-creator/variant/ch-de/1280500).

## What removing the terminal blocks saved

Rev 0.5 needed a second DIN rail for TB1, TB2, TB24+, TB24C, TB24S, TB0V, TB-HL and the PE terminal (≈ 70 mm row + 40 mm
duct) and a bottom row for TB-HL / TB24S / earth bar. In rev 0.6 K1 and the two fuse holders sit on the top rail, every
former terminal block is a daisy chain (max. 2 conductors per terminal, twin ferrules), and the earth bar is a busbar on
the bottom wall.

## Open items found by the wiring checks (decide before ordering)

- **VFD supply rating — OPEN again (reminder C14, 2026-10-26).** The full nameplate (FC300-2.2G-S2-B4C, 2.2 kW, 220 V 1~ in,
  0–220 V 3~ out) lists **11 A as the rated output current**, not the input. A 2.2 kW single-phase drive draws ≈ 22–24 A input at
  full load (Delta VFD022EL21A: 11 A out, 24 A in). Worst case behind Z1 ≈ 28–30 A (VFD 24 + PSUs ≈ 4–6.5 + pump). At full spindle
  load QF1 C16 trips, the 20 A filter is overloaded and QF0 C25 is marginal; light cuts (≈ 1 kW, ≈ 10–12 A input) are within all
  ratings. **Constraint: Z1 stays the HD2AC-20A — no higher-current filter is available (owner, 2026-10-27).** A larger
  QF1 alone does not help, because Z1 would still be the limit. Options that work with the 20 A filter, owner's decision:
  (a) **cap the spindle in the VFD** (current / power limit) at ≈ 1.5 kW, ≈ 15 A input: with the PSUs (≈ 4 A) and the pump
  that keeps Z1 under 20 A, and C16 fits. Check with a clamp meter on Z1 LINE L during a heavy cut. (b) Keep the parts and
  avoid long full-power cuts (the filter and C16 carry short peaks). (c) Feed the VFD ahead of Z1 so Z1 carries only the
  PSUs, pump and control (< 10 A) — but then the VFD, the main noise source, is unfiltered unless it has its own filter.
- **Supply path behind the C25 main:** 103–106, 109, 111, 114, 117, 120 are **4 mm²** (25 A, IEC 60204-1 Table 6 B2), so the
  conductors suit C25 — the open part of C14 is the device ratings (QF1, Z1, QF0) against the real input current above. Check that the EMI filter LOAD L terminal takes 2 × 4 mm² (105 + 111). The KM1 coil circuit (130, 131) is now
  behind QF4 (C6) and passes.
- **48 V drive feeds** 150–155 are 1.5 mm² (13.5 A) on an unfused 15 A supply: use 2.5 mm² or one fuse per drive.

## The bought enclosure

- **Door:** hinged on the left, seen from the front. The door wires cross on the hinge side in a spiral-wrapped service
  loop (≈ 0.5 m slack); the braided strap (wire 142) runs from the door earth stud to the earth bar.
- **No removable panels:** the 10 cable entries are drilled / punched through the bottom wall, 120 mm from the back:
  mains gland (x = −350) and HDC-HA (x = −290) under the power lane, 3 × X/Y/Z-LIM GX16-4 and the XT GX16-5 near the A11
  board (x = −190 … −70, 40 mm pitch), the pump gland (x = +215), and 3 × GX16-6 motor connectors under the motor lane
  (x = +285 … +365). Deburr; leave bare metal under the HDC-HA housing and the GX16 nuts (shield / shell bond).
- **Fan and outlet:** the owner's 80 × 80 mm 24 V fan blows in through its flanged plastic filter adapter on the left
  wall; the outlet in the right wall has the same 80 mm filter adapter (no fan). Two 80 × 80 cut-outs (check the
  adapter's own cut-out size before cutting), cut on assembly.
- **Braking resistor:** the 800W100RJ tube (Ø 60 × 300) stands vertically on the right wall on its two brackets, below
  the outlet so its heat leaves the box, 25 mm below the busbars and ≥ 50 mm in front of the VR duct (the motor lane VM
  starts 175 mm below it). Its surface
  gets very hot while braking: silicone leads (128, 129), nothing plastic touching it.
- **Body earth:** M6 stud on the inside of the left wall (wire 139 to the earth bar). Use the body's own earth stud if it
  has one.
- **Segregation:** VP (far left) carries only the incoming mains and the shielded VFD cable W1 (shield 360° at the EMC clamp
  on the plate and in the HDC-HA hood). VM (far right) carries only the stepper motor cables (LiYCY 4 × 1.0, shield clamped
  at the drive end). The door bundle and the 24 V wires cross VP at 90°.
- **Mounting plate:** 730 × 930 (measured). The simulator puts its front face 12.5 mm from the back outer face (assumed —
  measure it and set `Cabinet3D.Zb` if it differs); the wire lengths follow.

The fabricated 600 × 1000 × 250 enclosure (A11-ENC, `Enclosure_SW/`) was dropped when this enclosure was bought.

## Duct and rail cut list (730 × 930 plate, 2026-10-26)

Three 2 m lengths of 40 × 60 slotted duct. The motor lane VM only carries the motor cables from the third horizontal duct
down to the floor connectors, and the drive power plugs (motor cables) are at the bottom of the drives, so it starts at the
fourth horizontal duct and is 230 mm long; that frees the upper right strip for the L / N busbars and the braking resistor.

| Length | Pieces | Leftover |
|---|---|---|
| 1 | 930 power lane VP + 930 left vertical VL | ≈ 136 mm |
| 2 | 930 right vertical VR + 459 + 459 horizontals | ≈ 146 mm |
| 3 | 230 motor lane VM + 459 + 459 + 459 horizontals | ≈ 385 mm |

Positions from the plate edges: VP 22.5–62.5 mm from the left edge, VL 95.5–135.5 mm from the left edge, VR 95.5–135.5 mm
from the right edge, VM 22.5–62.5 mm from the right edge (from 700 mm below the plate top to the bottom). Horizontals between
VL and VR, top edges at 0, 150, 500, 700 and 890 mm below the plate top (the third and fourth moved 25 mm down for the
measured VFD, 2026-10-26). DIN rail TS35: one piece of **330 mm**, centre line
95 mm below the plate top, from 196.5 mm to 526.5 mm measured from the plate's left edge (the EMI filter sits left of it, 140.5–191.5 mm).
