"""Write the wire list (A11_Wire_List.pdf): one spreadsheet row per conductor, in build-guide order, with the safety
checks to do before each group and wire, on one A4 sheet printed front and back (portrait, flip on the long edge).

Each row: number, from, to, wire, colour band, cut length, end terminations, function and checks. Each build group
opens with its CHECK FIRST row; the sheet ends with the § 7.1 dead tests and the § 7.2 power-up order (as in the
simulator's procedures). The font shrinks until the sheet fits two pages. Drawn like a printed Excel sheet: column
letters, row numbers, header row repeated on page 2.

Source: A11_Wire_Lengths_rev07.csv, exported by the player with -a11lengths (routed length, function, end terminations,
build-guide group). Cut length = routed + 10 % + 15 cm, rounded up to 5 cm, as in make_wire_lengths.py.

Usage:  python make_wire_list.py [<out.pdf>]      (default ../../A11_Wire_List.pdf)
"""
import csv
import os
import re
import sys
from collections import defaultdict
from datetime import date

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import Paragraph, SimpleDocTemplate, Table

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
BAND = {"black": ("black", "#111111"), "blue": ("blue", "#2f6fd0"), "dark blue": ("dk blue", "#0d1a6b"),
        "blue/white": ("bl/wh", "#0d1a6b"), "red": ("red", "#c0140f")}
# Excel look: grey gridlines and row / column headings, green header row, light green section bands, amber check rows.
GRID, HEADING, INK = colors.HexColor("#c8c8c8"), colors.HexColor("#e9e9e9"), colors.HexColor("#555555")
HEAD, BANDROW, CHECKROW = colors.HexColor("#1f6e43"), colors.HexColor("#dfeee5"), colors.HexColor("#fff3d1")
WARN = "#a3260f"
SIGN = "<font name='Sym'>⚠</font>"   # Calibri has no warning sign
# Plain-words name after the first mention of each designator in a check or test cell: "Z1" -> "Z1 (EMI filter)".
NAMES = [("QF0", "main breaker"), ("QF1", "VFD breaker"), ("QF2", "48 V PSU breaker"), ("QF3", "pump outlet breaker"),
         ("QF4", "24 V PSU + coil breaker"), ("Z1", "EMI filter"), ("F1", "control fuse"), ("F2", "signal fuse"),
         ("G1", "48 V PSU"), ("G2", "24 V PSU"), ("KM1", "contactor"), ("K1", "latch relay"), ("XL", "switched L busbar"),
         ("XN", "N busbar"), ("XT", "tool-setter socket"), ("X7", "E-stop input"), ("X5", "drive alarm input"),
         ("X4", "tool-setter input"), ("W1", "spindle cable"), ("HDC-HA", "spindle connector"), ("GX16", "round connector"),
         ("VFD", "spindle drive"), ("A11", "controller"), ("PELV", "earthed 24 V"), ("§ 7.1", "dead tests"), ("§ 7.2", "power-up")]
_NAME_RE = [(re.compile(r"(?<![\w(–-])" + re.escape(k) + r"(?![\w–-])(?! \()"), f"{k} ({v})") for k, v in NAMES]


def explain(t):
    """Add the plain-words name after the first stand-alone mention of each designator (not in ranges like QF1–QF3,
    not after "VFD " where X1–X4 are VFD terminals, not when a bracket already follows)."""
    for rx, rep in _NAME_RE:
        m = next((m for m in rx.finditer(t) if not t[:m.start()].endswith("VFD ") and "<" not in t[t.rfind(">", 0, m.start()) + 1:m.start()]), None)
        if m:
            t = t[:m.start()] + rep + t[m.end():]
    return t
MARGIN = 8 * mm
ROWNO = 5 * mm
WIDTHS = [3.5, 6, 25, 25, 13, 10, 7, 22, 77.5]   # mm: ☐, No., From, To, Wire, Band, Cut, Ends, Function / check
PAGES = 2   # one A4 sheet, front and back

