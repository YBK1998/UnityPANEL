# § 8 wire schedule vs simulator netlist

**115 of 115 rows match, 0 mismatches.**

PDF rows read: 115 · simulator numbered wires: 115

| No. | PDF From → To | Simulator From → To | Result |
|---|---|---|---|
| 101 | Mains L (gland) → Main CB L in | `Mains.L` → `QF0.1` | PASS |
| 102 | Mains N (gland) → Main CB N in | `Mains.N` → `QF0.3` | PASS |
| 103 | Main CB L out → EMI L-IN | `QF0.2` → `Z1.LIN` | PASS |
| 104 | Main CB N out → EMI N-IN | `QF0.4` → `Z1.NIN` | PASS |
| 105 | EMI L-OUT → TB1 (L-UNSW) | `Z1.LOUT` → `TB1.T` | PASS |
| 106 | EMI N-OUT → TB2 (N-UNSW) | `Z1.NOUT` → `TB2.T` | PASS |
| 107 | TB1 → Contactor 1L1 | `TB1.T` → `KM1.1L1` | PASS |
| 108 | TB2 → Contactor 3L2 | `TB2.T` → `KM1.3L2` | PASS |
| 109 | Contactor 2T1 → L bus | `KM1.2T1` → `LBUS.T` | PASS |
| 110 | Contactor 4T2 → N bus | `KM1.4T2` → `NBUS.T` | PASS |
| 111 | TB1 → CB-24V (C6A) in | `TB1.T` → `QF4.1` | PASS |
| 112 | CB-24V out → PSU 24V L | `QF4.2` → `G2.L` | PASS |
| 113 | TB2 → PSU 24V N | `TB2.T` → `G2.N` | PASS |
| 114 | L bus → CB-VFD (C16A) in | `LBUS.T` → `QF1.1` | PASS |
| 115 | CB-VFD out → VFD L | `QF1.2` → `U1.L` | PASS |
| 116 | N bus → VFD N | `NBUS.T` → `U1.N` | PASS |
| 117 | L bus → CB-48V (C10A) in | `LBUS.T` → `QF2.1` | PASS |
| 118 | CB-48V out → PSU 48V L | `QF2.2` → `G1.L` | PASS |
| 119 | N bus → PSU 48V N | `NBUS.T` → `G1.N` | PASS |
| 120 | L bus → CB-PUMP (C10A) in | `LBUS.T` → `QF3.1` | PASS |
| 121 | CB-PUMP out → Schuko L | `QF3.2` → `XS1.L` | PASS |
| 122 | N bus → Schuko N | `NBUS.T` → `XS1.N` | PASS |
| 123 | VFD U → HDC-HA pin 1 | `U1.U` → `X1.1` | PASS |
| 124 | VFD V → HDC-HA pin 2 | `U1.V` → `X1.2` | PASS |
| 125 | VFD W → HDC-HA pin 3 | `U1.W` → `X1.3` | PASS |
| 126 | VFD PE → HDC-HA PE | `U1.PE` → `X1.PE` | PASS |
| 127 | HDC-HA pin 4 → VFD PE | `X1.4` → `U1.PEb` | PASS |
| 128 | VFD P+ → Braking resistor T1 | `U1.P+` → `R1.T1` | PASS |
| 129 | VFD PB → Braking resistor T2 | `U1.PB` → `R1.T2` | PASS |
| 130 | TB1 → K1 pole-2 COM | `TB1.T` → `K1.21` | PASS |
| 131 | K1 pole-2 NO → Contactor A2 | `K1.24` → `KM1.A2` | PASS |
| 132 | TB2 → Contactor A1 | `TB2.T` → `KM1.A1` | PASS |
| 140 | Mains PE (gland) → Earth bar | `Mains.PE` → `PEBAR.BAR` | PASS |
| 141 | Backplate bond → Earth bar | `BKP.T` → `PEBAR.BAR` | PASS |
| 142 | Door bond strap → Earth bar | `DOOR.T` → `PEBAR.BAR` | PASS |
| 143 | EMI filter ground lug → Earth bar | `Z1.GND` → `PEBAR.BAR` | PASS |
| 144 | VFD PE → Earth bar | `U1.PEc` → `PEBAR.BAR` | PASS |
| 145 | PSU 48V PE → Earth bar | `G1.PE` → `PEBAR.BAR` | PASS |
| 146 | PSU 24V PE → Earth bar | `G2.PE` → `PEBAR.BAR` | PASS |
| 147 | DIN-rail earth TB → Earth bar | `DINTB.T` → `PEBAR.BAR` | PASS |
| 148 | TB0V → Earth bar | `TB0V.T` → `PEBAR.BAR` | PASS |
| 150 | PSU 48V +V (screw 1) → DMA860H-X AC 1 | `G1.+V1` → `A2.AC1` | PASS |
| 151 | PSU 48V -V (screw 1) → DMA860H-X AC 2 | `G1.-V1` → `A2.AC2` | PASS |
| 152 | PSU 48V +V (screw 2) → DMA860H-Y AC 1 | `G1.+V2` → `A3.AC1` | PASS |
| 153 | PSU 48V -V (screw 2) → DMA860H-Y AC 2 | `G1.-V2` → `A3.AC2` | PASS |
| 154 | PSU 48V +V (screw 3) → DMA860H-Z AC 1 | `G1.+V3` → `A4.AC1` | PASS |
| 155 | PSU 48V -V (screw 3) → DMA860H-Z AC 2 | `G1.-V3` → `A4.AC2` | PASS |
| 160 | PSU 24V +V → TB24+ | `G2.+V` → `TB24P.T` | PASS |
| 161 | PSU 24V -V → TB0V | `G2.-V` → `TB0V.T` | PASS |
| 162 | TB24+ → F1 in | `TB24P.T` → `F1.in` | PASS |
| 163 | F1 out → TB24C | `F1.out` → `TB24C.T` | PASS |
| 164 | TB24+ → F2 in | `TB24P.T` → `F2.in` | PASS |
| 165 | F2 out → TB24S | `F2.out` → `TB24S.T` | PASS |
| 166 | TB24S → A11 board power + | `TB24S.T` → `A1.DC24V` | PASS |
| 167 | TB0V → A11 board power - | `TB0V.T` → `A1.DCGND` | PASS |
| 168 | TB24S → A11 input 24V+ | `TB24S.T` → `A1.24V` | PASS |
| 169 | TB0V → A11 input GND | `TB0V.T` → `A1.GND` | PASS |
| 170 | TB24+ → Fan + | `TB24P.T` → `M2.+` | PASS |
| 171 | TB0V → Fan - | `TB0V.T` → `M2.-` | PASS |
| 201 | TB24C → E-stop NC1 in | `TB24C.T` → `S0.1` | PASS |
| 202 | E-stop NC1 out → OFF (NC) in | `S0.2` → `S1.1` | PASS |
| 203 | OFF out (Node X) → ON (NO) in | `S1.2` → `S2.1` | PASS |
| 204 | ON out → K1 coil A1 | `S2.2` → `K1.A1` | PASS |
| 205 | Node X (OFF out) → K1 pole-1 COM | `S1.2` → `K1.11` | PASS |
| 206 | K1 pole-1 NO → K1 coil A1 | `K1.14` → `K1.A1` | PASS |
| 207 | K1 coil A2 → TB0V | `K1.A2` → `TB0V.T` | PASS |
| 210 | E-stop NC2 → A11 X7 | `S0.4` → `A1.X7` | PASS |
| 211 | E-stop NC2 (other screw) → TB0V | `S0.3` → `TB0V.T` | PASS |
| 220 | TB24C → Contactor aux 43 | `TB24C.T` → `KM1.43` | PASS |
| 221 | Contactor aux 44 → Yellow lamp + | `KM1.44` → `H1.+` | PASS |
| 222 | Yellow lamp - → TB0V | `H1.-` → `TB0V.T` | PASS |
| 223 | TB24C → Green lamp + | `TB24C.T` → `H2.+` | PASS |
| 224 | Green lamp - → A11 Y6 | `H2.-` → `A1.Y6` | PASS |
| 225 | TB24C → Red lamp + | `TB24C.T` → `H3.+` | PASS |
| 226 | Red lamp - → A11 Y5 | `H3.-` → `A1.Y5` | PASS |
| 301 | A11 X-axis 5V → DMA860H-X PUL+ | `A1.X_5V` → `A2.PUL+` | PASS |
| 302 | A11 X-axis PULSE → DMA860H-X PUL- | `A1.X_PUL` → `A2.PUL-` | PASS |
| 303 | A11 X-axis DIR → DMA860H-X DIR- | `A1.X_DIR` → `A2.DIR-` | PASS |
| 304 | DMA860H-X PUL+ → DMA860H-X DIR+ | `A2.PUL+` → `A2.DIR+` | PASS |
| 305 | A11 Y-axis 5V → DMA860H-Y PUL+ | `A1.Y_5V` → `A3.PUL+` | PASS |
| 306 | A11 Y-axis PULSE → DMA860H-Y PUL- | `A1.Y_PUL` → `A3.PUL-` | PASS |
| 307 | A11 Y-axis DIR → DMA860H-Y DIR- | `A1.Y_DIR` → `A3.DIR-` | PASS |
| 308 | DMA860H-Y PUL+ → DMA860H-Y DIR+ | `A3.PUL+` → `A3.DIR+` | PASS |
| 309 | A11 Z-axis 5V → DMA860H-Z PUL+ | `A1.Z_5V` → `A4.PUL+` | PASS |
| 310 | A11 Z-axis PULSE → DMA860H-Z PUL- | `A1.Z_PUL` → `A4.PUL-` | PASS |
| 311 | A11 Z-axis DIR → DMA860H-Z DIR- | `A1.Z_DIR` → `A4.DIR-` | PASS |
| 312 | DMA860H-Z PUL+ → DMA860H-Z DIR+ | `A4.PUL+` → `A4.DIR+` | PASS |
| 315 | DMA860H-X ALM+ → A11 X5 | `A2.ALM+` → `A1.X5` | PASS |
| 316 | DMA860H-X ALM- → TB0V | `A2.ALM-` → `TB0V.T` | PASS |
| 317 | DMA860H-Y ALM+ → A11 X5 | `A3.ALM+` → `A1.X5` | PASS |
| 318 | DMA860H-Y ALM- → TB0V | `A3.ALM-` → `TB0V.T` | PASS |
| 319 | DMA860H-Z ALM+ → A11 X5 | `A4.ALM+` → `A1.X5` | PASS |
| 320 | DMA860H-Z ALM- → TB0V | `A4.ALM-` → `TB0V.T` | PASS |
| 325 | A11 Y1 → VFD X1 | `A1.Y1` → `U1.X1` | PASS |
| 326 | A11 output GND → VFD COM (digital) | `A1.OGND` → `U1.COM` | PASS |
| 327 | A11 Y2 (S1) → VFD X2 | `A1.Y2` → `U1.X2` | PASS |
| 328 | A11 Y3 (S2) → VFD X3 | `A1.Y3` → `U1.X3` | PASS |
| 329 | A11 Y4 (S3) → VFD X4 | `A1.Y4` → `U1.X4` | PASS |
| 330 | X-LIM pin 1 → A11 X1 | `X-LIM.1` → `A1.X1` | PASS |
| 331 | Y-LIM pin 1 → A11 X2 | `Y-LIM.1` → `A1.X2` | PASS |
| 332 | Z-LIM pin 2 → A11 X3 | `Z-LIM.2` → `A1.X3` | PASS |
| 333 | X-LIM pin 2 → TB-HL | `X-LIM.2` → `TBHL.T` | PASS |
| 334 | Y-LIM pin 2 → TB-HL | `Y-LIM.2` → `TBHL.T` | PASS |
| 335 | Z-LIM pin 1 → TB-HL | `Z-LIM.1` → `TBHL.T` | PASS |
| 336 | TB-HL → A11 X6 | `TBHL.T` → `A1.X6` | PASS |
| 337 | TB0V → X-LIM pin 3 | `TB0V.T` → `X-LIM.3` | PASS |
| 338 | TB0V → Y-LIM pin 3 | `TB0V.T` → `Y-LIM.3` | PASS |
| 339 | TB0V → Z-LIM pin 3 | `TB0V.T` → `Z-LIM.3` | PASS |
| 340 | TB24S → X-LIM pin 4 | `TB24S.T` → `X-LIM.4` | PASS |
| 341 | TB24S → Y-LIM pin 4 | `TB24S.T` → `Y-LIM.4` | PASS |
| 342 | TB24S → Z-LIM pin 4 | `TB24S.T` → `Z-LIM.4` | PASS |
| 345 | TS pin 1 (red) → A11 X4 | `XT.1` → `A1.X4` | PASS |
| 346 | TS pin 2 (black) → TB0V | `XT.2` → `TB0V.T` | PASS |
| 347 | TS pin 3 (blue) → A11 X8 | `XT.3` → `A1.X8` | PASS |
| 348 | TS pin 4 (yellow) → TB0V | `XT.4` → `TB0V.T` | PASS |
