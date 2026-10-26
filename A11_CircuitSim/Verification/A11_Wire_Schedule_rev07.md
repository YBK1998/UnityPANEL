# Wire schedule rev 0.7 — wired directly; no terminal blocks, only the 230 V L / N busbars

Generated from the simulator netlist (`netlist_rev07.json`). Max. 2 conductors per device terminal; "2×" = twin ferrule. XL and XN (12-point busbars) take one conductor per point; the two 4-point earth bars take up to 2 per point (owner decision 2026-10-26) and are linked by 138. Changes against the rev 0.5 schedule (§ 8) are marked.

Colours (IEC 60204-1 13.2.4): L **black**, N **light blue**, PE **green/yellow**, AC control (KM1 coil) **red**, DC power (48 V, brake resistor) **black**, +24 V / DC control / signals **dark blue**, 0 V **blue/white**. The spindle cable W1 is one 4G1.5 shielded cable (cores brown / black / grey / G/Y).

| No. | From | To | Spec | Change vs rev 0.5 |
|---|---|---|---|---|
| 101 | Mains (gland) L | Main CB 1 | 2.5 mm² black | was 2.5 mm² brown |
| 102 | Mains (gland) N | Main CB 3 | 2.5 mm² light blue | was 2.5 mm² blue |
| 103 | Main CB 2 | EMI filter LIN | 2.5 mm² black | was 2.5 mm² brown |
| 104 | Main CB 4 | EMI filter NIN | 2.5 mm² light blue | was 2.5 mm² blue |
| 105 | EMI filter LOUT (2×) | Contactor 1L1 | 2.5 mm² black | was EMI filter LOUT → TB1; was 2.5 mm² brown |
| 106 | EMI filter NOUT | N busbar XN | 2.5 mm² light blue | was EMI filter NOUT → TB2; was 2.5 mm² blue |
| 109 | Contactor 2T1 | L busbar XL | 2.5 mm² black | was Contactor 2T1 → LBUS; was 2.5 mm² brown |
| 111 | EMI filter LOUT (2×) | CB-24V 1 | 2.5 mm² black | was TB1 → CB-24V 1; was 1.5 mm² brown |
| 112 | CB-24V 2 (2×) | PSU 24V L | 1.5 mm² black | was 1.5 mm² brown |
| 113 | N busbar XN | PSU 24V N | 1.5 mm² light blue | was TB2 → PSU 24V N; was 1.5 mm² blue |
| 114 | L busbar XL | CB-VFD 1 | 2.5 mm² black | was LBUS → CB-VFD 1; was 2.5 mm² brown |
| 115 | CB-VFD 2 | VFD L | 2.5 mm² black | was 2.5 mm² brown |
| 116 | N busbar XN | VFD N | 2.5 mm² light blue | was NBUS → VFD N; was 2.5 mm² blue |
| 117 | L busbar XL | CB-48V 1 | 2.5 mm² black | was LBUS → CB-48V 1; was 1.5 mm² brown |
| 118 | CB-48V 2 | PSU 48V L | 1.5 mm² black | was 1.5 mm² brown |
| 119 | N busbar XN | PSU 48V N | 1.5 mm² light blue | was NBUS → PSU 48V N; was 1.5 mm² blue |
| 120 | L busbar XL | CB-PUMP 1 | 2.5 mm² black | was LBUS → CB-PUMP 1; was 1.5 mm² brown |
| 121 | CB-PUMP 2 | Schuko L | 1.5 mm² black | was 1.5 mm² brown |
| 122 | N busbar XN | Schuko N | 1.5 mm² light blue | was NBUS → Schuko N; was 1.5 mm² blue |
| 123 | VFD U | HDC-HA 1 | 1.5 mm² brown, core of W1 4G1.5 shielded | was 1.5 mm² shielded |
| 124 | VFD V | HDC-HA 2 | 1.5 mm² black, core of W1 4G1.5 shielded | was 1.5 mm² shielded |
| 125 | VFD W | HDC-HA 3 | 1.5 mm² grey, core of W1 4G1.5 shielded | was 1.5 mm² shielded |
| 126 | VFD PE | HDC-HA PE | 1.5 mm² G/Y, core of W1 4G1.5 shielded | was G/Y |
| 127 | HDC-HA SH | VFD PEb | W1 braided shield, 360° clamp both ends | was HDC-HA 4 → VFD PEb; was — |
| 128 | VFD P+ | Braking resistor T1 | 1.5 mm² black, silicone | was 1.5 mm² |
| 129 | VFD PB | Braking resistor T2 | 1.5 mm² black, silicone | was 1.5 mm² |
| 130 | CB-24V 2 (2×) | K1 41 | 1.5 mm² red | was TB1 → K1 21; was 1.5 mm² brown |
| 131 | K1 44 | Contactor A2 | 1.5 mm² red | was K1 24 → Contactor A2; was 1.5 mm² brown |
| 132 | N busbar XN | Contactor A1 | 1.5 mm² light blue | was TB2 → Contactor A1; was 1.5 mm² blue |
| 138 | Earth bar 1 | Earth bar 2 | 4 mm² G/Y | **new** |
| 139 | Enclosure body (earth stud) | Earth bar 1 | 4 mm² G/Y | **new** |
| 140 | Mains (gland) PE | Earth bar 1 | 4 mm² G/Y |  |
| 141 | Backplate | Earth bar 1 | 4 mm² G/Y |  |
| 142 | Door strap | Earth bar 1 | 6 mm² braided strap | was 2.5 mm² G/Y |
| 143 | EMI filter GND | Earth bar 1 | 4 mm² G/Y |  |
| 144 | VFD PEc | Earth bar 1 | 2.5 mm² G/Y |  |
| 145 | PSU 48V PE | Earth bar 2 | 1.5 mm² G/Y | was PSU 48V PE → Earth bar 1 |
| 146 | PSU 24V PE | Earth bar 2 | 1.5 mm² G/Y | was PSU 24V PE → Earth bar 1 |
| 147 | DIN-rail earth stud | Earth bar 2 | 2.5 mm² G/Y | was DINTB → Earth bar 1; was G/Y |
| 148 | PSU 24V -V2 (2×) | Earth bar 2 | 1.5 mm² G/Y | was TB0V → Earth bar 1 |
| 149 | Schuko PE | Earth bar 2 | 1.5 mm² G/Y | **new** |
| 150 | PSU 48V +V1 | DMA860H-X AC1 | 1.5 mm² black | was 1.5 mm² red |
| 151 | PSU 48V -V1 | DMA860H-X AC2 | 1.5 mm² black |  |
| 152 | PSU 48V +V2 | DMA860H-Y AC1 | 1.5 mm² black | was 1.5 mm² red |
| 153 | PSU 48V -V2 | DMA860H-Y AC2 | 1.5 mm² black |  |
| 154 | PSU 48V +V3 | DMA860H-Z AC1 | 1.5 mm² black | was 1.5 mm² red |
| 155 | PSU 48V -V3 | DMA860H-Z AC2 | 1.5 mm² black |  |
| 162 | PSU 24V +V1 | F1 in | 2.5 mm² dark blue | was TB24P → F1 in; was 0.5 mm² red |
| 164 | PSU 24V +V2 | F2 in | 2.5 mm² dark blue | was TB24P → F2 in; was 0.5 mm² red |
| 166 | F2 out (2×) | A11 DC24V | 0.5 mm² dark blue | was TB24S → A11 DC24V; was 0.5 mm² red |
| 167 | PSU 24V -V1 (2×) | A11 DCGND (2×) | 0.5 mm² blue/white | was TB0V → A11 DCGND; was 0.5 mm² black |
| 168 | F2 out (2×) | A11 24V (2×) | 0.5 mm² dark blue | was TB24S → A11 24V; was 0.5 mm² red |
| 169 | PSU 24V -V2 (2×) | A11 GND (2×) | 0.5 mm² blue/white | was TB0V → A11 GND; was 0.5 mm² black |
| 170 | Contactor 43 (2×) | Fan + | 0.5 mm² dark blue | was TB24P → Fan +; was 0.5 mm² red |
| 171 | A11 DCGND (2×) | Fan - | 0.5 mm² blue/white | was TB0V → Fan -; was 0.5 mm² black |
| 201 | F1 out (2×) | E-stop 1 (2×) | 0.5 mm² dark blue | was TB24C → E-stop 1; was 0.5 mm² |
| 202 | E-stop 2 | OFF 1 | 0.5 mm² dark blue | was 0.5 mm² |
| 203 | OFF 2 (2×) | ON 1 | 0.5 mm² dark blue | was 0.5 mm² |
| 204 | ON 2 | K1 A1 (2×) | 0.5 mm² dark blue | was 0.5 mm² |
| 205 | OFF 2 (2×) | K1 11 | 0.5 mm² dark blue | was 0.5 mm² |
| 206 | K1 14 | K1 A1 (2×) | 0.5 mm² dark blue | was 0.5 mm² |
| 207 | PSU 24V -V1 (2×) | K1 A2 | 0.5 mm² blue/white | was K1 A2 → TB0V; was 0.5 mm² |
| 210 | E-stop 4 | A11 X7 | 0.5 mm² dark blue | was 0.5 mm² |
| 220 | F1 out (2×) | Contactor 43 (2×) | 0.5 mm² dark blue | was TB24C → Contactor 43; was 0.5 mm² |
| 221 | Contactor 44 | Yellow lamp + | 0.5 mm² dark blue | was 0.5 mm² |
| 222 | K1 A2 (2nd screw) | Yellow lamp - | 0.5 mm² blue/white | was Yellow lamp - → TB0V; was 0.5 mm² |
| 223 | E-stop 1 (2×) | Green lamp + (2×) | 0.5 mm² dark blue | was TB24C → Green lamp +; was 0.5 mm² |
| 224 | Green lamp - | A11 Y6 | 0.5 mm² dark blue | was 0.5 mm² |
| 225 | Green lamp + (2×) | Red lamp + | 0.5 mm² dark blue | was TB24C → Red lamp +; was 0.5 mm² |
| 226 | Red lamp - | A11 Y5 | 0.5 mm² dark blue | was 0.5 mm² |
| 301 | A11 X_5V | DMA860H-X PUL+ (2×) | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 302 | A11 X_PUL | DMA860H-X PUL- | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 303 | A11 X_DIR | DMA860H-X DIR- | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 304 | DMA860H-X PUL+ (2×) | DMA860H-X DIR+ | 0.5 mm² dark blue | was 0.5 mm² |
| 305 | A11 Y_5V | DMA860H-Y PUL+ (2×) | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 306 | A11 Y_PUL | DMA860H-Y PUL- | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 307 | A11 Y_DIR | DMA860H-Y DIR- | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 308 | DMA860H-Y PUL+ (2×) | DMA860H-Y DIR+ | 0.5 mm² dark blue | was 0.5 mm² |
| 309 | A11 Z_5V | DMA860H-Z PUL+ (2×) | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 310 | A11 Z_PUL | DMA860H-Z PUL- | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 311 | A11 Z_DIR | DMA860H-Z DIR- | 0.5 mm² dark blue, twisted | was 0.5 mm² twisted |
| 312 | DMA860H-Z PUL+ (2×) | DMA860H-Z DIR+ | 0.5 mm² dark blue | was 0.5 mm² |
| 315 | DMA860H-X ALM+ (2×) | A11 X5 | 0.5 mm² dark blue | was 0.5 mm² |
| 316 | A11 OGND (2×) | DMA860H-X ALM- (2×) | 0.5 mm² blue/white | was DMA860H-X ALM- → TB0V; was 0.5 mm² |
| 317 | DMA860H-Y ALM+ (2×) | DMA860H-X ALM+ (2×) | 0.5 mm² dark blue | was DMA860H-Y ALM+ → A11 X5; was 0.5 mm² |
| 318 | DMA860H-X ALM- (2×) | DMA860H-Y ALM- (2×) | 0.5 mm² blue/white | was DMA860H-Y ALM- → TB0V; was 0.5 mm² |
| 319 | DMA860H-Z ALM+ | DMA860H-Y ALM+ (2×) | 0.5 mm² dark blue | was DMA860H-Z ALM+ → A11 X5; was 0.5 mm² |
| 320 | DMA860H-Y ALM- (2×) | DMA860H-Z ALM- | 0.5 mm² blue/white | was DMA860H-Z ALM- → TB0V; was 0.5 mm² |
| 325 | A11 Y1 | VFD X1 | 0.5 mm² dark blue | was 0.5 mm² |
| 326 | A11 OGND (2×) | VFD COM | 0.5 mm² blue/white | was 0.5 mm² |
| 327 | A11 Y2 | VFD X2 | 0.5 mm² dark blue | was 0.5 mm² |
| 328 | A11 Y3 | VFD X3 | 0.5 mm² dark blue | was 0.5 mm² |
| 329 | A11 Y4 | VFD X4 | 0.5 mm² dark blue | was 0.5 mm² |
| 330 | X-LIM 1 | A11 X1 | 0.5 mm² dark blue | was 0.5 mm² |
| 331 | Y-LIM 1 | A11 X2 | 0.5 mm² dark blue | was 0.5 mm² |
| 332 | Z-LIM 2 | A11 X3 | 0.5 mm² dark blue | was 0.5 mm² |
| 334 | X-LIM 2 (2×) | Y-LIM 2 (2×) | 0.5 mm² dark blue | was Y-LIM 2 → TBHL; was 0.5 mm² |
| 335 | Y-LIM 2 (2×) | Z-LIM 1 | 0.5 mm² dark blue | was Z-LIM 1 → TBHL; was 0.5 mm² |
| 336 | X-LIM 2 (2×) | A11 X6 | 0.5 mm² dark blue | was TBHL → A11 X6; was 0.5 mm² |
| 337 | A11 GND (2×) | X-LIM 3 (2×) | 0.5 mm² blue/white | was TB0V → X-LIM 3; was 0.5 mm² |
| 338 | X-LIM 3 (2×) | Y-LIM 3 (2×) | 0.5 mm² blue/white | was TB0V → Y-LIM 3; was 0.5 mm² |
| 339 | Y-LIM 3 (2×) | Z-LIM 3 (2×) | 0.5 mm² blue/white | was TB0V → Z-LIM 3; was 0.5 mm² |
| 340 | A11 24V (2×) | X-LIM 4 (2×) | 0.5 mm² dark blue | was TB24S → X-LIM 4; was 0.5 mm² |
| 341 | X-LIM 4 (2×) | Y-LIM 4 (2×) | 0.5 mm² dark blue | was TB24S → Y-LIM 4; was 0.5 mm² |
| 342 | Y-LIM 4 (2×) | Z-LIM 4 | 0.5 mm² dark blue | was TB24S → Z-LIM 4; was 0.5 mm² |
| 345 | Tool setter 1 | A11 X4 | 0.5 mm² dark blue | was 0.5 mm² |
| 346 | Z-LIM 3 (2×) | Tool setter 2 (2×) | 0.5 mm² blue/white | was Tool setter 2 → TB0V; was 0.5 mm² |
| 347 | Tool setter 3 | E-stop 3 | 0.5 mm² dark blue | was Tool setter 3 → A11 X8; was 0.5 mm² — not fitted |
| 348 | Tool setter 2 (2×) | Tool setter 4 | 0.5 mm² blue/white | was Tool setter 4 → TB0V; was 0.5 mm² |

**Removed: 107, 108, 110, 160, 161, 163, 165, 211, 333.** 107 / 108 / 160 / 161 / 163 / 165 / 333 only connected to terminal blocks (TB1, TB2, TB24+, TB24C, TB24S, TB0V, TB-HL). 110 (KM1 4T2 → N bus) is gone because KM1 switches L only and every N conductor sits on XN. 211 (0 V to E-stop NC2) is gone: NC2 (added ZB2-BE102 block) now gets its 0 V through the tool-setter over-travel (347), so X7 opens on the E-stop or on over-travel, not on OFF.