# Check before wiring each build group (the guide's order), and before single wires.
GROUP_CHECK = {
    "1": "Scrape the paint under the body and plate studs, serrated washer under the lug. PE is G/Y only, never plain yellow. "
         "After the group: every PE point beeps to the earth bar.",
    "2": "Supply cord unplugged. L = black, N = blue; read each part's label before fitting: Z1 LINE in / LOAD out, "
         "VFD L / N in and U / V / W out. KM1 switches L only. Tighten and tug-test every 230 V screw.",
    "3": "PSU input selector (if fitted) on 230 V. Fit the drive ends only after § 7.2 reads G1 at 46–50 V DC. "
         "R3 open on 150 / 152 / 154 (1 mm² on the 15 A PSU): fuse each drive or use 1.5–2.5 mm² first.",
    "4": "Dark blue band = +24 V, blue/white = 0 V: check the polarity at every PSU and A11 terminal. Door wires go through the "
         "spiral-wrapped hinge loop. Leave 131 (KM1 A2), 166 and 168 (A11) off until § 7.2. Sensors (NPN NO): brown +24 V, "
         "blue 0 V; slide the GX16 shell and heat-shrink on before soldering.",
    "5": "5 V / PULSE / DIR are 5 V signals: never 24 V on them; twisted, away from 230 V and motor cables. GX16: slide the shell "
         "and heat-shrink on before soldering. NPN NO sensors: brown +24 V, blue 0 V.",
}
WIRE_CHECK = {
    "101": "connect the supply cable last, after the § 7.1 dead tests", "102": "connect last, after § 7.1",
    "140": "connect last, after § 7.1",
    "103": "Z1 LINE side (from QF0); do not swap LINE / LOAD", "104": "Z1 LINE side; do not swap LINE / LOAD",
    "115": "VFD input L, never U / V / W", "116": "VFD input N, never U / V / W",
    "128": "keep ≥ 50 mm from the tube (very hot)", "129": "keep ≥ 50 mm from the tube (very hot)",
    "131": "leave the KM1 A2 end off until § 7.2",
    "139": "bare metal under the lug", "141": "bare metal under the lug",
    "148": "must beep 0 V ↔ PE in § 7.1",
    "150": "drive end off until G1 reads 46–50 V; R3: fuse or ≥ 1.5 mm²", "151": "drive end off until § 7.2",
    "152": "drive end off until G1 reads 46–50 V; R3: fuse or ≥ 1.5 mm²", "153": "drive end off until § 7.2",
    "154": "drive end off until G1 reads 46–50 V; R3: fuse or ≥ 1.5 mm²", "155": "drive end off until § 7.2",
    "166": "A11 end off until F2 out reads 23–25 V DC (§ 7.2)", "168": "A11 end off until F2 out reads 23–25 V DC",
    "210": "§ 7.1: X7 opens with the E-stop pushed", "347": "§ 7.1: X7 opens with over-travel pushed",
    "326": "§ 7.2: VFD COM ↔ G2 −V < 1 V DC",
}
CABLE_CHECK = ("Motor cables: Ω-check the coil pairs at the motor before soldering (A+ / A− one coil, B+ / B− the other); never "
               "plug or unplug a motor with its drive powered; LiYCY braid to the plate clamp at the drive end. W1: 360° shield at the "
               "HDC-HA hood and the VFD clamp; U / V / W / PE only.")
GENERAL = ("<b>Before any wiring:</b> supply cord unplugged, QF0–QF4 OFF; if the VFD has been powered, wait until its display and "
           "charge lamp are out. Number sleeve and colour band on <b>both</b> ends before fitting; ferrule every stranded end; "
           "max. 2 conductors per terminal (R1); tug-test each screw.")
DEAD_TESTS = ("<b>Before first power</b> (§ 7.1, QF0 OFF, Ω meter): Z1 LOUT ↔ NOUT open or high · 230 V (Z1 LOUT) ↔ 0 V, "
              "+24 V and PE: open · 148: 0 V ↔ PE beeps · G1 + / − and G2 + / −: not 0 Ω · A11 X / Y / Z 5V ↔ G2 +V: open · "
              "A11 PUL ↔ G1 +V: open · ON / OFF / E-stop chain and K1 test lever as § 7.1.")
POWER_UP = ("<b>Power-up in order</b> (§ 7.2): QF1–QF3 OFF, 131 / 166 / 168 off → QF0 ON, hands off the plate → F2 out 23–25 V DC, "
            "then connect the A11 → ON holds K1, E-stop drops it → QF0 OFF, fit 131, ON → XL ↔ XN ≈ 230 V AC → "
            "QF2 ON (drives off): G1 46–50 V → fit 150–155, jog each axis slowly → VFD COM ↔ G2 −V < 1 V → spindle.")


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


