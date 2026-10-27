"""Write the wire list (A11_Wire_List.pdf): one spreadsheet row per conductor, A4 portrait, in build-guide order.

Each row: number, from, to, wire (section + insulation colour), marking (number sleeve at both ends + colour band),
cut length, how each end is terminated, function. Page 1 checks the cut lengths against the owner's wire stock.

Source: A11_Wire_Lengths_rev07.csv, exported by the player with -a11lengths (routed length, function, end terminations,
build-guide group). Cut length = routed + 10 % + 15 cm, rounded up to 5 cm, as in make_wire_lengths.py.

Usage:  python make_wire_list.py [<out.pdf>]      (default ../../A11_Wire_List.pdf)
"""
import csv
import os
import sys
from collections import defaultdict
from datetime import date

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_wire_lengths import AXIS, cut  # noqa: E402  (same cut allowance as the cutting list)
from schedule import pretty  # noqa: E402  (same terminal names as the wire schedule)

BOX = "\u2610"
# Owner's wire stock (2026-10-27), metres per colour.
STOCK = {"1 mm² yellow": 90,
         "2.5 mm² brown": 4, "2.5 mm² black": 4, "2.5 mm² blue": 4, "2.5 mm² G/Y": 4,
         "4 mm² brown": 5, "4 mm² black": 5, "4 mm² blue": 5, "4 mm² G/Y": 5}
# K1 sits on a socket marked IEC and NEMA (as in the build guide).
K1_NEMA = {"A1": "13", "A2": "14", "A2b": "14", "11": "9", "21": "10", "31": "11", "41": "12", "12": "1", "22": "2",
           "32": "3", "42": "4", "14": "5", "24": "6", "34": "7", "44": "8"}
BAND = {"black": "#111111", "blue": "#2f6fd0", "dark blue": "#0d1a6b", "blue/white": "#0d1a6b", "red": "#c0140f"}
GREY, ZEBRA, HEAD = colors.HexColor("#9a9a9a"), colors.HexColor("#f2f5f9"), colors.HexColor("#d9dee6")


def stock_of(spec):
    """'1 mm² yellow, band dark blue, twisted' -> '1 mm² yellow' (the reel the piece is cut from)."""
    return spec.split(",")[0].strip()


def band_of(spec):
    for part in spec.split(","):
        part = part.strip()
        if part.startswith("band "):
            return part[5:]
    return ""


def end_name(t):
    s = pretty(t)
    dev, _, term = t.partition(".")
    if dev == "K1" and term in K1_NEMA:
        s += f" (NEMA {K1_NEMA[term]})"
    return s


def load():
    rows, cables, external = [], defaultdict(list), []
    for r in csv.DictReader(open(os.path.join(HERE, "A11_Wire_Lengths_rev07.csv"), encoding="utf-8")):
        if r["routed_m"] == "external":
            external.append(r)
            continue
        r["m"] = float(r["routed_m"])
        s = r["spec"]
        if s == "":
            drv = r["from"].split(".")[0]
            cables[f"Motor cable {AXIS.get(drv, drv)}: LiYCY 4 × 1.0 shielded"].append(r)
        elif "W1" in s:
            cables["Spindle cable W1: 4G1.5 shielded"].append(r)
        else:
            rows.append(r)
    return rows, cables, external


