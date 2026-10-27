"""Generate src/seed_data.h (PanelWireViewer seed) from the simulator netlist.

Endpoint text follows A11_Wire_Schedule_rev07.md (same device names, same "(2x)"
twin-ferrule marks), so the viewer and the schedule read the same at the bench.
Signal descriptions, build areas and the parts list are kept here.

Usage:  python -I gen_seed.py ../../A11_CircuitSim/Verification/netlist_rev07.json src/seed_data.h
"""
import json
import sys
from collections import Counter

NAMES = {
    "Mains": "Mains (gland)", "QF0": "Main CB", "QF1": "CB-VFD", "QF2": "CB-48V", "QF3": "CB-PUMP", "QF4": "CB-24V",
    "Z1": "EMI filter", "KM1": "Contactor", "K1": "K1", "G1": "PSU 48V", "G2": "PSU 24V", "U1": "VFD", "X1": "HDC-HA",
    "R1": "Braking resistor", "XS1": "Schuko", "PEBAR": "Earth bar", "BKP": "Backplate", "DOOR": "Door strap",
    "BODY": "Enclosure body (earth stud)", "RAIL": "DIN-rail earth stud", "F1": "F1", "F2": "F2", "M2": "Fan",
    "S0": "E-stop", "S1": "OFF", "S2": "ON", "H1": "Yellow lamp", "H2": "Green lamp", "H3": "Red lamp", "A1": "A11",
    "A2": "DMA860H-X", "A3": "DMA860H-Y", "A4": "DMA860H-Z", "XT": "Tool setter",
    "XL": "L busbar XL", "XN": "N busbar XN",
}

# Build areas in build order (Build Progress tab shows them in first-appearance order).
AREAS = [
    ("230 V - Mains & distribution", [101, 102, 103, 104, 105, 106, 109, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122]),
    ("230 V - Spindle & brake resistor", [123, 124, 125, 126, 127, 128, 129]),
    ("230 V - Contactor coil (via K1)", [130, 131, 132]),
    ("PE - Earthing", [138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149]),
    ("48 V - Drive power", [150, 151, 152, 153, 154, 155]),
    ("24 V - Supply, A11 & fan", [162, 164, 166, 167, 168, 169, 170, 171]),
    ("24 V - E-stop / ON / OFF / K1", [201, 202, 203, 204, 205, 206, 207, 210]),
    ("24 V - Pilot lamps", [220, 221, 222, 223, 224, 225, 226]),
    ("Signal - Step / Dir", list(range(301, 313))),
    ("Signal - Drive alarms", [315, 316, 317, 318, 319, 320]),
    ("Signal - VFD run & speed", [325, 326, 327, 328, 329]),
    ("Signal - Home & limit sensors", [330, 331, 332, 334, 335, 336, 337, 338, 339, 340, 341, 342]),
    ("Signal - Tool setter", [345, 346, 347, 348]),
]