def short_end(e):
    """End termination in a few characters (legend at the top of the sheet)."""
    for pat, rep in ((r"^supply cable core from the gland$", "core"), (r"^crimp contact.*", "crimp"),
                     (r"^solder cup.*", "solder"), (r"^ring lug M6$", "ring M6"), (r"^ring lug.*", "ring"),
                     (r"^insulated fork lug$", "fork"), (r"^2 fork lugs under one screw \+ wire (\d+)$", r"fork+\1"),
                     (r"^twin ferrule 2 × [\d.]+ mm² \+ wire (\d+)$", r"2×+\1"), (r"^ferrule [\d.]+ mm² \(own point.*", "fer·own"),
                     (r"^ferrule [\d.]+ mm²$", "fer"), (r"^360° clamp in the HDC-HA.*", "360° hood"), (r"^360° EMC clamp.*", "360° clamp")):
        if re.match(pat, e):
            return re.sub(pat, rep, e)
    return e


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


class Sheet:
    """One worksheet as a single table: column letters on top, row numbers on the left, header row repeated per page."""

    def __init__(self, widths, fs):
        self.widths = widths
        self.rows = [[""] + [chr(65 + i) for i in range(len(widths))]]
        self.style = [("FONTNAME", (0, 0), (-1, -1), "Cal"), ("FONTSIZE", (0, 0), (-1, -1), fs), ("LEADING", (0, 0), (-1, -1), fs * 1.15),
                      ("GRID", (0, 0), (-1, -1), 0.3, GRID), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                      ("BACKGROUND", (0, 0), (-1, 0), HEADING), ("BACKGROUND", (0, 0), (0, -1), HEADING),
                      ("TEXTCOLOR", (0, 0), (-1, 0), INK), ("TEXTCOLOR", (0, 0), (0, -1), INK),
                      ("ALIGN", (0, 0), (-1, 0), "CENTER"), ("ALIGN", (0, 0), (0, -1), "CENTER"),
                      ("TOPPADDING", (0, 0), (-1, -1), 0.5), ("BOTTOMPADDING", (0, 0), (-1, -1), 0.9),
                      ("LEFTPADDING", (0, 0), (-1, -1), 1.4), ("RIGHTPADDING", (0, 0), (-1, -1), 1.4)]
        self.repeat = (0,)

    def add(self, cells, *style):
        r = len(self.rows)
        self.rows.append([str(r)] + list(cells) + [""] * (len(self.widths) - len(cells)))
        self.style += [(s[0], (s[1], r), (s[2], r)) + tuple(s[3:]) for s in style]
        return r

    def span(self, content, *style):
        """A row merged across all columns (title, note, section band, check row)."""
        return self.add([content], ("SPAN", 1, -1), *style)

    def header(self, cells, right=()):
        r = self.add(cells, ("FONTNAME", 1, -1, "CalB"), ("BACKGROUND", 1, -1, HEAD), ("TEXTCOLOR", 1, -1, colors.white),
                     *[("ALIGN", c + 1, c + 1, "RIGHT") for c in right])
        self.repeat = (0, r)
        return r

    def section(self, text):
        return self.span(text, ("FONTNAME", 1, -1, "CalB"), ("BACKGROUND", 1, -1, BANDROW))

    def check(self, content):
        return self.span(content, ("BACKGROUND", 1, -1, CHECKROW))

    def table(self):
        return Table(self.rows, colWidths=[ROWNO] + self.widths, style=self.style, repeatRows=self.repeat, hAlign="LEFT")


def warn(t):
    return f"<font color='{WARN}'><b>{SIGN} {t}</b></font>"


def add_wire(ws, r, P, sym):
    """One wire row: box, number, from, to, wire, band, cut, ends, function with its check."""
    s = r["spec"]
    band = band_of(s)
    name, col = BAND.get(band, (band, "#000000"))
    wire = stock_of(s).replace(" mm²", "") + (" tw" if s.endswith("twisted") else "")
    fn = r["desc"].split(" · ", 1)[1] if " · " in r["desc"] else ""
    if r["door"] == "1":
        fn = (fn + "; " if fn else "") + "door loop"
    if r["no"] in WIRE_CHECK:
        fn = (fn + " " if fn else "") + warn(explain(WIRE_CHECK[r["no"]]))
    ws.add([P(BOX, sym), P(f"<b>{r['no']}</b>"), P(end_name(r["from"])), P(end_name(r["to"])), P(wire),
            P(f"<font color='{col}'><b>{name}</b></font>") if band else "", f"{cut(r['m']):.2f}",
            P(f"{short_end(r['end_from'])} / {short_end(r['end_to'])}"), P(fn)], ("FONTNAME", 7, 7, "CalB"), ("ALIGN", 7, 7, "RIGHT"))


