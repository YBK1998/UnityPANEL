# A11 Circuit Sim (Unity 6000.6.3f1)

A live, zoomable version of `United_Box_A11_Manual_rev0.5.pdf`. Sheets E1–E4 are merged into one
schematic. Energized conductors glow in their potential's color.

The model is **wiring rev 0.7**: the rev 0.5 circuit wired directly, with no terminal blocks except two 230 V
busbars, XL (switched L) and XN (all N). Wire numbers follow § 8, except 9 removed and 3 added: 149 (pump
outlet PE), 139 (enclosure body to earth bar) and 138 (link between the two earth bars). See `Verification/A11_Wire_Schedule_rev07.md`.

Rev 0.7 changes (from a wire-by-wire review against the part datasheets, `Verification/A11_Circuit_Verification.md`):
- The E-stop gets an **added NC contact block (ZB2-BE102)**: NC1 opens the K1 chain, NC2 → A11 X7 in series with the
  **tool-setter over-travel** (wire 347). OFF does not raise the A11 alarm. K1 pole 4 switches the KM1 coil, pole 3 stays
  empty as the gap between 24 V and 230 V, pole 2 is spare.
- The KM1 coil circuit is fed from **QF4 out (C6)**.
- **XL / XN** busbars (160 × 16 × 42, 12 points each, vertical in the strip right of the right vertical duct): every N
  conductor has its own point; KM1 switches L only.
- **Hardware measured 2026-10-26:** EMI filter 51 × 51 × 30 screwed to the plate at the far left of row A (LINE side down,
  LOAD side up); DIN rail shortened to 330 mm; two 4-point copper earth bars (150 × 25 on 17 mm insulators) linked by 138,
  with **up to 2 conductors per point** — owner decision, a deviation from IEC 60204-1 (one protective conductor per point).
- Sensor 0 V comes straight from the A11 J8 GND screw (not through the drive ALM screws). The limit / home sensors stay
  NPN NO, so do the hard-limit check after any connector work.
- **Power lane VP** (left: mains in, shielded VFD cable W1 with the shield 360° at both ends) and **motor lane VM**
  (right: stepper motor cables), on the spare plate width.
- Wire colours to IEC 60204-1: L black, N blue, PE green/yellow, KM1 coil red, 48 V and brake resistor black,
  +24 V / signals dark blue, 0 V blue/white. Owner's stock (2026-10-27): every former 0.5 / 1.5 mm² wire is **1 mm² yellow**
  with that colour as a band at both ends; 2.5 / 4 mm² in black, blue, brown (+24 V 162 / 164, dark-blue band) and G/Y;
  PE always G/Y. Per-wire list with marking and cut lengths: `A11_Wire_List.pdf` (`Verification/make_wire_list.py`).
- Every conductor behind the C25 main (103–106, 109, 111, 114, 117, 120) is 4 mm².
- **Reminder C14 (open):** the FC300 nameplate's 11 A is the rated **output** current. The drive draws ≈ 22–24 A input at
  full spindle load, so QF1 C16 trips on long full-power cuts, the 20 A EMI filter is overloaded and QF0 C25 is marginal.
  Light cuts are fine. Z1 is fixed at 20 A (no larger filter available), so the way out is to cap the spindle at
  ≈ 1.5 kW (≈ 15 A input) in the VFD; options in `Verification/A11_Enclosure_Selection.md`.

## Run

- **Editor:** open this folder in Unity Hub (6000.6.3f1), open `Assets/Scenes/A11Circuit.unity`, press Play.
  Any scene also works because `Bootstrap` creates the simulation.
- **Player:** `Build/A11CircuitSim.exe` (menu *A11 Sim → 3 · Build Windows player* rebuilds it).

**Interface layout:**
- **App bar:** title, live status pills (mains, 24 V, K1, KM1 / bus, VFD frequency, drives, E-stop / alarm), the tools
  (Multimeter `T`, Procedures `P`, Build guide `B`), Help (`F1`) and the window controls.
- **Left sidebar:** the clickable operator-panel view, the emergency stop, breakers and fuses as switches, fault and
  sensor injection, and display options.
- **Right sidebar:** the A11 handheld (LCD, spindle, jog pad, jobs) and the machine-position plot.
- **Footer:** the wire-colour legend and the renderer and frame rate.