def build(out):
    rows, cables, external = load()
    title = ParagraphStyle("t", fontName="Helvetica-Bold", fontSize=15, leading=18)
    sub = ParagraphStyle("s", fontName="Helvetica", fontSize=7.6, leading=9.4)
    head = ParagraphStyle("h", fontName="Helvetica-Bold", fontSize=8.6, leading=10.5)
    cell = ParagraphStyle("c", fontName="Sym", fontSize=6.4, leading=7.4)
    bold = ParagraphStyle("b", parent=cell, fontName="Helvetica-Bold")
    hdr = ParagraphStyle("hd", parent=cell, fontName="Helvetica-Bold", fontSize=6.6)
    note = ParagraphStyle("n", fontName="Helvetica-Oblique", fontSize=6.8, leading=8.5)

    def P(t, st=cell):
        return Paragraph(t, st)

    def sheet(data, widths, right=()):
        """Spreadsheet look: full grid, shaded header, zebra rows, header repeated on every page."""
        st = [("GRID", (0, 0), (-1, -1), 0.3, GREY), ("BACKGROUND", (0, 0), (-1, 0), HEAD),
              ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("FONTNAME", (0, 0), (-1, -1), "Sym"), ("FONTSIZE", (0, 0), (-1, -1), 6.4),
              ("TOPPADDING", (0, 0), (-1, -1), 1.1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1.1),
              ("LEFTPADDING", (0, 0), (-1, -1), 1.8), ("RIGHTPADDING", (0, 0), (-1, -1), 1.8)]
        st += [("BACKGROUND", (0, i), (-1, i), ZEBRA) for i in range(2, len(data), 2)]
        st += [("ALIGN", (c, 0), (c, -1), "RIGHT") for c in right]
        return Table(data, colWidths=widths, repeatRows=1, style=st)

    # ---- stock check ----
    need, pieces = defaultdict(float), defaultdict(int)
    for r in rows:
        k = stock_of(r["spec"])
        need[k] += cut(r["m"])
        pieces[k] += 1
    short = []
    data = [[P("Wire (reel)", hdr), P("Pieces", hdr), P("To cut (m)", hdr), P("Have (m)", hdr), P("Left / short (m)", hdr), P("Status", hdr)]]
    for k in sorted(need, key=lambda k: (float(k.split()[0]), k)):
        have = STOCK.get(k)
        if have is None:
            left, status = "", P("to buy (parts list)")
        else:
            d = have - need[k]
            left = f"{d:+.2f}"
            status = P("OK") if d >= 0 else P(f"<font color='#c0140f'><b>SHORT {-d:.2f} m</b></font>")
            if d < 0:
                short.append((k, -d))
        data.append([P(k), str(pieces[k]), f"{need[k]:.2f}", "" if have is None else f"{have}", left, status])
    for k in sorted(set(STOCK) - set(need)):
        data.append([P(k), "0", "0.00", f"{STOCK[k]}", f"{STOCK[k]:+.2f}", P("spare")])
    stock_tbl = sheet(data, [52 * mm, 14 * mm, 20 * mm, 18 * mm, 24 * mm, 54 * mm], right=(1, 2, 3, 4))

    # ---- ferrules (from the end terminations) ----
    fer = defaultdict(int)
    for r in rows:
        for e in (r["end_from"], r["end_to"]):
            if e.startswith("twin ferrule"):
                fer[e.split(" + ")[0].replace("twin ferrule ", "twin ")] += 0.5   # one twin ferrule serves two wires
            elif e.startswith("ferrule"):
                fer[e.split(" (")[0].replace("ferrule ", "single ")] += 1
    fer_txt = ", ".join(f"{k} ×{int(v + 0.5)}" for k, v in sorted(fer.items()))

    total = sum(cut(r["m"]) for r in rows)
    story = [
        Table([[Paragraph("A11 Box - Wire List", title),
                Paragraph(f"United Control Box, wiring rev 0.7 · {date.today():%Y-%m-%d}<br/>"
                          "One row per wire, in build-guide order · lengths from the 3D duct routing", sub)]],
              colWidths=[70 * mm, 112 * mm], style=[("VALIGN", (0, 0), (-1, -1), "BOTTOM"), ("ALIGN", (1, 0), (1, 0), "RIGHT"),
                                                    ("LEFTPADDING", (0, 0), (-1, -1), 0), ("RIGHTPADDING", (0, 0), (-1, -1), 0)]),
        Spacer(1, 2 * mm),
        Paragraph(f"<b>{len(rows)} single wires, {total:.1f} m to cut</b> (+ {len(cables)} multi-core cables, last table). "
                  "<b>Cut</b> = routed + 10 % + 15 cm, rounded up to 5 cm; door wires include 0.30 m for the hinge loop. "
                  "<b>Marking</b>: the wire number on a sleeve at <b>both ends</b>. All 1 mm² wire is yellow, so its function colour goes "
                  "on as a <b>band</b> (coloured sleeve or tape) next to the number at both ends: black = L / DC power, blue = N, "
                  "red = 230 V AC control (KM1 coil), dark blue = +24 V / DC control / signals, blue + white = 0 V. "
                  "Never use plain yellow for earth: every PE wire is G/Y. "
                  "<b>2×</b> under Ends = twin ferrule shared with the wire named.", sub),
        Spacer(1, 2.5 * mm),
        Paragraph("Stock check (cut lengths against the wire you have)", head), Spacer(1, 1 * mm), stock_tbl, Spacer(1, 1.5 * mm),
        Paragraph(f"Ferrules needed: {fer_txt}.", sub), Spacer(1, 1 * mm),
    ]
    issues = [f"<b>{k}: short by {d:.2f} m</b>, buy {int(d + 1.999)} m (or 1.5 mm² G/Y for the PSU / outlet PE wires 145, 146, 148, 149)."
              for k, d in short if "G/Y" in k]
    issues += [f"<b>{k}: short by {d:.2f} m</b>." for k, d in short if "G/Y" not in k]
    issues.append("<b>48 V drive feeds 150-155 in 1 mm²</b> (10.4 A in a duct) are below the 15 A of the 48 V PSU: wiring rule R3 fails "
                  "for 150 / 152 / 154. Fix with one fuse per drive (6.3 A, holder on the DIN rail) or wire them in 1.5-2.5 mm² "
                  "(about 6 m). The drives draw far less in normal use; the rule covers a short circuit.")
    issues.append("<b>Braking resistor leads 128 / 129</b> are 1 mm² PVC (owner's choice): keep them clear of the tube, which gets very hot.")
    story += [Paragraph("Open points", head), Spacer(1, 0.6 * mm)] + [Paragraph("• " + t, sub) for t in issues] + [Spacer(1, 3 * mm)]

    # ---- one row per wire, grouped as in the build guide ----
    widths = [4 * mm, 8 * mm, 31 * mm, 31 * mm, 17 * mm, 21 * mm, 10 * mm, 29 * mm, 31 * mm]
    groups = defaultdict(list)
    for r in rows:
        groups[r["group"]].append(r)
    for g in sorted(groups):
        v = sorted(groups[g], key=lambda r: int(r["no"]) if r["no"].isdigit() else 10 ** 6)
        data = [[P("", hdr), P("No.", hdr), P("From", hdr), P("To", hdr), P("Wire", hdr), P("Marking (both ends)", hdr),
                 P("Cut m", hdr), P("Ends (from / to)", hdr), P("Function", hdr)]]
        for r in v:
            s = r["spec"]
            band = band_of(s)
            wire = stock_of(s) + (", twisted" if s.endswith("twisted") else "")
            if band:
                col = BAND.get(band, "#000000")
                mark = f"<b>{r['no']}</b> + <font color='{col}'><b>{band}</b></font> band"
            else:
                mark = f"<b>{r['no']}</b>"
            fn = r["desc"].split(" · ", 1)[1] if " · " in r["desc"] else ""
            if r["door"] == "1":
                fn = (fn + "; " if fn else "") + "door wire (hinge loop)"
            ends = f"{r['end_from']}<br/>{r['end_to']}".replace("twin ferrule 2 × ", "2× ").replace(" + wire ", " with ")
            data.append([P(BOX), P(f"<b>{r['no']}</b>"), P(end_name(r["from"])), P(end_name(r["to"])), P(wire), P(mark),
                         P(f"<b>{cut(r['m']):.2f}</b>"), P(ends), P(fn)])
        story += [Paragraph(f"{g} - {len(v)} wire{'s' if len(v) > 1 else ''}, {sum(cut(r['m']) for r in v):.2f} m", head),
                  Spacer(1, 0.8 * mm), sheet(data, widths), Spacer(1, 3 * mm)]

    # ---- multi-core cables ----
    data = [[P("", hdr), P("Cable", hdr), P("Cores (no.: from → to)", hdr), P("Cut m", hdr)]]
    for name in sorted(cables):
        v = sorted(cables[name], key=lambda r: (r["no"], r["from"]))
        cores = "<br/>".join(f"{r['no'] + ': ' if r['no'] else ''}{end_name(r['from'])} → {end_name(r['to'])}" for r in v)
        data.append([P(BOX), P(name), P(cores), P(f"<b>{cut(max(r['m'] for r in v)):.2f}</b>")])
    story += [Paragraph("Multi-core cables (cut once per cable, to the longest core)", head), Spacer(1, 0.8 * mm),
              sheet(data, [4 * mm, 50 * mm, 114 * mm, 14 * mm], right=(3,)), Spacer(1, 2 * mm)]
    if external:
        story.append(Paragraph(f"Not listed: {len(external)} conductors outside the box (motor and sensor cables to the machine).", note))
    story.append(Paragraph("Source: Verification/A11_Wire_Lengths_rev07.csv (player -a11lengths); regenerate with make_wire_list.py.", note))

    def footer(canvas, doc):
        canvas.saveState()
        canvas.setFont("Helvetica", 6.5)
        canvas.drawRightString(A4[0] - 14 * mm, 5 * mm, f"A11 Box - Wire List · page {doc.page}")
        canvas.restoreState()

    doc = SimpleDocTemplate(out, pagesize=A4, leftMargin=14 * mm, rightMargin=14 * mm, topMargin=8 * mm, bottomMargin=10 * mm,
                            title="A11 Box - Wire List")
    doc.build(story, onFirstPage=footer, onLaterPages=footer)
    return len(rows), total, short


def main():
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    font = next((p for p in ("C:/Windows/Fonts/seguisym.ttf", "C:/Windows/Fonts/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
                 if os.path.exists(p)), None)
    if font is None:
        raise SystemExit("need a font with the ballot-box and arrow glyphs (Segoe UI Symbol or DejaVu Sans)")
    pdfmetrics.registerFont(TTFont("Sym", font))
    bold = next((p for p in ("C:/Windows/Fonts/segoeuib.ttf", "C:/Windows/Fonts/DejaVuSans-Bold.ttf",
                             "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf") if os.path.exists(p)), font)
    pdfmetrics.registerFont(TTFont("SymB", bold))
    pdfmetrics.registerFontFamily("Sym", normal="Sym", bold="SymB", italic="Sym", boldItalic="SymB")
    out = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "A11_Wire_List.pdf"))
    n, total, short = build(out)
    print(f"{n} wires, {total:.2f} m to cut; short: {short}")
    print("written", out)


if __name__ == "__main__":
    main()
