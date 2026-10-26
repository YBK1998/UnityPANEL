"""Cross-check the simulator netlist against the § 8 wire schedule in the PDF.

Usage:  python -I verify_netlist.py <manual.pdf> <netlist.json> <report.md>

The PDF tables are read with pdfplumber (cell by cell). Each simulator terminal id is
mapped to the exact text the manual uses for it, so a wire that lands on the wrong
terminal in the simulator does not match the From / To cell of the manual.
"""
import json
import re
import sys

import pdfplumber

# Simulator terminal id -> accepted § 8 cell text (after normalization).
TERM = {
    "Mains.L": ["Mains L (gland)"], "Mains.N": ["Mains N (gland)"], "Mains.PE": ["Mains PE (gland)"],
    "QF0.1": ["Main CB L in"], "QF0.3": ["Main CB N in"], "QF0.2": ["Main CB L out"], "QF0.4": ["Main CB N out"],
    "Z1.LIN": ["EMI L-IN"], "Z1.NIN": ["EMI N-IN"], "Z1.LOUT": ["EMI L-OUT"], "Z1.NOUT": ["EMI N-OUT"],
    "Z1.GND": ["EMI filter ground lug"],
    "TB1.T": ["TB1", "TB1 (L-UNSW)"], "TB2.T": ["TB2", "TB2 (N-UNSW)"],
    "KM1.1L1": ["Contactor 1L1"], "KM1.3L2": ["Contactor 3L2"], "KM1.2T1": ["Contactor 2T1"], "KM1.4T2": ["Contactor 4T2"],
    "KM1.A1": ["Contactor A1"], "KM1.A2": ["Contactor A2"], "KM1.43": ["Contactor aux 43"], "KM1.44": ["Contactor aux 44"],
    "LBUS.T": ["L bus"], "NBUS.T": ["N bus"],
    "QF4.1": ["CB-24V (C6A) in"], "QF4.2": ["CB-24V out"], "G2.L": ["PSU 24V L"], "G2.N": ["PSU 24V N"],
    "QF1.1": ["CB-VFD (C16A) in"], "QF1.2": ["CB-VFD out"], "U1.L": ["VFD L"], "U1.N": ["VFD N"],
    "QF2.1": ["CB-48V (C10A) in"], "QF2.2": ["CB-48V out"], "G1.L": ["PSU 48V L"], "G1.N": ["PSU 48V N"],
    "QF3.1": ["CB-PUMP (C10A) in"], "QF3.2": ["CB-PUMP out"], "XS1.L": ["Schuko L"], "XS1.N": ["Schuko N"],
    "U1.U": ["VFD U"], "U1.V": ["VFD V"], "U1.W": ["VFD W"], "U1.PE": ["VFD PE"], "U1.PEb": ["VFD PE"], "U1.PEc": ["VFD PE"],
    "U1.P+": ["VFD P+"], "U1.PB": ["VFD PB"],
    "X1.1": ["HDC-HA pin 1"], "X1.2": ["HDC-HA pin 2"], "X1.3": ["HDC-HA pin 3"], "X1.4": ["HDC-HA pin 4"], "X1.PE": ["HDC-HA PE"],
    "R1.T1": ["Braking resistor T1"], "R1.T2": ["Braking resistor T2"],
    "K1.21": ["K1 pole-2 COM"], "K1.24": ["K1 pole-2 NO"], "K1.11": ["K1 pole-1 COM"], "K1.14": ["K1 pole-1 NO"],
    "K1.A1": ["K1 coil A1"], "K1.A2": ["K1 coil A2"],
    "PEBAR.BAR": ["Earth bar"], "BKP.T": ["Backplate bond"], "DOOR.T": ["Door bond strap"], "DINTB.T": ["DIN-rail earth TB"],
    "G1.PE": ["PSU 48V PE"], "G2.PE": ["PSU 24V PE"], "TB0V.T": ["TB0V"],
    "G2.+V": ["PSU 24V +V"], "G2.-V": ["PSU 24V -V"], "TB24P.T": ["TB24+"], "TB24C.T": ["TB24C"], "TB24S.T": ["TB24S"],
    "F1.in": ["F1 in"], "F1.out": ["F1 out"], "F2.in": ["F2 in"], "F2.out": ["F2 out"],
    "A1.DC24V": ["A11 board power +"], "A1.DCGND": ["A11 board power -"], "A1.24V": ["A11 input 24V+"], "A1.GND": ["A11 input GND"],
    "M2.+": ["Fan +"], "M2.-": ["Fan -"],
    "S0.1": ["E-stop NC1 in"], "S0.2": ["E-stop NC1 out"], "S0.4": ["E-stop NC2"], "S0.3": ["E-stop NC2 (other screw)"],
    "S1.1": ["OFF (NC) in"], "S1.2": ["OFF out (Node X)", "Node X (OFF out)"], "S2.1": ["ON (NO) in"], "S2.2": ["ON out"],
    "H1.+": ["Yellow lamp +"], "H1.-": ["Yellow lamp -"], "H2.+": ["Green lamp +"], "H2.-": ["Green lamp -"],
    "H3.+": ["Red lamp +"], "H3.-": ["Red lamp -"],
    "A1.X7": ["A11 X7"], "A1.Y6": ["A11 Y6"], "A1.Y5": ["A11 Y5"], "A1.X5": ["A11 X5"],
    "A1.Y1": ["A11 Y1"], "A1.Y2": ["A11 Y2 (S1)"], "A1.Y3": ["A11 Y3 (S2)"], "A1.Y4": ["A11 Y4 (S3)"], "A1.OGND": ["A11 output GND"],
    "U1.X1": ["VFD X1"], "U1.X2": ["VFD X2"], "U1.X3": ["VFD X3"], "U1.X4": ["VFD X4"], "U1.COM": ["VFD COM (digital)"],
    "A1.X1": ["A11 X1"], "A1.X2": ["A11 X2"], "A1.X3": ["A11 X3"], "A1.X4": ["A11 X4"], "A1.X6": ["A11 X6"], "A1.X8": ["A11 X8"],
    "TBHL.T": ["TB-HL"],
    "XT.1": ["TS pin 1 (red)"], "XT.2": ["TS pin 2 (black)"], "XT.3": ["TS pin 3 (blue)"], "XT.4": ["TS pin 4 (yellow)"],
}
AX = {"X": "A2", "Y": "A3", "Z": "A4"}
for i, (pv, mv) in enumerate([("+V1", "-V1"), ("+V2", "-V2"), ("+V3", "-V3")], start=1):
    TERM[f"G1.{pv}"] = [f"PSU 48V +V (screw {i})"]
    TERM[f"G1.{mv}"] = [f"PSU 48V -V (screw {i})"]
