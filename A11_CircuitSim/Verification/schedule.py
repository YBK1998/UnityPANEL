"""Write the rev 0.7 wire schedule (Markdown) from netlist_rev07.json, marking changes against rev 0.5.

Usage:  python -I schedule.py <netlist_rev07.json> <netlist_rev05.json> <out.md>
"""
import json
import sys
from collections import Counter

NAMES = {
    "Mains": "Mains (gland)", "QF0": "Main CB", "QF1": "CB-VFD", "QF2": "CB-48V", "QF3": "CB-PUMP", "QF4": "CB-24V",
    "Z1": "EMI filter", "KM1": "Contactor", "K1": "K1", "G1": "PSU 48V", "G2": "PSU 24V", "U1": "VFD", "X1": "HDC-HA",
    "R1": "Braking resistor", "XS1": "Schuko", "PEBAR": "Earth bar", "BKP": "Backplate", "DOOR": "Door strap", "BODY": "Enclosure body (earth stud)",
    "RAIL": "DIN-rail earth stud", "F1": "F1", "F2": "F2", "M2": "Fan", "S0": "E-stop", "S1": "OFF", "S2": "ON",
    "H1": "Yellow lamp", "H2": "Green lamp", "H3": "Red lamp", "A1": "A11", "A2": "DMA860H-X", "A3": "DMA860H-Y",
    "A4": "DMA860H-Z", "XT": "Tool setter", "XL": "L busbar XL", "XN": "N busbar XN",
}


def pretty(tid):
    dev, _, term = tid.partition(".")
    name = NAMES.get(dev, dev)
    if tid == "K1.A2b":
        term = "A2 (2nd screw)"
    if dev == "PEBAR":
        return "Earth bar 2" if term == "BAR2" else "Earth bar 1"
    return f"{name} {term}" if term not in ("T", "BAR") else name


def main():
    new, old, out = sys.argv[1:4]
    n = {w["no"]: w for w in json.load(open(new, encoding="utf-8"))}
    o = {w["no"]: w for w in json.load(open(old, encoding="utf-8"))}
    load = Counter()
    for w in n.values():
        if not w.get("notConnected"):
            load[w["from"]] += 1
            load[w["to"]] += 1
    lines = ["# Wire schedule rev 0.7 — wired directly; no terminal blocks, only the 230 V L / N busbars", "",
             "Generated from the simulator netlist (`netlist_rev07.json`). Max. 2 conductors per device terminal; "
             "\"2×\" = twin ferrule. XL and XN (12-point busbars) take one conductor per point; the two 4-point earth bars take up to 2 per point "
             "(owner decision 2026-10-26) and are linked by 138. Changes against the rev 0.5 schedule (§ 8) are marked.", "",
             "Colours (IEC 60204-1 13.2.4): L **black**, N **light blue**, PE **green/yellow**, AC control (KM1 coil) **red**, "
             "DC power (48 V, brake resistor) **black**, +24 V / DC control / signals **dark blue**, 0 V **blue/white**. "
             "The spindle cable W1 is one 4G1.5 shielded cable (cores brown / black / grey / G/Y).", "",
             "| No. | From | To | Spec | Change vs rev 0.5 |", "|---|---|---|---|---|"]
    for no in sorted(n, key=int):
        w = n[no]
        f, t = w["from"], w["to"]
        tag = []
        if no not in o:
            tag.append("**new**")
        else:
            ow = o[no]
            if (ow["from"], ow["to"]) != (f, t):
                tag.append(f"was {pretty(ow['from'])} → {pretty(ow['to'])}")
            if ow["spec"] != w["spec"]:
                tag.append(f"was {ow['spec']}")
        fm = f"{pretty(f)}{' (2×)' if load[f] == 2 else ''}"
        tm = f"{pretty(t)}{' (2×)' if load[t] == 2 else ''}"
        nc = " — not connected" if w.get("notConnected") else ""
        lines.append(f"| {no} | {fm} | {tm} | {w['spec']}{nc} | {'; '.join(tag)} |")
    removed = sorted(set(o) - set(n), key=int)
    lines += ["", f"**Removed: {', '.join(removed)}.** 107 / 108 / 160 / 161 / 163 / 165 / 333 only connected to terminal blocks "
              "(TB1, TB2, TB24+, TB24C, TB24S, TB0V, TB-HL). 110 (KM1 4T2 → N bus) is gone because KM1 switches L only and every N "
              "conductor sits on XN. 211 (0 V to E-stop NC2) is gone: NC2 (added ZB2-BE102 block) now gets its 0 V through the "
              "tool-setter over-travel (347), so X7 opens on the E-stop or on over-travel, not on OFF.", ""]
    open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"{len(n)} wires, {len(removed)} removed -> {out}")


if __name__ == "__main__":
    main()