def add_cable(ws, name, v, P, sym):
    """One multi-core cable row: name, cut to the longest core, the cores."""
    v = sorted(v, key=lambda r: (r["no"], r["from"]))
    cores = " · ".join(f"{r['no'] + ': ' if r['no'] else ''}{end_name(r['from'])} → {end_name(r['to'])}" for r in v)
    ws.add([P(BOX, sym), "", P(f"<b>{name}</b>"), "", "", "", f"{cut(max(r['m'] for r in v)):.2f}", P(cores)],
           ("SPAN", 3, 6), ("SPAN", 8, 9), ("FONTNAME", 7, 7, "CalB"), ("ALIGN", 7, 7, "RIGHT"))


def make_footer(tab, title, pages):
    def footer(canvas, doc):
        """Sheet tab bottom left, page number bottom right."""
        canvas.saveState()
        x, y, h = MARGIN, 3.2 * mm, 4.2 * mm
        w = canvas.stringWidth(tab, "CalB", 6.5) + 6 * mm
        canvas.setFillColor(colors.white)
        canvas.setStrokeColor(GRID)
        canvas.rect(x, y, w, h, fill=1, stroke=1)
        canvas.setStrokeColor(HEAD)
        canvas.setLineWidth(1.4)
        canvas.line(x + 1, y + 0.7, x + w - 1, y + 0.7)
        canvas.setFillColor(HEAD)
        canvas.setFont("CalB", 6.5)
        canvas.drawCentredString(x + w / 2, y + 1.4 * mm, tab)
        canvas.setFillColor(INK)
        canvas.setFont("Cal", 6.5)
        canvas.drawRightString(A4[0] - MARGIN, y + 1.4 * mm, f"{title} · page {doc.page} of {pages} · "
                                                            "print double-sided, flip on the long edge")
        canvas.restoreState()
    return footer


