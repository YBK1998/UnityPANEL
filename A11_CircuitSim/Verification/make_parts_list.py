"""Write the parts list (A11_Box_Parts_List.pdf) for the rev 0.7 box in the bought 800 x 1000 x 250 enclosure.

Wire and cable quantities come from the 3D routing (A11_Wire_Lengths_rev07.csv, exported by the player with
-a11lengths): routed length + 10 % + 15 cm per wire (per cable for multi-core cables), rounded up to whole metres.
Wire colours follow IEC 60204-1 13.2.4 and are read from the spec of each wire (netlist_rev07.json).

Usage:  python -I make_parts_list.py [<out.pdf>]      (default ../../A11_Box_Parts_List.pdf)
"""
import csv
import math
import os
import sys
from collections import defaultdict

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

HERE = os.path.dirname(os.path.abspath(__file__))
TICK = "\u2611"   # bought
BOX = "\u2610"    # to buy


def up(m):
    return max(1, math.ceil(m - 1e-9))


def length_m(rows):
    """Single wires: sum of routed lengths + 10 % + 15 cm each."""
    return up(sum(r["m"] for r in rows) * 1.1 + 0.15 * len(rows))


def cable_m(groups):
    """Multi-core cables: per cable the longest conductor + 10 % + 15 cm."""
    return up(sum(max(r["m"] for r in g) * 1.1 + 0.15 for g in groups))


def quantities():
    rows = []
    for r in csv.DictReader(open(os.path.join(HERE, "A11_Wire_Lengths_rev07.csv"), encoding="utf-8")):
        try:
            r["m"] = float(r["routed_m"])
        except ValueError:
            continue   # external (outside the cabinet)
        rows.append(r)
    by = defaultdict(list)
    for r in rows:
        s = r["spec"]
        if s == "":   # stepper motor conductors, drive -> GX16-6, one shielded 4-core cable per axis
            by["motor:" + r["to"].split(".")[0]].append(r)
        elif "W1" in s:   # VFD -> HDC-HA: U, V, W, PE cores and the shield (127), one 4G1.5 shielded cable
            by["spindle:"].append(r)
        else:   # single wires, by reel: "1 mm² yellow, band dark blue, twisted" is cut from the 1 mm² yellow
            by[s.split(",")[0].strip()].append(r)
    q = {k: length_m(v) for k, v in by.items() if ":" not in k}
    q["motor"] = cable_m([v for k, v in by.items() if k.startswith("motor:")])
    q["spindle"] = cable_m([by["spindle:"]])
    q["n"] = len(rows)
    return q


