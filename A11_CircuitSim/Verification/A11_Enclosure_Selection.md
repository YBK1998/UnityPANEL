# Enclosure selection — rev 0.7 (wired directly)

**Bought: 800 W × 1000 H × 250 D mm steel wall cabinet** with a swing door hinged on the left (seen from the front), no
removable panels, and its own galvanised mounting plate (size **assumed 750 × 955** — measure it). The layout below needs a
539 × 955 field (the smallest standard size that holds every part at datasheet size with 40 × 60 ducts, the § 6
clearances and no terminal blocks is 600 × 1000 × 250); on the 800-wide plate it sits centred. Rev 0.7 uses the ≈ 100 mm
spare on each side for two extra 40 × 60 ducts: the **power lane VP** (left: incoming mains and the shielded VFD cable W1 only)
and the **motor lane VM** (right: the three stepper motor cables only).

## Part sizes used (W × H × D, mm)

| Part | Size | Source | Note |
|---|---|---|---|
| Contactor KM1, Hyundai UMC65 | 55 × 106 × 111 | Hyundai U-series catalogue gives 55 × 94 × 105 for the UMC40–65 frame ([valtronic.bg](http://valtronic.bg/media/content/HTMLcatalogs/EN/Hyundai/Hyundai_U_series-Contactors/26.html)); the U-series manual gives 55 × 106 × 111 ([automation24.ir](https://www.automation24.ir/media/uploads/files/products/Hyundai_U_series_Contactors_User_Manual.pdf)) | larger value used |
| MCB QF0–QF4 | 18 per pole × 85 × 75 | Acti9 iC60 class, 18 mm per pole ([RS](https://za.rs-online.com/web/p/mcbs/7768980)) | QF0 2-pole = 36 |
| Relay K1, Finder 55.34.9.024.0000 on the owner's socket (looks like a Schneider RXZE2S114M, not confirmed; IEC + NEMA marked) | drawn at RXZE2S114M size 27 × ≈ 80 × 61, measure yours ([Schneider datasheet](https://www.se.com/uk/en/product/RXZE2S114M/socket-separate-contact-10-a-relay-type-rxm2-rxm4-screw-connector-250-v-ac); height 79–82.5 by source) | socket: [Rapid](https://www.rapidonline.com/finder-94-04-relay-socket-250v-10a-for-55-32-and-55-34-series-relays-51-0638) (for 55.32 / 55.34); [Finder 55 datasheet](https://cdn.findernet.com/app/uploads/S55EN.pdf): 4 CO 7 A, AC15 350 VA, basic insulation between adjacent contacts, 94.04 takes 2 × 2.5 mm²; fitted without the 99.02 LED + diode module (recommended if the socket takes it, C11) | relay body drawn approximately; pole 1 latch, pole 2 → X7, pole 3 empty (gap), pole 4 → KM1 coil |
| L / N distribution blocks XL, XN | 25 × 45 × 45 each (assumed) | 1-pole DIN-rail distribution block, ≈ 6 points | right end of the rail, after XS1 |
| Fuse holders F1, F2, RT18-32 1P | 18 × 79 × 60 | 10 × 38 mm fuse links, gG 2 A; one 18 mm module ([RT18-32X listing](https://makerselectronics.com/product/fuse-holders-500v32a-10x38-rt18-32x/)); [VIOX](https://viox.com/product/viox-rt18-32-din-rail-mounted-fuse-holder) gives a different length for its 1P | outline approximate |
| VFD U1, FC300-2.2G-S2 | 130 × 175 × 175 | FC300 outline not published; same-class 2.2 kW 1-phase drive measured W130 × H175 × D175 ([MyCNCUK](https://www.mycncuk.com/archive/index.php/t-13369.html)); that thread quotes 50 mm side / 120 mm vertical clearance | **measure your unit** |
| PSU G1 48 V 15 A (S-720-48 class) | 124 × 238 × 65 | seller data 238 × 124 × 65 ([Newegg](https://www.newegg.com/p/2VH-02N1-008X1)) | upright |
| PSU G2 24 V 20 A (S-500-24 class) | 113 × 215 × 50 | seller data 215 × 113 × 50 ([china.org.ru](https://china.org.ru/product/60508897844)) | upright; DIN-rail alternative Mean Well SDR-480-24 85.5 × 125.2 × 128.5 ([DigiKey](https://azcus.digikey.com/en/products/detail/mean-well-usa-inc/SDR-480-24/7706560)) |
| Drives A2–A4, Leadshine DMA860H | 97 × 151 × 48 | distributor data ([UNIT Electronics](https://uelectronics.com/producto/dm860h-driver-para-motor-a-pasos/)); Leadshine's drawing is image-only ([leadshine.com](https://www.leadshine.com/legacys/DMA860H.html)) | |
| A11E interface board | 150 × 100 | no published outline — estimated | **measure your board** |
| EMI filter Z1, HD2AC-20A | 85 × 54 × 30 | HD2AC-20A not published; same-class 20 A filter Schaffner FN2020-20 ([RS](https://benl.rs-online.com/web/p/power-line-filters/1705106)) | |
| Braking resistor R1, 100 Ω 600 W | 232 × 107 × 50 | RXLG/600/100 ([Motor Control Warehouse](https://www.motorcontrolwarehouse.co.uk/rxlg-600-100-dynamic-braking-resistor-1.html)) | right side wall |
| Schuko outlet XS1 (DIN rail) | 44 × 75 × 66 | Hager SNS016 class, terminals 2 × 1–2.5 mm² ([lemu.dk](https://www.lemu.dk/en/product/outlet-schuko-for-din-rail-id-7918001408)) | |
| Wire ducts | 40 × 60 | § 2.2 | 5 horizontal + 2 vertical |

## Fit (mounting plate)

Vertical stack (mm): duct 40 + **row A** (DIN rail: QF0, Z1, KM1, QF1–QF4, K1, F1, F2, XS1, XL, XN; KM1 is the tallest at 106) 110
+ duct 40 + **row B** (VFD 175 + **105 clear above**, PSU 48 V 238) 285 + duct 40 + **row C** (3 × DMA860H) 160 + duct 40
+ **row D** (A11E board 100 + 100 spare) 200 + duct 40 = **955 = plate height**.

Widths inside the two vertical ducts (449 mm usable): row A 438 (K1 27 + F1 / F2 2 × 18 moved the Schuko 14 mm right; XL + XN 2 × 25 at the right end), row B 432 (VFD 130 + **50 clear** + PSU 124 + 10 + PSU 113),
row C 315. Depth: VFD 175 + plate offset → **60 mm to the door** at the VFD; the door contact blocks (≈ 45 mm) sit in front of
row A, not the VFD.

| Enclosure (W × H × D) | Plate | Fits? | Why |
|---|---|---|---|
| 600 × 600 × 210 (AE 1060.500) | — | no | height |
| 600 × 760 × 210 (AE 1076.500) | ≈ 539 × 715 | no | stack 955 > 715; depth 210 leaves 20 mm in front of the VFD |
| 600 × 800 × 250 (§ 6) | ≈ 540 × 755 | no | stack 955 > 755 (the § 6 layout also has no room for 100 mm above the VFD) |
| 800 × 800 × 250 | ≈ 740 × 755 | yes, tight | A11E beside the drives (row C 485 mm); 0.16 m³ and 200 mm wider |
| 600 × 1000 × 250 (AE 1090.500) | 539 × 955 | yes | all clearances, 100 mm spare in row D; 0.15 m³ — smallest that fits |
| **800 × 1000 × 250 (bought)** | **≈ 750 × 955 (measure)** | **yes** | same layout centred, ≈ 100 mm spare each side; 0.20 m³ — **in use** |
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

- **VFD supply rating.** 2.2 kW single-phase drives draw ≈ 24 A at full load and their manuals ask for 30 A protection
  ([OneFinity forum, quoting drive manuals](https://forum.onefinitycnc.com/t/simple-answer-on-breaker-size/28670)). The FC300 sits
  behind QF1 **C16**, a **20 A** EMI filter and the **C25** main. Read the FC300 nameplate input current and re-rate QF1, Z1, QF0
  and the 2.5 mm² supply path together (4 mm² for 25–32 A).
- **2.5 mm² behind the C25 main** (L: 103, 105, 109, 111, 114, 117, 120; N: 104, 106): IEC 60204-1 Table 6 (B2) gives 18.3 A.
  Either the supply path goes to 4 mm², or the main comes down — which conflicts with the VFD current above. This is
  **reminder C14** (schematic, tooltips, parts list); rev 0.7 does not change it. The KM1 coil circuit (130, 131) is now
  behind QF4 (C6) and passes.
- **48 V drive feeds** 150–155 are 1.5 mm² (13.5 A) on an unfused 15 A supply: use 2.5 mm² or one fuse per drive.

## The bought enclosure

- **Door:** hinged on the left, seen from the front. The door wires cross on the hinge side in a spiral-wrapped service
  loop (≈ 0.5 m slack); the braided strap (wire 142) runs from the door earth stud to the earth bar.
- **No removable panels:** the 10 cable entries are drilled / punched through the bottom wall, 120 mm from the back:
  mains gland (x = −350) and HDC-HA (x = −290) under the power lane, 3 × X/Y/Z-LIM GX16-4 and the XT GX16-5 near the A11
  board (x = −190 … −70, 40 mm pitch), the pump gland (x = +215), and 3 × GX16-6 motor connectors under the motor lane
  (x = +285 … +365). Deburr; leave bare metal under the HDC-HA housing and the GX16 nuts (shield / shell bond).
- **Fan and outlet:** 124 × 124 cut-outs for the Rittal SK 3238.124 filter fan (left wall) and SK 3238.200 outlet
  filter (right wall), cut on assembly.
- **Body earth:** M6 stud on the inside of the left wall (wire 139 to the earth bar). Use the body's own earth stud if it
  has one.
- **Segregation:** VP (far left) carries only the incoming mains and the shielded VFD cable W1 (shield 360° at the EMC clamp
  on the plate and in the HDC-HA hood). VM (far right) carries only the stepper motor cables (LiYCY 4 × 1.0, shield clamped
  at the drive end). The door bundle and the 24 V wires cross VP at 90°.
- **Mounting plate:** the simulator assumes 750 × 955 with its front face 12.5 mm from the back outer face. Measure both
  and set `Cabinet3D.PlateW`, `PlateH` (and `Zb` if the offset differs); the wire lengths follow.

The fabricated 600 × 1000 × 250 enclosure (A11-ENC, `Enclosure_SW/`) was dropped when this enclosure was bought.
