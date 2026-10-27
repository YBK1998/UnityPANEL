"""Write the wire cutting list (A11_Wire_Lengths.pdf) for the rev 0.7 box from the 3D routing.

Lengths come from A11_Wire_Lengths_rev07.csv (exported by the player with -a11lengths): each conductor's route
terminal -> duct -> duct -> terminal, door wires including 0.30 m for the hinge service loop. Cut length = routed + 10 %
+ 15 cm (strip, ferrule and dressing), rounded up to 5 cm - the same allowance as the parts list. Multi-core cables
(stepper motor cables, PUL / DIR cables, spindle cable W1) are cut once per cable, to the longest core.

Usage:  python -I make_wire_lengths.py [<out.pdf>]      (default ../../A11_Wire_Lengths.pdf)
"""
import csv
import math
import os
import sys
from collections import defaultdict
from datetime import date

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import KeepTogether, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from schedule import pretty  # noqa: E402  (same terminal names as the wire schedule)

BOX = "\u2610"
AXIS = {"A2": "X", "A3": "Y", "A4": "Z"}


def cut(m):
    return math.ceil((m * 1.1 + 0.15) * 20 - 1e-9) / 20


def load():
    single, cables, external = defaultdict(list), defaultdict(list), []
    for r in csv.DictReader(open(os.path.join(HERE, "A11_Wire_Lengths_rev07.csv"), encoding="utf-8")):
        if r["routed_m"] == "external":
            external.append(r)
            continue
        r["m"] = float(r["routed_m"])
        s = r["spec"]
        if s == "":
            drv = r["from"].split(".")[0]
            cables[f"Motor cable {AXIS.get(drv, drv)} - LiYCY 4 × 1.0 shielded ({drv} → XM-{AXIS.get(drv, drv)})"].append(r)
        elif "W1" in s:
            cables["Spindle cable W1 - 4G1.5 shielded (VFD → HDC-HA)"].append(r)
        elif s.endswith("twisted"):
            drv = r["to"].split(".")[0]
            cables[f"PUL / DIR cable {AXIS.get(drv, drv)} - 3 × 0.5 mm² dark blue, twisted (A11 → {drv})"].append(r)
        else:
            single[s].append(r)
    return single, cables, external


def num(r):
    return int(r["no"]) if r["no"].isdigit() else 10 ** 6


