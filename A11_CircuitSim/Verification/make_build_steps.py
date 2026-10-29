"""Write the stage-by-stage build (A11_Build_Steps.pdf): the same wire rows as A11_Wire_List.pdf, regrouped into stages
that can each be tested on their own (earth, supply + 24 V, K1 latch, KM1 contactor, ...), each followed by its dead
and live tests. An error then shows up in the stage that caused it, not after the whole box is wired.

Every wire of A11_Wire_Lengths_rev07.csv must sit in exactly one stage (checked). Test values follow the simulator's
§ 7.1 / § 7.2 procedures. Same Excel look as make_wire_list.py; the font shrinks until the sheet fits PAGES pages
(A4 portrait, printed double-sided, flip on the long edge).

Usage:  python make_build_steps.py [<out.pdf>]      (default ../../A11_Build_Steps.pdf)
"""
import os
import sys
from datetime import date

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import Paragraph, SimpleDocTemplate

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from make_wire_list import (BOX, CABLE_CHECK, GROUP_CHECK, MARGIN, SIGN, WARN, WIDTHS, Sheet, add_cable, add_wire,  # noqa: E402
                            cut, explain, load, make_footer, register_fonts)

TESTROW = colors.HexColor("#ddebf7")   # Excel's light blue
PAGES = 4   # two A4 sheets, front and back
LIVE, DEAD = "LIVE", "dead"


def r(*nos):
    return [str(n) for n in nos]