The look is a neutral slate theme with one blue accent. Green, amber and red appear only to show state: on, forced,
and fault / E-stop. The keyboard and mouse card (`F1`) also appears once on first start.

The player starts **borderless full screen** at the monitor's native resolution. `F11` switches to a
window, and `Ctrl+Q` quits. It renders with Direct3D 12 on the discrete GPU (RTX 3050 on this laptop)
with 8× MSAA. The right panel shows the GPU, API and fps.

## 3D door

The left panel shows a live 3D model of the enclosure door (§ 6). `V` switches to a full-screen 3D view.
The model includes the E-stop XB2-BS542, the twin-head ON/OFF, the three 24 V LED pilot lamps (lit from
the circuit), the A11 handheld with a live LCD and 16 working keys, the holder, the cable and the gland.
Everything is clickable. In the 3D view: drag to orbit, middle-drag to pan, scroll to zoom, `F` for the
door, `G` for the handheld. The handheld key layout is approximate (A11 style); the RichAuto documents
on hand do not give it.

## Cabinet interior

Press `O` (or click the door lock, or *Open door* in the left panel) to open the door. `I` frames the interior.

Inside is the full panel at datasheet size, in the bought enclosure:
- **800 × 1000 × 250 mm steel wall cabinet**, swing door hinged on the left (seen from the front), quarter-turn lock on
  the right, foamed door gasket;
- no removable panels: the 10 cable entries are drilled / punched through the bottom wall (mains and HDC-HA under the
  power lane, motor connectors under the motor lane, sensor connectors near the A11);
- its own galvanised mounting plate on welded M6 studs. The plate is **730 W × 930 H (measured)**,
  `Cabinet3D.PlateW` / `PlateH`. The layout uses the centre 539 × 930 of it, so there is spare room left and right.

The panel has TS35 rails and slotted 40 × 60 ducts, plus the power lane VP and the motor lane VM on the spare width.
Row A holds the plate-mounted EMI filter (far left), then on the 330 mm DIN rail the MCBs, UMC65 contactor, K1 (Finder 55.34 on a socket that looks like a Schneider RXZE2S114M, exact part not confirmed; wire by the IEC numbers, see caution C11),
the F1 / F2 RT18 holders with 10 × 38 gG 2 A fuses and the Schuko DIN socket. The L / N busbars XL, XN stand vertically right of the right vertical duct. Row B has the VFD with a live display (100 mm clear above it) and two PSUs, row C the three DMA860H drives (side-mounted, terminals facing the door: signals top, power bottom), and
row D the A11E board. Every part is drawn at the owner's measured size. The tube braking resistor (Ø 60 × 300), the 80 mm fan and the outlet (both behind flanged 80 mm filter adapters) sit on the side walls, and the two earth bars
and the cable entries on the bottom wall. There are no terminal blocks (only XL / XN for 230 V). Each other wire goes
from device screw to device screw, with at most 2 per screw (twin ferrule). The size choice and its sources are in `Verification/A11_Enclosure_Selection.md`.

Every wire is drawn terminal → duct → duct → terminal, with its cross-section and colour and a number sleeve at each
end. A collision test checks that no conductor passes through a part. The door wires cross the hinge in a spiral-wrapped service loop that follows the
door, and the door has an earth strap. Energized wires glow in the schematic's colours (*Live wires*).

Clicking works inside too: breaker levers switch, fuse carriers open, K1's test lever (hold), and drive
faults. Hover any part or wire for its data and size. `C` fits the duct covers and `M` shows the
enclosure dimensions.

## Multimeter, procedures and build guide

- **Multimeter (`T`).** Click any terminal in the schematic, or any wiring point in the open cabinet, to place the
  red probe, then the black one. Modes: V DC, V AC, Ω with a continuity beep. Readings come from the live circuit:
  nominal supply values; a coil, lamp or fan passes voltage to an open net; the A11 and VFD inputs have their
  internal pull-ups. Continuity follows conductors and closed contacts, so loads read OL. An Ω test on a live
  circuit shows LIVE.