def register_fonts():
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont

    def first(*paths):
        return next((p for p in paths if os.path.exists(p)), None)
    font = first("C:/Windows/Fonts/seguisym.ttf", "C:/Windows/Fonts/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
    if font is None:
        raise SystemExit("need a font with the ballot-box and arrow glyphs (Segoe UI Symbol or DejaVu Sans)")
    pdfmetrics.registerFont(TTFont("Sym", font))
    pdfmetrics.registerFontFamily("Sym", normal="Sym", bold="Sym", italic="Sym", boldItalic="Sym")
    # Calibri for the Excel look; DejaVu (or Segoe UI Symbol) where Calibri is missing.
    body = first("C:/Windows/Fonts/calibri.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf") or font
    bold = first("C:/Windows/Fonts/calibrib.ttf", "C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf") or body
    pdfmetrics.registerFont(TTFont("Cal", body))
    pdfmetrics.registerFont(TTFont("CalB", bold))
    pdfmetrics.registerFontFamily("Cal", normal="Cal", bold="CalB", italic="Cal", boldItalic="CalB")


def build(out, fs):
    rows, cables, external = load()
    cell = ParagraphStyle("c", fontName="Cal", fontSize=fs, leading=fs * 1.15)
    sym = ParagraphStyle("y", parent=cell, fontName="Sym")
    title = ParagraphStyle("t", parent=cell, fontName="CalB", fontSize=fs * 1.9, leading=fs * 2.2)
    hdr = ParagraphStyle("h", parent=cell, textColor=colors.white)

    def P(t, st=cell):
        return Paragraph(t, st)

    need, pieces = defaultdict(float), defaultdict(int)
    for r in rows:
        k = stock_of(r["spec"])
        need[k] += cut(r["m"])
        pieces[k] += 1
    total = sum(cut(r["m"]) for r in rows)
    stock, short = [], []
    for k in sorted(need, key=lambda k: (float(k.split()[0]), k)):
        have = STOCK.get(k)
        if have is None:
            stock.append(f"{k} {need[k]:.2f} (to buy)")
        elif have >= need[k]:
            stock.append(f"{k} {need[k]:.2f} / {have} OK")
        else:
            short.append((k, need[k] - have))
            stock.append(f"<font color='{WARN}'><b>{k} {need[k]:.2f} / {have} SHORT {need[k] - have:.2f}</b></font>")
    fer = defaultdict(float)
    for r in rows:
        for e in (r["end_from"], r["end_to"]):
            if e.startswith("twin ferrule"):
                fer[e.split(" + ")[0].replace("twin ferrule ", "twin ")] += 0.5   # one twin ferrule serves two wires
            elif e.startswith("ferrule"):
                fer[e.split(" (")[0].replace("ferrule ", "")] += 1

    ws = Sheet([w * mm for w in WIDTHS], fs)
    ws.span(P(f"A11 Box – Wire List <font size='{fs}' name='Cal'>· United Control Box, wiring rev 0.7 · {date.today():%Y-%m-%d} · "
              f"{len(rows)} wires, {total:.1f} m to cut + {len(cables)} cables · build-guide order</font>", title))
    ws.span(P("<b>Cut</b> = routed + 10 % + 15 cm, rounded up to 5 cm (door wires incl. 0.30 m hinge loop). <b>Band</b>: all 1 mm² wire is "
              "yellow; its function colour goes on as a band next to the number sleeve at both ends (black L / DC power, blue N, red "
              "230 V control, dk blue +24 V / signals, bl/wh 0 V). <b>Ends</b> from / to: fer = ferrule, fer·own = own point on the "
              "distribution block, 2×+N = twin ferrule with wire N, fork(+N) = insulated fork lug (2 under one screw with N), "
              "ring = ring lug, solder = GX16 cup, crimp = HDC-HA contact, core = supply cable core."))
    ws.span(P("<b>Stock (cut / have, m):</b> " + " · ".join(stock) + " · <b>Ferrules:</b> "
              + ", ".join(f"{k} ×{int(v + 0.5)}" for k, v in sorted(fer.items()))))
    ws.check(P(f"<font color='{WARN}'><b>{SIGN}</b></font> " + explain(GENERAL)))
    ws.header(["", "No.", "From", "To", "Wire mm²", "Band", "Cut", "Ends (from / to)", P(f"<b>Function · <font name='Sym'>⚠</font> check before / while wiring</b>", hdr)], right=(6,))

    groups = defaultdict(list)
    for r in rows:
        groups[r["group"]].append(r)
    for g in sorted(groups):
        v = sorted(groups[g], key=lambda r: int(r["no"]) if r["no"].isdigit() else 10 ** 6)
        ws.section(f"{g} – {len(v)} wire{'s' if len(v) > 1 else ''}, {sum(cut(r['m']) for r in v):.2f} m")
        if g.split(" ")[0] in GROUP_CHECK:
            ws.check(P(f"<font color='{WARN}'><b>{SIGN} CHECK FIRST:</b></font> " + explain(GROUP_CHECK[g.split(' ')[0]])))
        for r in v:
            add_wire(ws, r, P, sym)

    ws.section(f"Multi-core cables – {len(cables)}, cut once per cable to the longest core")
    ws.check(P(f"<font color='{WARN}'><b>{SIGN} CHECK FIRST:</b></font> " + explain(CABLE_CHECK)))
    for name in sorted(cables):
        add_cable(ws, name, cables[name], P, sym)

    ws.section("Before power")
    ws.check(P(f"<font color='{WARN}'><b>{SIGN}</b></font> " + explain(DEAD_TESTS)))
    ws.check(P(f"<font color='{WARN}'><b>{SIGN}</b></font> " + explain(POWER_UP)))
    notes = [f"<b>Open:</b> {k} short by {d:.2f} m – buy {int(d + 1.999)} m (or 1.5 mm² G/Y for PE 145, 146, 148, 149)" for k, d in short]
    notes.append("braking resistor leads 128 / 129 are 1 mm² PVC (owner's choice)")
    if external:
        notes.append(f"not listed: {len(external)} conductors outside the box (motor and sensor cables to the machine)")
    notes.append("source A11_Wire_Lengths_rev07.csv (player -a11lengths), regenerate with make_wire_list.py")
    ws.span(P(" · ".join(notes)))

    footer = make_footer("Wire list", "A11 Box – Wire List", PAGES)
    doc = SimpleDocTemplate(out, pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN, topMargin=7 * mm, bottomMargin=9 * mm,
                            title="A11 Box - Wire List")
    doc.build([ws.table()], onFirstPage=footer, onLaterPages=footer)
    return len(rows), total, short, doc.page


def main():
    register_fonts()
    out = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "A11_Wire_List.pdf"))
    fs = 8.5
    while True:   # largest font that keeps the sheet on one A4 sheet, front and back
        n, total, short, pages = build(out, fs)
        if pages <= PAGES or fs <= 5.0:
            break
        fs = round(fs - 0.1, 1)
    print(f"{n} wires, {total:.2f} m to cut, {pages} pages at {fs} pt; short: {short}")
    print("written", out)


if __name__ == "__main__":
    main()
