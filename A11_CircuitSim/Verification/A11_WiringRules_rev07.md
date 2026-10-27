# Wiring rule check — rev 0.7

109 numbered conductors.
**3 problem(s).**

## R1 Conductors per terminal

| Terminal | Conductors | Capacity | Result |
|---|---|---|---|
| `PEBAR.BAR` | 7 (140, 139, 141, 142, 143, 144, 138) | 8 | PASS |
| `XN.T` | 6 (106, 113, 132, 116, 119, 122) | 12 | PASS |
| `PEBAR.BAR2` | 6 (147, 145, 146, 149, 148, 138) | 8 | PASS |
| `XL.T` | 4 (109, 114, 117, 120) | 12 | PASS |

## R2 Protective earth

- PASS `Mains.PE` bonded to the earth bar
- PASS `Z1.GND` bonded to the earth bar
- PASS `U1.PE` bonded to the earth bar
- PASS `G1.PE` bonded to the earth bar
- PASS `G2.PE` bonded to the earth bar
- PASS `X1.PE` bonded to the earth bar
- PASS `XS1.PE` bonded to the earth bar
- PASS `BKP.T` bonded to the earth bar
- PASS `DOOR.T` bonded to the earth bar
- PASS `BODY.T` bonded to the earth bar

## R3 Cross-section vs upstream protection (IEC 60204-1 Table 6, B2)

| Wire | Section | Ampacity | Protected by | Result |
|---|---|---|---|---|
| 150 | 1.0 mm² | 10.4 A | 15 A | **FAIL** |
| 152 | 1.0 mm² | 10.4 A | 15 A | **FAIL** |
| 154 | 1.0 mm² | 10.4 A | 15 A | **FAIL** |
| 162 | 2.5 mm² | 18.3 A | 20 A | WARN |
| 164 | 2.5 mm² | 18.3 A | 20 A | WARN |

## R6 Neutral conductors (return current of every branch behind them)

| Wire | Section | Ampacity | Branches behind it | Load | Result |
|---|---|---|---|---|---|
| 104 | 4.0 mm² | 25.0 A | G1.N, G2.N, KM1.A1, U1.N, XS1.N | 25 A | PASS |
| 106 | 4.0 mm² | 25.0 A | G1.N, G2.N, KM1.A1, U1.N, XS1.N | 25 A | PASS |

## R4 Every endpoint is a real part