for ax, d in AX.items():
    TERM[f"A1.{ax}_5V"] = [f"A11 {ax}-axis 5V"]
    TERM[f"A1.{ax}_PUL"] = [f"A11 {ax}-axis PULSE"]
    TERM[f"A1.{ax}_DIR"] = [f"A11 {ax}-axis DIR"]
    for t in ["PUL+", "PUL-", "DIR+", "DIR-", "ALM+", "ALM-"]:
        TERM[f"{d}.{t}"] = [f"DMA860H-{ax} {t}"]
    TERM[f"{d}.AC1"] = [f"DMA860H-{ax} AC 1"]
    TERM[f"{d}.AC2"] = [f"DMA860H-{ax} AC 2"]
    TERM[f"{ax}-LIM.1"] = [f"{ax}-LIM pin 1"]
    TERM[f"{ax}-LIM.2"] = [f"{ax}-LIM pin 2"]
    TERM[f"{ax}-LIM.3"] = [f"{ax}-LIM pin 3"]
    TERM[f"{ax}-LIM.4"] = [f"{ax}-LIM pin 4"]


def norm(s):
    s = (s or "").replace("\n", " ")
    for ch in "−–—":
        s = s.replace(ch, "-")
    return re.sub(r"\s+", " ", s).strip()


def read_schedule(pdf_path):
    rows = {}
    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            for table in page.extract_tables():
                for row in table:
                    if row and row[0] and re.fullmatch(r"\d{3}", row[0].strip()) and len(row) >= 3:
                        no = int(row[0])
                        if 100 < no < 400:
                            rows[no] = {"from": norm(row[1]), "to": norm(row[2]),
                                        "func": norm(row[3]) if len(row) > 3 else "",
                                        "spec": norm(row[4]) if len(row) > 4 else ""}
    return rows


def main():
    pdf_path, net_path, out_path = sys.argv[1:4]
    pdf_rows = read_schedule(pdf_path)
    net = {int(w["no"]): w for w in json.load(open(net_path, encoding="utf-8"))}
    lines = ["# § 8 wire schedule vs simulator netlist", "",
             f"PDF rows read: {len(pdf_rows)} · simulator numbered wires: {len(net)}", "",
             "| No. | PDF From → To | Simulator From → To | Result |", "|---|---|---|---|"]
    fails = 0
    for no in sorted(set(pdf_rows) | set(net)):
        p, w = pdf_rows.get(no), net.get(no)
        if p is None or w is None:
            fails += 1
            lines.append(f"| {no} | {'—' if p is None else p['from'] + ' → ' + p['to']} | "
                         f"{'—' if w is None else w['from'] + ' → ' + w['to']} | **FAIL: missing** |")
            continue
        ok_from = p["from"] in [norm(x) for x in TERM.get(w["from"], [])]
        ok_to = p["to"] in [norm(x) for x in TERM.get(w["to"], [])]
        res = "PASS" if ok_from and ok_to else "**FAIL**" + ("" if ok_from else " from") + ("" if ok_to else " to")
        if not (ok_from and ok_to):
            fails += 1
        lines.append(f"| {no} | {p['from']} → {p['to']} | `{w['from']}` → `{w['to']}` | {res} |")
    lines.insert(2, f"**{len(pdf_rows) - fails if fails <= len(pdf_rows) else 0} of {len(pdf_rows)} rows match, {fails} mismatches.**")
    lines.insert(3, "")
    open(out_path, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"rows={len(pdf_rows)} wires={len(net)} mismatches={fails}")
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
