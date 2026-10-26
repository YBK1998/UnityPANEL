"""Wiring rule checks on a simulator netlist export (netlist_rev05.json / netlist.json).

Usage:  python -I wiring_rules.py <netlist.json> <rev label> <report.md>

Checks
  R1  conductors per terminal  (device terminal: max 2, e.g. Finder 94.04 "2 x 2.5 mm2";
      terminal block nodes: 2 per screw x 2 screws x number of blocks from § 6)
  R2  PE: every part with a PE terminal / exposed metal is bonded to the earth bar
  R3  cross-section vs upstream protection, IEC 60204-1:2016 Table 6, method B2 (in duct, PVC, 40 °C)
  R4  a physical part exists for every node (no "virtual" junctions)
  R5  electrical equivalence against another revision (optional 4th/5th args)
  R6  neutral conductors: a N conductor carries the return current of every branch behind it, so it is
      checked against the sum of those branch protections (capped by the main CB)
"""
import json
import re
import sys
from collections import defaultdict

# Terminal-block nodes of rev 0.5 and their block count (§ 4.1, § 6).
TB_BLOCKS = {"TB1.T": 1, "TB2.T": 1, "TB24P.T": 2, "TB24C.T": 1, "TB24S.T": 2, "TB0V.T": 4, "TBHL.T": 1, "DINTB.T": 1}
VIRTUAL = {"LBUS.T": "L bus", "NBUS.T": "N bus"}          # § 8 endpoints with no part in § 2 / § 6
MULTI = {"PEBAR.BAR": 8, "PEBAR.BAR2": 8, "XL.T": 12, "XN.T": 12}  # earth bars: 4 points x 2 conductors (owner decision 2026-10-26); 230 V L / N busbars: 12 points

# Internal conducting paths of parts (for protection analysis: contacts assumed closed, fuses / MCBs are boundaries).
THROUGH = [("Z1.LIN", "Z1.LOUT"), ("Z1.NIN", "Z1.NOUT"), ("KM1.1L1", "KM1.2T1"), ("KM1.3L2", "KM1.4T2"), ("KM1.43", "KM1.44"),
           ("K1.11", "K1.14"), ("K1.41", "K1.44"), ("S0.1", "S0.2"), ("S0.3", "S0.4"), ("S1.1", "S1.2"), ("S2.1", "S2.2"),
           ("XT.3", "XT.4"),
           ("U1.PE", "U1.PEb"), ("U1.PE", "U1.PEc"), ("A1.GND", "A1.DCGND"), ("A1.GND", "A1.OGND"),
           ("G2.+V1", "G2.+V2"), ("G2.-V1", "G2.-V2"), ("G1.+V1", "G1.+V2"), ("G1.+V1", "G1.+V3"), ("G1.-V1", "G1.-V2"), ("G1.-V1", "G1.-V3")]

# Upstream protection (A) seen from the output side of each protective device / current-limited source.
PROTECT = {"QF0.2": 25, "QF0.4": 25, "QF1.2": 16, "QF2.2": 10, "QF3.2": 10, "QF4.2": 6, "F1.out": 2, "F2.out": 2,
           "G2.+V": 20, "G2.+V1": 20, "G2.+V2": 20, "G1.+V1": 15, "G1.+V2": 15, "G1.+V3": 15}
# IEC 60204-1 Table 6, B2. 0.5 mm² is not in the table; 3 A is used as a conservative control-wiring value.
AMPACITY = {0.5: 3.0, 0.75: 7.6, 1.0: 10.4, 1.5: 13.5, 2.5: 18.3, 4.0: 25.0, 6.0: 32.0}
PE_REQUIRED = ["Mains.PE", "Z1.GND", "U1.PE", "G1.PE", "G2.PE", "X1.PE", "XS1.PE", "BKP.T", "DOOR.T", "BODY.T"]
# N terminal of each load → the protection of the L conductor feeding that load (A), for R6.
LOAD_N = {"U1.N": 16, "G1.N": 10, "XS1.N": 10, "G2.N": 6, "KM1.A1": 6}
MAIN_CB = 25
# Same physical point under its rev 0.5 / rev 0.6 name (PSU screws split into two, DIN-rail earth).
ALIAS = {"G2.+V1": "G2.+V", "G2.+V2": "G2.+V", "G2.-V1": "G2.-V", "G2.-V2": "G2.-V", "RAIL.T": "DINTB.T", "K1.A2b": "K1.A2"}


