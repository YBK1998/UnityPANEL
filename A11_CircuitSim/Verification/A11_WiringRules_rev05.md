# Wiring rule check — rev 0.5 (manual)

114 numbered conductors.
**21 problem(s).**

## R1 Conductors per terminal

| Terminal | Conductors | Capacity | Result |
|---|---|---|---|
| `TB0V.T` | 16 (148, 161, 167, 169, 171, 207, 211, 222, 316, 318, 320, 337, 338, 339, 346, 348) | 16 | PASS |
| `PEBAR.BAR` | 9 (140, 141, 142, 147, 143, 144, 145, 146, 148) | 12 | PASS |
| `TB24S.T` | 6 (165, 166, 168, 340, 341, 342) | 8 | PASS |
| `TB24C.T` | 5 (163, 201, 220, 223, 225) | 4 | **FAIL** |
| `TB1.T` | 4 (105, 107, 111, 130) | 4 | PASS |
| `TB2.T` | 4 (106, 108, 113, 132) | 4 | PASS |
| `LBUS.T` | 4 (109, 114, 117, 120) | no part | **FAIL** |
| `NBUS.T` | 4 (110, 116, 119, 122) | no part | **FAIL** |
| `TB24P.T` | 4 (160, 162, 164, 170) | 8 | PASS |
| `TBHL.T` | 4 (333, 334, 335, 336) | 4 | PASS |
| `A1.X5` | 3 (315, 317, 319) | 2 | **FAIL** |
| `DINTB.T` | 1 (147) | 4 | PASS |

## R2 Protective earth

- PASS `Mains.PE` bonded to the earth bar
- PASS `Z1.GND` bonded to the earth bar
- PASS `U1.PE` bonded to the earth bar
- PASS `G1.PE` bonded to the earth bar
- PASS `G2.PE` bonded to the earth bar
- PASS `X1.PE` bonded to the earth bar
- **FAIL** `XS1.PE` bonded to the earth bar
- PASS `BKP.T` bonded to the earth bar
- PASS `DOOR.T` bonded to the earth bar

## R3 Cross-section vs upstream protection (IEC 60204-1 Table 6, B2)

| Wire | Section | Ampacity | Protected by | Result |
|---|---|---|---|---|
| 103 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 105 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 107 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 109 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 111 | 1.5 mm² | 13.5 A | 25 A | **FAIL** |
| 114 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 117 | 1.5 mm² | 13.5 A | 25 A | **FAIL** |
| 120 | 1.5 mm² | 13.5 A | 25 A | **FAIL** |
| 130 | 1.5 mm² | 13.5 A | 25 A | **FAIL** |
| 131 | 1.5 mm² | 13.5 A | 25 A | **FAIL** |
| 150 | 1.5 mm² | 13.5 A | 15 A | WARN |
| 152 | 1.5 mm² | 13.5 A | 15 A | WARN |
| 154 | 1.5 mm² | 13.5 A | 15 A | WARN |
| 160 | 1.5 mm² | 13.5 A | 20 A | **FAIL** |
| 162 | 0.5 mm² | 3.0 A | 20 A | **FAIL** |
| 164 | 0.5 mm² | 3.0 A | 20 A | **FAIL** |
| 170 | 0.5 mm² | 3.0 A | 20 A | **FAIL** |

## R4 Every endpoint is a real part

- **FAIL** `LBUS.T` (L bus) has 4 conductors but no part in § 2 / § 6 (busbar? terminal?)
- **FAIL** `NBUS.T` (N bus) has 4 conductors but no part in § 2 / § 6 (busbar? terminal?)