SIGNAL = {
    101: "Mains L in", 102: "Mains N in", 103: "L after main CB", 104: "N after main CB",
    105: "Filtered L -> contactor (switched side)", 106: "Filtered N -> N distribution",
    109: "Switched L -> L distribution", 111: "Unswitched L -> CB-24V (24 V side stays live on E-stop)",
    112: "24 V PSU L (unswitched)", 113: "24 V PSU N", 114: "Switched L -> VFD breaker", 115: "VFD supply L",
    116: "VFD supply N", 117: "Switched L -> 48 V breaker", 118: "48 V PSU L", 119: "48 V PSU N",
    120: "Switched L -> pump breaker", 121: "Pump outlet L", 122: "Pump outlet N",
    123: "Spindle U (W1 brown)", 124: "Spindle V (W1 black)", 125: "Spindle W (W1 grey)", 126: "Spindle PE (W1 G/Y)",
    127: "Spindle cable shield, 360 deg clamp both ends", 128: "Braking resistor P+", 129: "Braking resistor PB",
    130: "Unswitched L -> K1 pole 41 (contactor coil feed)", 131: "K1 44 -> contactor coil A2 (K1 on = contactor on)",
    132: "Contactor coil N",
    139: "Enclosure body bond", 140: "Mains PE in", 141: "Backplate bond", 142: "Door bond (braided strap)",
    138: "Earth bar 1 -> earth bar 2 link (bars on insulators)", 143: "EMI filter earth", 144: "VFD chassis PE", 145: "48 V PSU PE", 146: "24 V PSU PE", 147: "DIN-rail bond",
    148: "0 V -> PE single-point bond (PELV)", 149: "Pump outlet PE",
    150: "X drive +48 V", 151: "X drive 48 V return", 152: "Y drive +48 V", 153: "Y drive 48 V return",
    154: "Z drive +48 V", 155: "Z drive 48 V return",
    162: "+24 V -> F1 (control: E-stop chain, lamps, fan)", 164: "+24 V -> F2 (signal: A11, sensors)",
    166: "A11 board power +24 V (J10 DC24V)", 167: "A11 board power 0 V", 168: "+24 V for A11 inputs / sensors",
    169: "0 V for A11 inputs / sensors", 170: "Fan +24 V (from F1 via aux 43 screw)", 171: "Fan 0 V",
    201: "24 V control -> E-stop NC1", 202: "E-stop NC1 -> OFF (NC)", 203: "OFF -> ON (node X)",
    204: "ON -> K1 coil A1", 205: "Node X -> K1 11 (self-hold)", 206: "K1 14 -> K1 coil A1 (self-hold)",
    207: "K1 coil 0 V", 210: "E-stop NC2 -> A11 X7 (E-stop input)",
    220: "24 V control -> contactor aux 43", 221: "Aux 44 -> yellow lamp + (power ON)", 222: "Yellow lamp 0 V",
    223: "+24 V -> green lamp +", 224: "Green lamp - -> A11 Y6 (Work)", 225: "+24 V -> red lamp +",
    226: "Red lamp - -> A11 Y5 (Alarm)",
    345: "Tool-setter touch (NO) -> A11 X4", 346: "Tool-setter COM 0 V",
    347: "Tool-setter over-travel (NC) -> E-stop NC2 (opens X7)", 348: "Tool-setter COM pin 4 -> pin 2",
    325: "Spindle FWD run (Y1 -> VFD X1)", 326: "VFD digital COM -> A11 output common",
    327: "Speed bit 1 (Y2 -> VFD X2)", 328: "Speed bit 2 (Y3 -> VFD X3)", 329: "Speed bit 3 (Y4 -> VFD X4)",
    330: "X home (X- sensor) -> A11 X1", 331: "Y home (Y- sensor) -> A11 X2", 332: "Z home (Z+ sensor) -> A11 X3",
    334: "Far-end limit X+ || Y+", 335: "Far-end limit Y+ || Z-", 336: "Far-end limits -> A11 X6 (hard limit)",
    337: "Sensor 0 V -> X-LIM", 338: "Sensor 0 V X-LIM -> Y-LIM", 339: "Sensor 0 V Y-LIM -> Z-LIM",
    340: "Sensor +24 V -> X-LIM", 341: "Sensor +24 V X-LIM -> Y-LIM", 342: "Sensor +24 V Y-LIM -> Z-LIM",
}
for i, ax in enumerate("XYZ"):
    b = 301 + 4 * i
    SIGNAL[b] = f"{ax} step/dir +5 V -> PUL+"
    SIGNAL[b + 1] = f"{ax} pulse"
    SIGNAL[b + 2] = f"{ax} direction"
    SIGNAL[b + 3] = f"{ax} PUL+ -> DIR+ (+5 V jumper)"
for i, ax in enumerate("XYZ"):
    SIGNAL[315 + 2 * i] = f"{ax} drive alarm ALM+" + (" -> A11 X5" if ax == "X" else " (parallel)")
    SIGNAL[316 + 2 * i] = f"{ax} drive alarm ALM-" + (" -> A11 OGND" if ax == "X" else " (parallel)")