# (title, wires, cable-name prefixes, check first, tests as (dead / LIVE, what to do, what you must see))
STAGES = [
    ("Protective earth", r(138, 139, 141, 142, 143, 144, 145, 146, 147, 148, 149), [],
     "All parts and ducts mounted, no other wire yet. " + GROUP_CHECK["1"],
     [(DEAD, "Each PE point (body and plate studs, door, DIN rail, Z1 GND, VFD PEc, G1 / G2 PE, Schuko PE) ↔ earth bar", "beep, ≈ 0 Ω"),
      (DEAD, "Earth bar 1 ↔ earth bar 2 (138); door stud ↔ body with the door fully open (142)", "beep"),
      (DEAD, "G2 −V ↔ earth bar (148, the PELV bond)", "beep")]),
    ("Supply, EMI filter and 24 V PSU", r(140, 101, 102, 103, 104, 106, 111, 112, 113, 162, 164), [],
     "Cord unplugged. Read the labels: Z1 LINE in from QF0, LOAD out. F1 / F2 carriers open while wiring. "
     "Supply cable last: PE (140) first, then L (101) and N (102).",
     [(DEAD, "QF0 OFF: Z1 LOUT ↔ Z1 NOUT", "open, or high (filter bleed)"),
      (DEAD, "QF0 ON, cord still unplugged: plug L ↔ PE and plug N ↔ PE", "open"),
      (DEAD, "G2 +V ↔ G2 −V", "not 0 Ω"),
      (LIVE, "Plug in; QF1–QF3 OFF, F1 / F2 open; QF0 ON, QF4 ON: G2 +V ↔ G2 −V", "23–25 V DC, +V positive (else adjust G2)"),
      (LIVE, "Close F1 and F2: F1 out ↔ G2 −V and F2 out ↔ G2 −V", "23–25 V DC each"),
      (LIVE, "Open F1: F1 out ↔ G2 −V; close F1 again", "0 V")]),
    ("K1 self-hold: ON / OFF / E-stop (24 V)", r(201, 202, 203, 204, 205, 206, 207), [],
     "K1 in its socket. Door wires 201–205 through the spiral-wrapped hinge loop. K1 pole 4 → KM1 (130 / 131) is not wired yet, "
     "so only K1 moves in this stage.",
     [(DEAD, "§ 7.1: F1 out ↔ K1 A1, no button pushed", "open"),
      (DEAD, "Same probes, ON held", "beep"),
      (DEAD, "ON held and E-stop pushed; then ON held and OFF pushed", "open, open"),
      (LIVE, "Push ON and release", "K1 pulls in and stays in (latch)"),
      (LIVE, "Push OFF. Then ON again and push the E-stop", "K1 drops out each time"),
      (LIVE, "E-stop pushed: push ON. Release the E-stop", "K1 stays off; no restart until ON")]),
    ("Contactor KM1 (230 V coil) and yellow lamp", r(130, 131, 132, 105, 109, 220, 221, 222), [],
     "KM1 coil is 230 V AC: 130 / 131 red band, 132 blue. Z1 LOUT → KM1 1L1, KM1 2T1 → XL (KM1 switches L only). "
     "QF1–QF3 stay OFF: XL feeds them.",
     [(DEAD, "QF4 out ↔ KM1 A2; then hold the K1 test lever", "open; beep"),
      (DEAD, "KM1 A1 ↔ XN; Z1 LOUT ↔ XL", "beep; open"),
      (LIVE, "Push ON", "K1 and KM1 pull in, yellow lamp on"),
      (LIVE, "XL ↔ XN", "207–253 V AC"),
      (LIVE, "Push OFF; then ON and the E-stop", "KM1 drops, lamp off, XL ↔ XN ≈ 0 V")]),
    ("Pump outlet (QF3)", r(120, 121, 122), [],
     "Schuko PE (149) is already in from stage 1. Nothing plugged into the outlet while wiring.",
     [(DEAD, "Schuko L ↔ N; L ↔ PE; N ↔ PE", "open"),
      (LIVE, "Push ON, QF3 ON: Schuko L ↔ N", "207–253 V AC"),
      (LIVE, "Push OFF; plug the pump in, ON, then OFF", "0 V when off; pump runs only with KM1 in")]),
    ("A11 controller power, fan and lamps", r(166, 167, 168, 169, 170, 171, 223, 224, 225, 226), [],
     "Dark blue band = +24 V, blue/white = 0 V. Fit 166 / 168 at F2 and 167 / 169 at G2, leave their A11 ends loose until 6.2. "
     "Door wires 223–226 through the hinge loop.",
     [(DEAD, "Loose ends 166 ↔ 167 and 168 ↔ 169", "not 0 Ω"),
      (LIVE, "QF0, QF4 ON: loose ends 166 ↔ 167 and 168 ↔ 169. Then QF0 OFF, unplug, fit the A11 ends",
       "23–25 V DC, 166 / 168 positive; green and red lamps off"),
      (LIVE, "Power up", "handheld starts, fan runs"),
      (LIVE, "Handheld status", "E-stop / X7 alarm is expected until stage 7")]),
    ("E-stop input, sensors and tool setter", r(210, 330, 331, 332, 334, 335, 336, 337, 338, 339, 340, 341, 342, 345, 346, 347, 348), [],
     "NPN NO sensors: brown +24 V, blue 0 V. Slide the GX16 shell and heat-shrink on before soldering. "
     "Tool setter unplugged: GX16-5 dummy plug (pins 3–4 bridged) in XT.",
     [(DEAD, "§ 7.1: A11 X7 ↔ G2 −V, E-stop and over-travel released", "beep"),
      (DEAD, "Same probes, E-stop pushed; then over-travel pushed", "open, open"),
      (DEAD, "X / Y / Z-LIM 4 ↔ A11 24V; LIM 3 ↔ A11 GND; LIM 4 ↔ LIM 3", "beep; beep; not 0 Ω"),
      (LIVE, "Power up, E-stop released", "the E-stop / X7 alarm clears"),
      (LIVE, "Hold steel at each sensor", "sensor LED on, its input changes: X1 / X2 / X3 home, X6 limit"),
      (LIVE, "Touch the tool-setter plate to the tool", "X4 changes")]),
    ("48 V supply, drives and motor cables", r(117, 118, 119, 150, 151, 152, 153, 154, 155), ["Motor cable"],
     GROUP_CHECK["3"] + " Set each drive's current and microstep switches for its motor before power. "
     "Motor cables: Ω-check the coil pairs first; never plug or unplug a motor with its drive powered.",
     [(DEAD, "G1 +V ↔ G1 −V", "not 0 Ω"),
      (LIVE, "150–155 loose at the drives; ON, QF2 ON: G1 +V ↔ G1 −V. Then QF0 OFF, wait for the G1 LED to go out",
       "46–50 V DC (else adjust G1)"),
      (DEAD, "Each motor at the drive end: A+ ↔ A−, B+ ↔ B−; A+ ↔ B+", "a few Ω each; open"),
      (LIVE, "Fit 150–155; ON, QF2 ON", "drive power LEDs on, no alarm LED, motors hold")]),
    ("Step / direction signals and drive alarm", r(301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 315, 316, 317, 318, 319, 320), [],
     GROUP_CHECK["5"].split(" GX16:")[0],
     [(DEAD, "§ 7.1: A11 X / Y / Z 5V ↔ G2 +V; A11 PUL ↔ G1 +V", "open; open"),
      (DEAD, "Jumpers 304 / 308 / 312: PUL+ ↔ DIR+ at each drive", "beep"),
      (DEAD, "A11 X5 ↔ ALM+ of each drive; A11 OGND ↔ ALM− of each drive", "beep"),
      (LIVE, "Jog each axis slowly with the handheld", "smooth, the right way (else change the direction setting)"),
      (LIVE, "Home each axis at low speed", "stops at its home sensor")]),
    ("VFD, braking resistor and spindle", r(114, 115, 116, 128, 129, 325, 326, 327, 328, 329), ["Spindle cable"],
     "VFD L / N in, U / V / W out: never mains on U / V / W. Braking resistor tube ≥ 50 mm from ducts and wires. "
     + CABLE_CHECK.split("end. ")[1] + " Before the first cut, limit the VFD to ≈ 1.5 kW (C14: Z1 is 20 A).",
     [(DEAD, "Braking resistor alone, before 128 / 129: T1 ↔ T2", "≈ 100 Ω"),
      (DEAD, "VFD L ↔ N; spindle U / V / W ↔ PE at the HDC-HA", "not 0 Ω; open"),
      (LIVE, "Push ON, QF1 ON", "VFD display on"),
      (LIVE, "§ 7.2: VFD X1 ↔ VFD COM; VFD COM ↔ G2 −V", "≤ 24 V DC, X1 positive; < 1 V DC"),
      (LIVE, "Start the spindle slowly from the handheld; try each speed grade", "turns the right way, each grade its own frequency"),
      (LIVE, "Heavy test cut, clamp meter on Z1 LINE L", "< 20 A (else lower the VFD limit)")]),
    ("Final check", [], [],
     "All ducts covered, every screw tug-tested, every number sleeve and colour band on.",
     [(DEAD, "Repeat the whole § 7.1 dead test (simulator: Procedures, P)", "all pass"),
      (LIVE, "Repeat the § 7.2 power-up in order", "all pass"),
      (DEAD, "Power off: open and close the door 10 times, repeat tests 3.1–3.3", "unchanged")]),
]
GENERAL = ("<b>Between stages:</b> QF0 OFF and the cord unplugged; prove it dead with the meter (Z1 LINE L ↔ N, L ↔ PE) before you touch. "
           "<b>Live tests:</b> one hand, insulated probes, a CAT III meter, nobody else at the box. <b>A test fails:</b> stop, fix it and "
           "repeat that stage's tests before the next stage. Number sleeve and colour band on both ends; ferrule every stranded end; "
           "max. 2 conductors per terminal (R1).")