class UF:
    def __init__(self):
        self.p = {}

    def find(self, a):
        self.p.setdefault(a, a)
        while self.p[a] != a:
            self.p[a] = self.p[self.p[a]]
            a = self.p[a]
        return a

    def union(self, a, b):
        self.p[self.find(a)] = self.find(b)


def section(spec):
    m = re.match(r"\s*([0-9.]+)", spec or "")
    return float(m.group(1)) if m else None


def is_neutral(w):
    s = w["spec"].lower()
    return w["kind"] == "AC" and ("light blue" in s or (re.search(r"\bblue\b", s) is not None and "dark blue" not in s and "blue/white" not in s))


def is_return(w):
    """N, 0 V and PSU −V conductors (by colour, or by the −V screw they start from)."""
    s = w["spec"].lower()
    return is_neutral(w) or "blue/white" in s or any(".-V" in e for e in (w["from"], w["to"])) or \
        ("black" in s and w["kind"] == "DC24")  # rev 0.5 / 0.6: 0 V was black


def nets(wires, extra=()):
    uf = UF()
    for w in wires:
        if not w.get("notConnected"):
            uf.union(w["from"], w["to"])
    for a, b in extra:
        uf.union(a, b)
    return uf


def main():
    path, label, out = sys.argv[1:4]
    wires = [w for w in json.load(open(path, encoding="utf-8")) if not w.get("notConnected")]
    # Same physical point under one name, used for the PE check (R2) and the equivalence check (R5).
    aliased = [dict(w, **{"from": ALIAS.get(w["from"], w["from"]), "to": ALIAS.get(w["to"], w["to"])}) for w in wires]
    lines =[f"# Wiring rule check — {label}", "", f"{len(wires)} numbered conductors.", ""]
    problems = 0

    # R1 conductors per terminal
    count = defaultdict(list)
    for w in wires:
        count[w["from"]].append(w["no"])
        count[w["to"]].append(w["no"])
    lines += ["## R1 Conductors per terminal", "", "| Terminal | Conductors | Capacity | Result |", "|---|---|---|---|"]
    for t, ws in sorted(count.items(), key=lambda kv: -len(kv[1])):
        if t in MULTI:
            cap = MULTI[t]
        elif t in TB_BLOCKS:
            cap = TB_BLOCKS[t] * 2 * 2
        else:
            cap = 2
        if len(ws) > 2 or t in TB_BLOCKS or t in VIRTUAL:
            ok = len(ws) <= cap and t not in VIRTUAL
            # A single feed-through block has 2 screws: more than 2 conductors per screw is not possible.
            if t in TB_BLOCKS and TB_BLOCKS[t] == 1 and len(ws) > 4:
                ok = False
            if not ok:
                problems += 1
            lines.append(f"| `{t}` | {len(ws)} ({', '.join(ws)}) | {cap if t not in VIRTUAL else 'no part'} | {'PASS' if ok else '**FAIL**'} |")
    lines.append("")

    # R2 PE
    uf = nets(aliased, [("U1.PE", "U1.PEb"), ("U1.PE", "U1.PEc")])  # the VFD PE studs are one part
    lines += ["## R2 Protective earth", ""]
    for t in PE_REQUIRED:
        ok = uf.find(t) == uf.find("PEBAR.BAR")
        if not ok:
            problems += 1
        lines.append(f"- {'PASS' if ok else '**FAIL**'} `{t}` bonded to the earth bar")
    lines.append("")

    # R3 cross-section vs protection
    uf2 = nets(wires, THROUGH)
    prot_of_root = {}
    for t, amps in PROTECT.items():
        r = uf2.find(t)
        prot_of_root[r] = min(prot_of_root.get(r, 999), amps)
    lines += ["## R3 Cross-section vs upstream protection (IEC 60204-1 Table 6, B2)", "",
              "| Wire | Section | Ampacity | Protected by | Result |", "|---|---|---|---|---|"]
    for w in wires:
        s = section(w["spec"])
        if s is None or w["kind"] in ("PE", "Pulse", "Dir", "Sig", "Sig5", "Motor", "Phase"):
            continue
        if is_return(w):
            continue  # N / 0 V / −V conductors carry the current of their L / + conductor (N: rule R6)
        r = uf2.find(w["from"])
        amps = prot_of_root.get(r)
        if amps is None:
            continue
        cap = AMPACITY.get(s, 0)
        if cap >= amps:
            continue
        res = "**FAIL**" if cap < 0.75 * amps else "WARN"
        problems += res == "**FAIL**"
        lines.append(f"| {w['no']} | {s} mm² | {cap} A | {amps} A | {res} |")
    lines.append("")

    # R6 neutral conductors
    lines += ["## R6 Neutral conductors (return current of every branch behind them)", "",
              "| Wire | Section | Ampacity | Branches behind it | Load | Result |", "|---|---|---|---|---|---|"]
    nw = [w for w in wires if is_neutral(w)]
    adj = defaultdict(list)
    for w in nw:
        adj[w["from"]].append((w["to"], w["no"]))
        adj[w["to"]].append((w["from"], w["no"]))
    for x, y in THROUGH + [("QF0.3", "QF0.4"), ("Z1.NIN", "Z1.NOUT")]:  # N paths through parts
        adj[x].append((y, ""))
        adj[y].append((x, ""))
    for w in nw:
        s = section(w["spec"])
        if s is None or w["from"].startswith("Mains.") or w["to"].startswith("Mains."):
            continue  # the supply cable ahead of QF0 is protected upstream
        # The side of this conductor away from the supply (Mains.N) holds the loads it returns.
        best = None
        for start, other in ((w["to"], w["from"]), (w["from"], w["to"])):
            seen, stack = {other}, [start]
            while stack:
                n = stack.pop()
                if n in seen:
                    continue
                seen.add(n)
                stack += [m for m, no in adj[n] if no != w["no"]]
            if "Mains.N" not in seen:
                best = sorted(t for t in seen if t in LOAD_N) or None
                break
        if not best:
            continue
        amps = min(MAIN_CB, sum(LOAD_N[t] for t in best))
        cap = AMPACITY.get(s, 0)
        res = "PASS" if cap >= amps else ("**FAIL**" if cap < 0.75 * amps else "WARN")
        problems += res == "**FAIL**"
        if res != "PASS" or len(best) > 1:
            lines.append(f"| {w['no']} | {s} mm² | {cap} A | {', '.join(best)} | {amps} A | {res} |")
    lines.append("")

    # R4 virtual nodes
    lines += ["## R4 Every endpoint is a real part", ""]
    for t, name in VIRTUAL.items():
        if t in count:
            problems += 1
            lines.append(f"- **FAIL** `{t}` ({name}) has {len(count[t])} conductors but no part in § 2 / § 6 (busbar? terminal?)")
    lines.append("")

    # R5 equivalence
    if len(sys.argv) >= 6:
        ref_path, ref_label = sys.argv[4:6]
        ref = [w for w in json.load(open(ref_path, encoding="utf-8")) if not w.get("notConnected")]
        ref = [dict(w, **{"from": ALIAS.get(w["from"], w["from"]), "to": ALIAS.get(w["to"], w["to"])}) for w in ref]
        board = [("A1.GND", "A1.DCGND"), ("A1.GND", "A1.OGND")]  # one 0 V inside the A11 board
        ufa, ufb = nets(ref, board), nets(aliased, board)
        drop = set(TB_BLOCKS) | set(VIRTUAL)
        terms = sorted(({w["from"] for w in ref} | {w["to"] for w in ref} | {w["from"] for w in aliased} | {w["to"] for w in aliased}) - drop)

        def groups(uf):
            g = defaultdict(set)
            for t in terms:
                g[uf.find(t)].add(t)
            return {t: frozenset(g[uf.find(t)]) for t in terms}

        ga, gb = groups(ufa), groups(ufb)
        diffs = sorted(t for t in terms if ga[t] != gb[t])
        lines += [f"## R5 Electrical equivalence with {ref_label}", ""]
        if not diffs:
            lines.append("- PASS — every terminal sits on exactly the same net.")
        else:
            seen = set()
            for t in diffs:
                if t in seen:
                    continue
                seen |= gb[t]
                lines.append(f"- CHANGED `{t}`: {ref_label} net {{{', '.join(sorted(ga[t]))}}} → now {{{', '.join(sorted(gb[t]))}}}")
        lines.append("")

    lines.insert(3, f"**{problems} problem(s).**")
    open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"{label}: {problems} problem(s) -> {out}")


if __name__ == "__main__":
    main()