# (name, category, location, spec, description, match tokens)
PARTS = [
    ("Mains (gland)", "Supply", "Cable entry", "230 V L / N / PE", "Mains cable entry.", "Mains (gland)"),
    ("Main CB C25", "Breaker", "Backplate", "C25 2P", "Incomer: 1/3 line, 2/4 load.", "Main CB"),
    ("EMI filter", "Filter", "Backplate", "HD2AC-20A, 51 x 51 x 30 mm, screwed to the plate",
     "Mains filter, far left of row A (off the DIN rail). LINE side (L, N in) faces down, LOAD side (L, N out, PE) faces up. LOUT feeds both the contactor and CB-24V.", "EMI filter"),
    ("Contactor KM1", "Contactor", "Backplate", "Hyundai UMC65 22NS, 220 VAC coil",
     "Main power contactor (switches L only). Coil A1 = N, A2 = via K1. Aux 43-44 NO -> yellow lamp; 43 also feeds the fan.", "Contactor"),
    ("L busbar XL", "Busbar", "Backplate, right strip", "160 x 16 x 42 mm, 12 points, 1 conductor per point",
     "Contactor-switched L to the branch breakers. Vertical, right of the right vertical duct.", "busbar XL"),
    ("N busbar XN", "Busbar", "Backplate, right strip", "160 x 16 x 42 mm, 12 points, 1 conductor per point",
     "All N conductors. Vertical, beside XL.", "busbar XN"),
    ("CB-24V C6A", "Breaker", "Backplate", "C6A", "24 V PSU branch (unswitched) + K1 contactor-coil feed.", "CB-24V"),
    ("CB-VFD C16A", "Breaker", "Backplate", "C16A", "VFD branch.", "CB-VFD"),
    ("CB-48V C10A", "Breaker", "Backplate", "C10A", "48 V PSU branch.", "CB-48V"),
    ("CB-PUMP C10A", "Breaker", "Backplate", "C10A", "Coolant pump outlet branch.", "CB-PUMP"),
    ("PSU 24V/20A", "Power Supply", "Backplate", "24 V / 20 A", "A11, sensors, panel, K1, fan. -V bonded to PE (wire 148).", "PSU 24V"),
    ("PSU 48V/15A", "Power Supply", "Backplate", "48 V / 15 A", "X + Y + Z drives, one +V/-V pair each.", "PSU 48V"),
    ("Fuse F1", "Fuse", "Backplate", "DIN fuse terminal 5x20, 2 A", "24 V CONTROL: E-stop chain, lamps, fan.", "F1"),
    ("Fuse F2", "Fuse", "Backplate", "DIN fuse terminal 5x20, 2 A", "24 V SIGNAL: A11 board + sensors.", "F2"),
    ("Relay K1", "Relay", "Backplate", "24 VDC coil, 2 CO, >=8 A 250 VAC (e.g. Finder 40.52) + flyback diode",
     "Self-holding ON relay. 11-14 = latch, 41-44 = 230 V feed to contactor coil. A2 has a 2nd screw for the yellow lamp 0 V.", "K1"),
    ("VFD FC300-2.2G", "Drive", "Backplate", "FC300-2.2G-S2-B4C",
     "Spindle VFD. X1 = FWD run, X2-X4 = multi-speed bits, COM -> A11 OGND.", "VFD L;VFD N;VFD U;VFD V;VFD W;VFD P;VFD X;VFD COM"),
    ("Braking resistor", "Resistor", "External", "100 ohm / 800 W", "VFD dynamic braking on P+ / PB.", "Braking resistor"),
    ("DMA860H-X", "Drive", "Backplate", "Leadshine DMA860H", "X stepper drive.", "DMA860H-X"),
    ("DMA860H-Y", "Drive", "Backplate", "Leadshine DMA860H", "Y stepper drive.", "DMA860H-Y"),
    ("DMA860H-Z", "Drive", "Backplate", "Leadshine DMA860H", "Z stepper drive.", "DMA860H-Z"),
    ("A11 interface board", "Controller", "Backplate", "RichAuto A11E interface board",
     "X1-X3 home, X4 tool touch, X5 drive alarm, X6 hard limit, X7 E-stop; Y1 FWD, Y2-Y4 speed bits, Y5 alarm lamp, Y6 work lamp.", "A11"),
    ("Fan", "Cooling", "Enclosure", "24 VDC", "Cabinet fan.", "Fan"),
    ("Earth bar 1", "Earth", "Bottom wall", "Copper 150 x 25 mm on 2 red insulators, 4 points",
     "P1 140+139, P2 141+143, P3 144+138, P4 142. Up to 2 per point (deviation from IEC 60204-1, owner decision).", "Earth bar 1"),
    ("Earth bar 2", "Earth", "Bottom wall", "Copper 150 x 25 mm on 2 red insulators, 4 points",
     "P1 138+148, P2 145+146, P3 147+149, P4 spare. Up to 2 per point (deviation from IEC 60204-1, owner decision).", "Earth bar 2"),
    ("Enclosure body (earth stud)", "Earth", "Enclosure", "Earth stud", "Enclosure body bond.", "Enclosure body"),
    ("Backplate", "Earth", "Backplate", "Earth stud", "Backplate bond.", "Backplate"),
    ("Door strap", "Earth", "Door", "6 mm2 braided strap", "Door bond.", "Door strap"),
    ("DIN-rail earth stud", "Earth", "Backplate", "Earth stud", "DIN-rail bond.", "DIN-rail earth stud"),
    ("E-stop (XB2-BS542)", "Button", "Door", "Schneider XB2-BS542 + ZB2-BE102 (2x NC)",
     "1-2 = NC1 in the 24 V control chain; 3-4 = NC2 in series with the tool-setter over-travel -> A11 X7.", "E-stop"),
    ("OFF button (NC)", "Button", "Door", "ZB2-BE102C", "Power OFF, NC.", "OFF 1;OFF 2"),
    ("ON button (NO)", "Button", "Door", "ZB2-BE101C", "Power ON, NO.", "ON 1;ON 2"),
    ("Yellow lamp", "Pilot", "Door", "22 mm 24 V DC LED", "Power ON (contactor aux 43-44).", "Yellow lamp"),
    ("Green lamp", "Pilot", "Door", "22 mm 24 V DC LED", "Work (A11 Y6).", "Green lamp"),
    ("Red lamp", "Pilot", "Door", "22 mm 24 V DC LED", "Alarm (A11 Y5).", "Red lamp"),
    ("HDC-HA-004 (spindle)", "Connector", "Side wall", "Heavy-duty 4 + PE",
     "1 U, 2 V, 3 W, PE = motor earth, SH = cable shield clamp.", "HDC-HA"),
    ("Schuko outlet", "Outlet", "Side wall", "Panel-mount", "Coolant pump outlet (own breaker).", "Schuko"),
    ("GX16-4 X-LIM", "Connector", "Side wall", "4-pin aviation", "1 X- (home), 2 X+ (far end), 3 0 V, 4 +24 V.", "X-LIM"),
    ("GX16-4 Y-LIM", "Connector", "Side wall", "4-pin aviation", "1 Y- (home), 2 Y+ (far end), 3 0 V, 4 +24 V.", "Y-LIM"),
    ("GX16-4 Z-LIM", "Connector", "Side wall", "4-pin aviation", "1 Z- (far end), 2 Z+ (home), 3 0 V, 4 +24 V.", "Z-LIM"),
    ("GX16-5 TS (tool setter)", "Connector", "Side wall", "5-pin aviation",
     "1 touch NO -> X4, 2 COM, 3 over-travel NC -> E-stop NC2, 4 COM, 5 spare.", "Tool setter"),
]