def build(out, fs):
    rows, cables, external = load()
    by_no = {w["no"]: w for w in rows}
    placed = [n for s in STAGES for n in s[1]]
    dup = sorted({n for n in placed if placed.count(n) > 1})
    missing = sorted(set(by_no) - set(placed))
    unknown = sorted(set(placed) - set(by_no))
    cab = [c for s in STAGES for p in s[2] for c in cables if c.startswith(p)]
    if dup or missing or unknown or sorted(cab) != sorted(cables):
        raise SystemExit(f"stage plan does not match the wire list: twice {dup}, missing {missing}, unknown {unknown}, "
                         f"cables {sorted(set(cables) - set(cab))}")

    cell = ParagraphStyle("c", fontName="Cal", fontSize=fs, leading=fs * 1.15)
    sym = ParagraphStyle("y", parent=cell, fontName="Sym")
    title = ParagraphStyle("t", parent=cell, fontName="CalB", fontSize=fs * 1.9, leading=fs * 2.2)
    hdr = ParagraphStyle("h", parent=cell, textColor=colors.white)

    def P(t, st=cell):
        return Paragraph(t, st)

    total = sum(cut(w["m"]) for w in rows)
    ws = Sheet([w * mm for w in WIDTHS], fs)
    ws.span(P(f"A11 Box – Build and Test, Stage by Stage <font size='{fs}' name='Cal'>· United Control Box, wiring rev 0.7 · "
              f"{date.today():%Y-%m-%d} · {len(STAGES) - 1} stages</font>", title))
    ws.span(P("Wire one stage, then pass all its tests before the next: each stage only adds parts the earlier stages already proved, "
              "so a fault shows up where it was made. Columns, cut lengths, bands and end codes as in A11_Wire_List.pdf (fer = ferrule, "
              "fer·own = own point on the distribution block, 2×+N = twin ferrule with wire N, fork(+N) = fork lug (2 under one screw "
              "with N), ring = ring lug, solder = GX16 cup, crimp = HDC-HA contact, core = supply cable core). "
              f"<b>Tests:</b> <font color='{WARN}'><b>LIVE</b></font> = powered, dead = QF0 OFF and the cord unplugged, Ω / beep meter. "
              "Values from the § 7.1 / § 7.2 procedures."))
    ws.check(P(f"<font color='{WARN}'><b>{SIGN}</b></font> " + explain(GENERAL)))
    ws.header(["", "No.", "From", "To", "Wire mm²", "Band", "Cut", "Ends (from / to)",
               P(f"<b>Function · <font name='Sym'>⚠</font> check before / while wiring</b>", hdr)], right=(6,))

    for k, (name, nos, prefixes, check, tests) in enumerate(STAGES, start=1):
        v = [by_no[n] for n in nos]
        cs = [c for p in prefixes for c in sorted(cables) if c.startswith(p)]
        metres = sum(cut(w["m"]) for w in v) + sum(cut(max(x["m"] for x in cables[c])) for c in cs)
        what = ", ".join(x for x in (f"{len(v)} wire{'s' if len(v) != 1 else ''}" if v else "",
                                     f"{len(cs)} cable{'s' if len(cs) != 1 else ''}" if cs else "") if x)
        ws.section(f"Stage {k} · {name}" + (f" – {what}, {metres:.2f} m" if what else ""))
        ws.check(P(f"<font color='{WARN}'><b>{SIGN} CHECK FIRST:</b></font> " + explain(check)))
        for w in v:
            add_wire(ws, w, P, sym)
        for c in cs:
            add_cable(ws, c, cables[c], P, sym)
        nxt = f"before stage {k + 1}" if k < len(STAGES) else "before the first job"
        ws.add(["", "", P(f"<b>TEST {k} – pass all {nxt}</b>"), "", "", "", "", "", P("<b>You must see</b>")],
               ("SPAN", 3, 8), ("BACKGROUND", 1, -1, TESTROW))
        for i, (kind, do, see) in enumerate(tests, start=1):
            tag = f"<font color='{WARN}'><b>LIVE</b></font>" if kind == LIVE else "<font color='#666666'>dead</font>"
            ws.add([P(f"{BOX} <b>{k}.{i}</b>", sym), "", P(f"{tag} {explain(do)}"), "", "", "", "", "", P(f"<b>{explain(see)}</b>")],
                   ("SPAN", 1, 2), ("SPAN", 3, 8))

    notes = []
    if external:
        notes.append(f"Not listed: {len(external)} conductors outside the box (motor and sensor cables to the machine)")
    notes.append(f"{total:.1f} m of single wire to cut in all")
    notes.append("source A11_Wire_Lengths_rev07.csv, regenerate with make_build_steps.py")
    ws.span(P(" · ".join(notes)))

    doc = SimpleDocTemplate(out, pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN, topMargin=7 * mm, bottomMargin=9 * mm,
                            title="A11 Box - Build and Test, Stage by Stage")
    doc.build([ws.table()], onFirstPage=make_footer("Build & test", "A11 Box – Build and Test", PAGES),
              onLaterPages=make_footer("Build & test", "A11 Box – Build and Test", PAGES))
    return len(rows), doc.page


def main():
    register_fonts()
    out = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "A11_Build_Steps.pdf"))
    fs = 8.5
    while True:   # largest font that keeps the sheet on PAGES pages
        n, pages = build(out, fs)
        if pages <= PAGES or fs <= 5.0:
            break
        fs = round(fs - 0.1, 1)
    print(f"{n} wires in {len(STAGES) - 1} stages, {pages} pages at {fs} pt")
    print("written", out)


if __name__ == "__main__":
    main()