def build(out):
    q = quantities()
    listed = set()

    def w(key):
        listed.add(key)
        return f"{q.get(key, 0):g} m"
    sections = [
        ("BOUGHT", [
            (TICK, "Enclosure 800 W × 1000 H × 250 D mm, steel, swing door hinged on the left, mounting plate included, no removable panels", "x1", "wall mount"),
            (TICK, "Relay Finder 55.34.9.024.0000 + socket (RXZE2S114M look-alike), 24 V DC (K1: pole 1 latch, 2 spare, 3 empty gap, 4 → KM1 coil; AC15 350 VA ≥ 200 VA coil inrush)", "x1", "in socket"),
            (TICK, "Fuse holder RT18-32 (or RT14) 1P, DIN rail, for 10 × 38 mm fuse links (F1, F2)", "x2", "DIN"),
            (TICK, "Fuse link 10 × 38 mm (RO15) gG 2 A: use these for F1 and F2", "x2 + spares", "-"),
            (TICK, "Fuse link 10 × 38 mm aM 2 A: do NOT use for F1 / F2 (aM = motor short-circuit only, no overload protection)", "-", "spare / return"),
            (TICK, "230 V busbar 160 × 16 × 42 mm, 12 points, on insulators: XL (switched L) and XN (all N); vertical, right of the right vertical duct", "x2", "screws to plate"),
            (TICK, "Earth bar, copper 150 × 25 mm on two 17 mm red insulators, 4 points: PE 1 and PE 2, linked by wire 138. Up to 2 conductors per point "
                   "(owner decision; IEC 60204-1 asks for one protective conductor per point - a 12-point bar removes the deviation)", "x2", "bottom wall"),
            (TICK, "Slotted wire duct 40 W × 60 H with cover, 2 m (cut list in A11_Enclosure_Selection.md; motor lane VM 230 mm)", "x3", "screws to plate"),
        ]),
        ("ENCLOSURE & MOUNTING", [
            (TICK, "DIN rail TS35 × 7.5, slotted: one 330 mm piece (row A, right of the plate-mounted EMI filter), + 4 end stops", "0.5 m + x4", "screws to plate"),
            (BOX, "DIN rail earth clamp, M6 stud", "x1", "DIN"),
            (BOX, f"Door earth strap, braided ≥ 6 mm², {w('6 mm² braided strap')}, M6 lugs (door stud → earth bar, through the hinge loop)", "x1", "-"),
            (TICK, "M6 earth stud kit (bolt, 2 nuts, serrated washers): body (wire 139) and plate (wire 141), only where the enclosure has none", "x2", "body / plate"),
            (TICK, "Cable gland M20, nylon, IP68 (mains + pump cord)", "x2", "bottom wall"),
            (TICK, "80 mm plastic filter adapters, flanged: one on the 24 V fan (left wall), one on the outlet (right wall); 80 × 80 cut-outs", "x2", "side walls"),
        ]),
        ("EMC (spindle cable W1 and stepper motor cables)", [
            (BOX, "EMC shield clamp (saddle) for Ø 8–12 mm, screwed to the plate: W1 at the power lane (wire 127)", "x1", "plate"),
            (BOX, "HDC-HA metal hood + housing with EMC cable gland (360° shield), both halves; + brass EMC gland at the spindle terminal box", "x1 set + x1", "bottom wall / spindle"),
            (BOX, "Shield clamps for the motor cables at the drives (LiYCY braid to the plate)", "x3", "plate"),
        ]),
        ("CONTROL PARTS", [
            (TICK, "RichAuto A11E kit (handheld + board + 50-pin cable) - 24 V DC 3 A", "x1", "standoffs"),
            (TICK, "22 mm LED pilot lamp 24 V DC - yellow / green / red", "x1 each", "door"),
            (BOX, "NC contact block Schneider ZB2-BE102 (or the clone's 1 NC block) clipped onto the E-stop base: NC2 → A11 X7", "x1", "door"),
            (BOX, "GX16-5 plug with pins 3-4 bridged: dummy plug when the tool setter is unplugged (over-travel is in the X7 loop)", "x1", "XT"),
        ]),
        (f"WIRE - colours IEC 60204-1 (3D routing, {q['n']} conductors, +10 % +15 cm each, rounded up); "
         "stock bought 2026-10-27; per-wire marking in A11_Wire_List.pdf", [
            (TICK, f"1 mm² H05V-K yellow, 90 m: every control, signal and 230 V branch wire; function colour as a band at both ends "
                   f"(black L / 48 V, blue N, red KM1 coil, dark blue +24 V / signals, blue + white 0 V); 5V / PULSE / DIR twisted in threes. "
                   f"Needs {w('1 mm² yellow')}", "90 m", ""),
            (TICK, f"2.5 mm² H07V-K black (mains L 101, VFD L 115): needs {w('2.5 mm² black')}", "4 m", ""),
            (TICK, f"2.5 mm² H07V-K blue (mains N 102, VFD N 116): needs {w('2.5 mm² blue')}", "4 m", ""),
            (TICK, f"2.5 mm² H07V-K brown + dark-blue band (+24 V PSU → F1 / F2, 162 / 164): needs {w('2.5 mm² brown')}", "4 m", ""),
            (TICK, f"2.5 mm² H07V-K green/yellow (PE): needs {w('2.5 mm² G/Y')}", "4 m", ""),
            (BOX, "2.5 mm² (or 1.5 mm²) H07V-K green/yellow: the rest of the PE wires (stock check in A11_Wire_List.pdf)", "5 m", ""),
            (TICK, f"4 mm² H07V-K black (L, supply path behind QF0): needs {w('4 mm² black')}", "5 m", ""),
            (TICK, f"4 mm² H07V-K blue (N, supply path behind QF0): needs {w('4 mm² blue')}", "5 m", ""),
            (TICK, f"4 mm² H07V-K green/yellow (PE): needs {w('4 mm² G/Y')}", "5 m", ""),
            (TICK, "4 mm² H07V-K brown: not used (spare)", "5 m", ""),
            (BOX, "Coloured sleeves or tape for the 1 mm² bands: black, blue, red, dark blue, white (with blue for 0 V)", "1 set", ""),
            (BOX, "Control cable LiYCY 4 × 1.0 shielded (drive → motor connector, 1 per axis; also for the motor side)", f"{q['motor']:g} m", "motor lane"),
            (BOX, "Shielded cable 4G1.5, e.g. ÖLFLEX CLASSIC 110 CY (W1: VFD → spindle connector)", f"{q['spindle']:g} m", "power lane"),
        ]),
        ("FINISHING & TOOLS", [
            (TICK, "Insulated ferrules: 0.5 ×100, 1.0 ×50, 1.5 ×100, 2.5 ×50, 4 ×50", "1 box each", ""),
            (BOX, "1 mm² ferrules: the yellow wire needs more than the box of 50 (count in A11_Wire_List.pdf)", "1 box ×100", ""),
            (TICK, "Twin ferrules: 2 × 0.5 ×50, 2 × 1.5 ×10, 2 × 2.5 ×10, 2 × 4 ×10 (EMI filter LOAD L: 105 + 111)", "1 box each", ""),
            (BOX, "Twin ferrules 2 × 1 mm² (the 2 × 0.5 box no longer fits the 1 mm² wire)", "1 box ×50", ""),
            (TICK, "Wire number markers 0-9 for 0.5-4 mm²; spiral wrap 10 mm (door hinge loop)", "1 set + 1 m", ""),
            (TICK, "Knock-out punches / step drill: 16.2 (GX16), 20.5 (M20), 22.5 (door devices); nibbler or jigsaw for 2 × 80 × 80 (fan, outlet)", "if needed", "bottom wall / door"),
        ]),
        ("DECIDE BEFORE ORDERING", [
            (BOX, "C14 open: FC300 input ≈ 22–24 A at full load (11 A is output). Z1 fixed at 20 A (no larger filter): cap the spindle at ≈ 1.5 kW in the VFD (≈ 15 A input), check with a clamp meter. Z1 LOAD L: 2 × 4 mm²?", "-", ""),
            (BOX, "48 V drive feeds 150-155 are 1 mm² (10.4 A) on the 15 A PSU: fuse each drive (3 × 6.3 A + holders) or wire them in 1.5-2.5 mm²", "x3 or 6 m", ""),
        ]),
    ]
    # Every wire spec in the netlist needs its own row, or its metres silently drop off the list.
    unlisted = sorted(k for k in q if k not in listed and k not in ("motor", "spindle", "n"))
    if unlisted:
        raise SystemExit(f"wire specs with no row in the WIRE section: {unlisted}")

    title = ParagraphStyle("t", fontName="Helvetica-Bold", fontSize=16, leading=19)
    sub = ParagraphStyle("s", fontName="Helvetica", fontSize=8, leading=10)
    cell = ParagraphStyle("c", fontName="DejaVu", fontSize=6.8, leading=7.4)
    head = ParagraphStyle("h", fontName="Helvetica-Bold", fontSize=8.2, leading=9.6)
    note = ParagraphStyle("n", fontName="Helvetica-Oblique", fontSize=6.8, leading=8.5)
    story = [
        Table([[Paragraph("A11 Box - Parts to Buy", title),
                Paragraph("United Control Box, wiring rev 0.7 (direct wired; no terminal blocks, only the L / N busbars)"
                          "<br/>Enclosure: bought 800 × 1000 × 250, swing door", sub)]],
              colWidths=[70 * mm, 112 * mm], style=[("VALIGN", (0, 0), (-1, -1), "BOTTOM"), ("ALIGN", (1, 0), (1, 0), "RIGHT"),
                                                    ("LEFTPADDING", (0, 0), (-1, -1), 0), ("RIGHTPADDING", (0, 0), (-1, -1), 0)]),
        Spacer(1, 1.5 * mm),
        Paragraph("Already have: breakers, EMI filter, contactor, VFD, PSUs, drives (with ALM), E-stop (1 NC + block below), ON/OFF, connectors, "
                  "NPN NO sensors, fan, Schuko, earth bar.", sub),
        Spacer(1, 1.2 * mm),
    ]
    widths = [6 * mm, 124 * mm, 28 * mm, 24 * mm]
    for name, items in sections:
        data = [["", Paragraph(name, head), "", ""]]
        style = [("SPAN", (1, 0), (-1, 0)), ("LINEBELOW", (0, 0), (-1, 0), 0.8, colors.black),
                 ("VALIGN", (0, 0), (-1, -1), "TOP"), ("TOPPADDING", (0, 0), (-1, -1), 0.4), ("BOTTOMPADDING", (0, 0), (-1, -1), 0.4),
                 ("LEFTPADDING", (0, 0), (-1, -1), 2), ("FONTNAME", (0, 1), (0, -1), "DejaVu"), ("FONTSIZE", (0, 1), (0, -1), 9)]
        for i, (mark, text, qty, where) in enumerate(items, start=1):
            grey = mark == TICK
            c = ParagraphStyle("g", parent=cell, textColor=colors.HexColor("#555555")) if grey else cell
            data.append([mark, Paragraph(text, c), Paragraph(f"<b>{qty}</b>", c), Paragraph(where, c)])
            style.append(("LINEBELOW", (0, i), (-1, i), 0.3, colors.HexColor("#999999")))
        story += [Table(data, colWidths=widths, style=style), Spacer(1, 1.0 * mm)]
    story.append(Paragraph(
        "Lengths: 3D duct routes ±15-20 %, Verification/A11_Wire_Lengths_rev07.csv; regenerate with make_parts_list.py.", note))
    doc = SimpleDocTemplate(out, pagesize=A4, leftMargin=14 * mm, rightMargin=14 * mm, topMargin=6 * mm, bottomMargin=5 * mm,
                            title="A11 Box - Parts to Buy")
    doc.build(story)
    return q


def main():
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    font = next((p for p in ("C:/Windows/Fonts/seguisym.ttf", "C:/Windows/Fonts/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
                 if os.path.exists(p)), None)
    if font is None:
        raise SystemExit("need a font with the ballot-box glyphs (Segoe UI Symbol or DejaVu Sans)")
    pdfmetrics.registerFont(TTFont("DejaVu", font))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "A11_Box_Parts_List.pdf")
    q = build(os.path.abspath(out))
    print({k: v for k, v in sorted(q.items())})
    print("written", os.path.abspath(out))


if __name__ == "__main__":
    main()