def ascii_(s):
    for a, b in (("×", "x"), ("²", "2"), ("°", " deg"), ("—", "-"), ("–", "-"), ("→", "->"), ("≥", ">=")):
        s = s.replace(a, b)
    assert all(ord(c) < 128 for c in s), s
    return s


def pretty(tid, load):
    dev, _, term = tid.partition(".")
    name = NAMES.get(dev, dev)
    if tid == "K1.A2b":
        term = "A2 (2nd screw)"
    if dev == "PEBAR":
        name, term = ("Earth bar 2" if term == "BAR2" else "Earth bar 1"), "T"
    txt = f"{name} {term}" if term not in ("T", "BAR") else name
    return txt + (" (2x)" if load[tid] == 2 else "")


def c(s):
    return '"' + ascii_(s).replace("\\", "\\\\").replace('"', '\\"') + '"'


def main():
    src, out = sys.argv[1:3]
    wires = {int(w["no"]): w for w in json.load(open(src, encoding="utf-8"))}
    load = Counter()
    for w in wires.values():
        if not w.get("notConnected"):
            load[w["from"]] += 1
            load[w["to"]] += 1

    in_areas = [n for _, ns in AREAS for n in ns]
    assert sorted(in_areas) == sorted(wires), f"area map mismatch: {set(wires) ^ set(in_areas)}"
    assert set(SIGNAL) == set(wires), f"signal map mismatch: {set(wires) ^ set(SIGNAL)}"

    # Every endpoint must match exactly one part (the viewer matches tokens as case-insensitive substrings).
    endpoints = {pretty(e, load) for w in wires.values() for e in (w["from"], w["to"])}
    for ep in sorted(endpoints):
        hits = [p[0] for p in PARTS if any(t.strip().lower() in ep.lower() for t in p[5].split(";") if t.strip())]
        assert len(hits) == 1, f"{ep!r} matches {hits}"

    lines = [
        "// ===========================================================================",
        "// seed_data.h  --  GENERATED by gen_seed.py from",
        "// A11_CircuitSim/Verification/netlist_rev07.json (United Box A11, wiring",
        "// rev 0.7: wired directly, no terminal blocks except the 230 V XL / XN",
        "// busbars). Endpoint text matches A11_Wire_Schedule_rev07.md;",
        "// \"(2x)\" = twin ferrule. Do not edit by hand -- edit gen_seed.py and rerun.",
        "//",
        "// Only used to populate an EMPTY database on first run; after that the",
        "// SQLite DB is authoritative. (File > Reset DB re-applies this seed.)",
        "// ===========================================================================",
        "#pragma once",
        "",
        "struct SeedWire {",
        "    const char* number;",
        "    const char* from_text;",
        "    const char* to_text;",
        "    const char* signal;",
        "    const char* wire_spec;",
        "    const char* area;",
        "};",
        "",
        "struct SeedPart {",
        "    const char* name;",
        "    const char* category;",
        "    const char* location;",
        "    const char* spec;",
        "    const char* description;",
        "    const char* match;   // ';'-separated case-insensitive substring tokens",
        "};",
        "",
        f"// {len(wires)} wires, in build order (area by area).",
        "static const SeedWire kSeedWires[] = {",
    ]
    for area, nums in AREAS:
        lines.append(f"    // {ascii_(area)}")
        for n in sorted(nums):
            w = wires[n]
            spec = w["spec"] + (" - NOT CONNECTED" if w.get("notConnected") else "")
            lines.append(f"    {{{c(str(n))},{c(pretty(w['from'], load))},{c(pretty(w['to'], load))},"
                         f"{c(SIGNAL[n])},{c(spec)},{c(area)}}},")
    lines += ["};", "", f"// {len(PARTS)} parts.", "static const SeedPart kSeedParts[] = {"]
    for p in PARTS:
        lines.append("    {" + ",".join(c(x) for x in p) + "},")
    lines += ["};", ""]
    open(out, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print(f"{len(wires)} wires, {len(PARTS)} parts, {len(endpoints)} endpoints -> {out}")


if __name__ == "__main__":
    main()