def build(out):
    single, cables, external = load()
    title = ParagraphStyle("t", fontName="Helvetica-Bold", fontSize=16, leading=19)
    sub = ParagraphStyle("s", fontName="Helvetica", fontSize=8, leading=10)
    head = ParagraphStyle("h", fontName="Helvetica-Bold", fontSize=9, leading=11)
    cell = ParagraphStyle("c", fontName="Sym", fontSize=7, leading=8)
    note = ParagraphStyle("n", fontName="Helvetica-Oblique", fontSize=7, leading=9)
    grid = [("VALIGN", (0, 0), (-1, -1), "TOP"), ("TOPPADDING", (0, 0), (-1, -1), 0.6), ("BOTTOMPADDING", (0, 0), (-1, -1), 0.6),
            ("LEFTPADDING", (0, 0), (-1, -1), 2), ("RIGHTPADDING", (0, 0), (-1, -1), 2),
            ("LINEBELOW", (0, 0), (-1, 0), 0.8, colors.black), ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"), ("FONTSIZE", (0, 0), (-1, -1), 7)]

    def P(t):
        return Paragraph(t, cell)

    n_wires = sum(len(v) for v in single.values()) + len(cables)
    total_cut = sum(cut(r["m"]) for v in single.values() for r in v) + sum(cut(max(r["m"] for r in v)) for v in cables.values())
    story = [
        Table([[Paragraph("A11 Box - Wire Cutting List", title),
                Paragraph(f"United Control Box, wiring rev 0.7 · {date.today():%Y-%m-%d}<br/>"
                          "From the 3D duct routing of the simulator (measured parts, 730 × 930 plate)", sub)]],
              colWidths=[80 * mm, 102 * mm], style=[("VALIGN", (0, 0), (-1, -1), "BOTTOM"), ("ALIGN", (1, 0), (1, 0), "RIGHT"),
                                                    ("LEFTPADDING", (0, 0), (-1, -1), 0), ("RIGHTPADDING", (0, 0), (-1, -1), 0)]),
        Spacer(1, 2 * mm),
        Paragraph(f"<b>Cut = routed + 10 % + 15 cm</b>, rounded up to 5 cm. {n_wires} pieces, <b>{total_cut:.1f} m</b> in total. "
                  "Door wires include 0.30 m for the hinge service loop. Multi-core cables are cut once, to the longest core. "
                  "Routes follow the duct centre lines, so the real length can differ by ±15-20 %: cut the long runs a little long "
                  "and trim on fitting. (2×) = twin ferrule with another wire on that screw.", sub),
        Spacer(1, 2 * mm),
    ]

    # Summary per wire type.
    data = [["Wire / cable", "Pieces", "Routed (m)", "To cut (m)"]]
    for s in sorted(single, key=lambda k: (-sum(r["m"] for r in single[k]))):
        v = single[s]
        data.append([P(s), str(len(v)), f"{sum(r['m'] for r in v):.2f}", f"{sum(cut(r['m']) for r in v):.2f}"])
    for name in sorted(cables):
        v = cables[name]
        data.append([P(name), "1", f"{max(r['m'] for r in v):.2f}", f"{cut(max(r['m'] for r in v)):.2f}"])
    data.append(["Total", str(n_wires), "", f"{total_cut:.2f}"])
    story += [Paragraph("Summary by wire type", head), Spacer(1, 1 * mm),
              Table(data, colWidths=[120 * mm, 16 * mm, 22 * mm, 24 * mm], repeatRows=1,
                    style=grid + [("ALIGN", (1, 0), (-1, -1), "RIGHT"), ("LINEABOVE", (0, -1), (-1, -1), 0.6, colors.black),
                                  ("FONTNAME", (0, -1), (-1, -1), "Helvetica-Bold")]),
              Spacer(1, 3 * mm)]

    # Count conductors per terminal for the twin-ferrule mark.
    load_ = defaultdict(int)
    for v in list(single.values()) + list(cables.values()):
        for r in v:
            load_[r["from"]] += 1
            load_[r["to"]] += 1

    def end(t):
        return pretty(t) + (" (2×)" if load_[t] == 2 else "")

    widths = [6 * mm, 12 * mm, 60 * mm, 60 * mm, 16 * mm, 16 * mm, 12 * mm]
    for s in sorted(single, key=lambda k: (-sum(r["m"] for r in single[k]))):
        v = sorted(single[s], key=num)
        data = [["", "No.", "From", "To", "Routed", "Cut (m)", "Door"]]
        for r in v:
            data.append([P(BOX), r["no"], P(end(r["from"])), P(end(r["to"])), f"{r['m']:.2f}", f"{cut(r['m']):.2f}", "door" if r["door"] == "1" else ""])
        st = grid + [("LINEBELOW", (0, i), (-1, i), 0.25, colors.HexColor("#aaaaaa")) for i in range(1, len(data))]
        st += [("FONTNAME", (5, 1), (5, -1), "Helvetica-Bold"), ("ALIGN", (4, 0), (5, -1), "RIGHT")]
        story += [KeepTogether([Paragraph(f"{s} - {len(v)} piece{'s' if len(v) > 1 else ''}, {sum(cut(r['m']) for r in v):.2f} m", head), Spacer(1, 0.8 * mm)]),
                  Table(data, colWidths=widths, repeatRows=1, style=st), Spacer(1, 2.5 * mm)]

    # Multi-core cables: one piece each, cores listed.
    data = [["", "Cable", "Cores (no.: from → to)", "Longest", "Cut (m)"]]
    for name in sorted(cables):
        v = sorted(cables[name], key=lambda r: (num(r), r["from"]))
        cores = "<br/>".join(f"{r['no'] + ': ' if r['no'] else ''}{end(r['from'])} → {end(r['to'])} ({r['m']:.2f})" for r in v)
        longest = max(r["m"] for r in v)
        data.append([P(BOX), P(name), P(cores), f"{longest:.2f}", f"{cut(longest):.2f}"])
    st = grid + [("LINEBELOW", (0, i), (-1, i), 0.25, colors.HexColor("#aaaaaa")) for i in range(1, len(data))]
    st += [("FONTNAME", (4, 1), (4, -1), "Helvetica-Bold"), ("ALIGN", (3, 0), (4, -1), "RIGHT")]
    story += [KeepTogether([Paragraph("Multi-core cables (cut once per cable)", head), Spacer(1, 0.8 * mm)]),
              Table(data, colWidths=[6 * mm, 50 * mm, 94 * mm, 16 * mm, 16 * mm], repeatRows=1, style=st), Spacer(1, 2.5 * mm)]

    if external:
        story.append(Paragraph(f"Not in this list: {len(external)} conductors outside the box (motor and sensor cables from the "
                               "connectors to the machine) - their length depends on the machine.", note))
    story.append(Paragraph("Source: Verification/A11_Wire_Lengths_rev07.csv; regenerate with make_wire_lengths.py after re-exporting "
                           "the lengths (player -a11lengths).", note))
    doc = SimpleDocTemplate(out, pagesize=A4, leftMargin=14 * mm, rightMargin=14 * mm, topMargin=8 * mm, bottomMargin=8 * mm,
                            title="A11 Box - Wire Cutting List")
    doc.build(story)
    return n_wires, total_cut


def main():
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    font = next((p for p in ("C:/Windows/Fonts/seguisym.ttf", "C:/Windows/Fonts/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
                 if os.path.exists(p)), None)
    if font is None:
        raise SystemExit("need a font with the ballot-box and arrow glyphs (Segoe UI Symbol or DejaVu Sans)")
    pdfmetrics.registerFont(TTFont("Sym", font))
    out = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "A11_Wire_Lengths.pdf"))
    n, total = build(out)
    print(f"{n} pieces, {total:.2f} m to cut")
    print("written", out)


if __name__ == "__main__":
    main()
