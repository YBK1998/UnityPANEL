# Wiring rule check — rev 0.7

109 numbered conductors.
**9 problem(s).**

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
| 103 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 105 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 111 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 109 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 114 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 117 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 120 | 2.5 mm² | 18.3 A | 25 A | **FAIL** |
| 150 | 1.5 mm² | 13.5 A | 15 A | WARN |
| 152 | 1.5 mm² | 13.5 A | 15 A | WARN |
| 154 | 1.5 mm² | 13.5 A | 15 A | WARN |
| 162 | 2.5 mm² | 18.3 A | 20 A | WARN |
| 164 | 2.5 mm² | 18.3 A | 20 A | WARN |

## R6 Neutral conductors (return current of every branch behind them)

| Wire | Section | Ampacity | Branches behind it | Load | Result |
|---|---|---|---|---|---|
| 104 | 2.5 mm² | 18.3 A | G1.N, G2.N, KM1.A1, U1.N, XS1.N | 25 A | **FAIL** |
| 106 | 2.5 mm² | 18.3 A | G1.N, G2.N, KM1.A1, U1.N, XS1.N | 25 A | **FAIL** |

## R4 Every endpoint is a real part


## R5 Electrical equivalence with rev 0.5

- CHANGED `A1.DCGND`: rev 0.5 net {A1.DCGND, A1.GND, A1.OGND, A2.ALM-, A3.ALM-, A4.ALM-, BKP.T, DOOR.T, G1.PE, G2.-V, G2.PE, H1.-, K1.A2, M2.-, Mains.PE, PEBAR.BAR, S0.3, U1.COM, U1.PEc, X-LIM.3, XT.2, XT.4, Y-LIM.3, Z-LIM.3, Z1.GND} → now {A1.DCGND, A1.GND, A1.OGND, A2.ALM-, A3.ALM-, A4.ALM-, BKP.T, BODY.T, DOOR.T, G1.PE, G2.-V, G2.PE, H1.-, K1.A2, M2.-, Mains.PE, PEBAR.BAR, PEBAR.BAR2, U1.COM, U1.PEc, X-LIM.3, XS1.PE, XT.2, XT.4, Y-LIM.3, Z-LIM.3, Z1.GND}
- CHANGED `F1.in`: rev 0.5 net {F1.in, F2.in, G2.+V, M2.+} → now {F1.in, F2.in, G2.+V}
- CHANGED `F1.out`: rev 0.5 net {F1.out, H2.+, H3.+, KM1.43, S0.1} → now {F1.out, H2.+, H3.+, KM1.43, M2.+, S0.1}
- CHANGED `G1.N`: rev 0.5 net {G1.N, KM1.4T2, U1.N, XS1.N} → now {G1.N, G2.N, KM1.A1, U1.N, XN.T, XS1.N, Z1.NOUT}
- CHANGED `G2.L`: rev 0.5 net {G2.L, QF4.2} → now {G2.L, K1.41, QF4.2}
- CHANGED `K1.21`: rev 0.5 net {K1.21, KM1.1L1, QF4.1, Z1.LOUT} → now {K1.21}
- CHANGED `K1.24`: rev 0.5 net {K1.24, KM1.A2} → now {K1.24}
- CHANGED `K1.44`: rev 0.5 net {K1.44} → now {K1.44, KM1.A2}
- CHANGED `KM1.1L1`: rev 0.5 net {K1.21, KM1.1L1, QF4.1, Z1.LOUT} → now {KM1.1L1, QF4.1, Z1.LOUT}
- CHANGED `KM1.2T1`: rev 0.5 net {KM1.2T1, QF1.1, QF2.1, QF3.1} → now {KM1.2T1, QF1.1, QF2.1, QF3.1, XL.T}
- CHANGED `KM1.3L2`: rev 0.5 net {G2.N, KM1.3L2, KM1.A1, Z1.NOUT} → now {KM1.3L2}
- CHANGED `KM1.4T2`: rev 0.5 net {G1.N, KM1.4T2, U1.N, XS1.N} → now {KM1.4T2}
- CHANGED `S0.3`: rev 0.5 net {A1.DCGND, A1.GND, A1.OGND, A2.ALM-, A3.ALM-, A4.ALM-, BKP.T, DOOR.T, G1.PE, G2.-V, G2.PE, H1.-, K1.A2, M2.-, Mains.PE, PEBAR.BAR, S0.3, U1.COM, U1.PEc, X-LIM.3, XT.2, XT.4, Y-LIM.3, Z-LIM.3, Z1.GND} → now {S0.3, XT.3}
- CHANGED `U1.PEb`: rev 0.5 net {U1.PEb, X1.4} → now {U1.PEb, X1.SH}
- CHANGED `X1.4`: rev 0.5 net {U1.PEb, X1.4} → now {X1.4}