- **Procedures (`P`).** § 7.1 tests with no power (18 steps) and § 7.2 power-up in steps (19 steps, including cautions
  C2, C3 and C7) as click-through steps. Each step says which probes and mode to use, highlights the target terminals,
  and passes when the reading matches the manual. *Show me* places the probes for you.
- **Build guide (`B`).** All 120 cabinet conductors in fitting order: earth, 230 V and VFD, 48 V, 24 V control,
  signals, motor leads. Each step gives the wire number, size and colour, the cut length (routed length + 10 % + 15 cm),
  and how each end is terminated (ferrule, twin ferrule and which wire shares it, fork lug, ring lug or solder cup).
  Wires not fitted yet are hidden and the current one pulses in the 3D cabinet. `Enter` = fitted, next. Progress is saved.

## Verification

See `Verification/A11_Circuit_Verification.md`:
- The rev 0.5 netlist matches all 115 rows of § 8 in the PDF (`verify_netlist.py`, read with pdfplumber).
- `wiring_rules.py` checks conductors per terminal, PE coverage, cross-section vs protection (IEC 60204-1 Table 6),
  neutral conductors and real endpoints. It found 21 problems in rev 0.5. Rev 0.7 leaves 9, all the 2.5 mm² supply path
  behind the C25 main (reminder C14, resolved by the 4 mm² supply path). It also lists every net that differs from rev 0.5 (all intended).
- 62 / 62 self-tests pass on rev 0.7: the § 7.1 dead tests, § 7.2 power-up, functions, short-circuit scans, drawing
  geometry and wiring rules (*A11 Sim → 4 · Run circuit self-test*).
- The document also lists the design findings (F2 sizing, category-0 spindle stop, VFD supply rating, …).

## Controls

| Input | Action |
|---|---|
| Mouse wheel, `+` / `−` | Zoom (toward the cursor) |
| Left-drag on empty space, right/middle-drag, WASD / arrows | Pan |
| `F` / Home | Fit the full circuit |
| `Tab` | Hide / show the panels |
| Click a breaker, fuse, E-stop, sensor, tool setter, drive | Operate it (drive = inject fault) |
| Hold S1 / S2 on the schematic, or hold `0` / `1` | OFF / ON push-buttons |
| `E` / `Q` | E-stop / main CB QF0 |
| `Space` / `H` / `Esc` | Spindle on/off, home all, stop |
| Hover | Wire § 8 data + live state, device info, caution C1–C13 text |
| `T` / `P` / `B` | Multimeter / procedures / build guide |

## What is simulated

`Circuit.Solve()` runs union-find over wires, fixed links and closed contacts, then applies the
sources (mains, G1, G2, VFD, A11 5 V). It repeats until coil states settle, so these behave as in
§ 4 and § 5:

- K1 self-latch (pole 1), K1 pole 2 → KM1 coil, no auto-restart after a mains failure.
- 24 V side on the unswitched supply: the A11 keeps power and sees the E-stop (X7, NC).
- A11 sink outputs Y1–Y4 → VFD X1–X4 multi-speed (P030–P037), Y5 alarm lamp, Y6 work lamp.
- NPN sensors pull X1–X4 / X6 to 0 V; far limits wire-OR on X6; drive ALM outputs in parallel on X5.
- Open-loop axes: if a drive has no 48 V, the A11 DRO moves but the machine does not
  ("lost steps").

Assumptions where the manual leaves items open (§ 9.1): ALM in parallel with X5 = NO (test 3),
S-grade bit order S1 = 000 … S8 = 111 (test 4). X8 is not connected (the over-travel acts on X7 through the E-stop NC2 loop).

## Colors

L orange · N blue · VFD U/V/W purple · DC bus / brake amber–red · +48 V yellow · +24 V red ·
0 V return dim blue · active signal green · +5 V lime · stepper phases pink · PE olive (never glows).

## Code map

- `A11Layout.cs`: every device position and every § 8 wire (one place to correct the drawing).
- `Devices.cs`, `A11Board.cs`: device models, symbols, A11 handheld logic.
- `Circuit.cs`: net solver. `SchematicView.cs`: rendering, hover/click. `Hud.cs`: panels.
- `Resources/A11Line*.shader`: GPU-widened anti-aliased lines + max-blend glow (no post-processing).

Camera zoom-to-cursor follows the approach in SebLague/Digital-Logic-Sim (MIT).
