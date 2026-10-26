using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace A11Sim
{
    /// Wire-by-wire assembly guide for the 3D cabinet: earth first, then 230 V, 48 V, 24 V, signals, motor leads.
    /// Fitted wires stay visible, the current one pulses, the rest are hidden. Progress is saved.
    public class BuildGuide
    {
        public class Item
        {
            public Wire w;
            public float routed;
            public bool door;
            public int group;
        }

        public static readonly string[] Groups =
        {
            "1 · Protective earth", "2 · 230 V power and VFD", "3 · 48 V drive supply", "4 · 24 V control", "5 · Signals", "6 · Motor leads",
        };

        public bool active;
        public int index;
        public readonly List<Item> items = new();
        readonly Dictionary<Wire, int> order = new();
        readonly Dictionary<Terminal, List<Wire>> atTerminal = new();
        const string PrefKey = "a11.build.step";

        public BuildGuide(Circuit c, Cabinet3D cab)
        {
            foreach (var (w, len, door) in cab.RoutedWires())
                items.Add(new Item { w = w, routed = len, door = door, group = GroupOf(w) });
            items.Sort((a, b) => a.group != b.group ? a.group.CompareTo(b.group) : Key(a.w).CompareTo(Key(b.w)));
            for (int i = 0; i < items.Count; i++) order[items[i].w] = i;
            foreach (var w in c.wires)
            {
                if (w.isBus || w.notConnected) continue;
                foreach (var t in new[] { w.a, w.b })
                {
                    if (!atTerminal.TryGetValue(t, out var l)) atTerminal[t] = l = new List<Wire>();
                    l.Add(w);
                }
            }
            index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, items.Count);
        }

        static int GroupOf(Wire w) => w.kind switch
        {
            WireKind.PE => 0,
            WireKind.AC or WireKind.Phase or WireKind.Brake => 1,
            WireKind.DC48 => 2,
            WireKind.DC24 => 3,
            WireKind.Motor => 5,
            _ => 4,
        };

        static string Key(Wire w) => w.number != "" ? int.Parse(w.number).ToString("D5") : "z" + w.a.id;

        public Item Current => index < items.Count ? items[index] : null;
        public bool Done => index >= items.Count;

        /// 0 = not fitted yet (hidden), 1 = fitted, 2 = the one to fit now.
        public int StateOf(Wire w)
        {
            if (!active || w == null || !order.TryGetValue(w, out int i)) return 1;
            return i < index ? 1 : i == index ? 2 : 0;
        }

        public void Next() => Go(index + 1);
        public void Prev() => Go(index - 1);
        public void Restart() => Go(0);

        public void Go(int i)
        {
            index = Mathf.Clamp(i, 0, items.Count);
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();
        }

        public int GroupStart(int g) => items.FindIndex(it => it.group == g);

        // ------------------------------------------------------------------ text

        /// Cut length: routed length + 10 % + 15 cm for stripping and dressing, rounded up to 5 cm.
        public static float Cut(float routed) => Mathf.Ceil((routed * 1.1f + 0.15f) * 20f) / 20f;

        public static string Spec(Wire w)
        {
            if (w.kind == WireKind.Motor) return "LiYY 4×1.0 core";
            if (string.IsNullOrEmpty(w.spec) || w.spec == "—") return w.kind == WireKind.PE ? "cable shield" : "cable core";
            return w.spec;
        }

        static string Section(Wire w)
        {
            if (w.kind == WireKind.Motor) return "1.0";
            var s = w.spec ?? "";
            int i = s.IndexOf(" mm");
            return i > 0 ? s.Substring(0, i) : "";
        }

        static string Num(Wire w) => w.number != "" ? w.number : w.desc;

        /// How this end is terminated: ferrule (single / twin), lug or solder cup.
        public string Termination(Terminal t, Wire w)
        {
            var others = atTerminal.TryGetValue(t, out var l) ? l.Where(o => o != w).ToList() : new List<Wire>();
            bool twin = others.Count == 1;
            string tag = t.dev.tag, sec = Section(w);
            string with = twin ? $" + wire {Num(others[0])}" : "";
            if (tag is "PEBAR" or "BKP" or "DOOR" or "RAIL" or "BODY") return "ring lug M6";
            if (tag == "R1") return "ring lug (resistor stud)";
            if (tag == "Mains") return "supply cable core from the gland";
            if (tag is "G1" or "G2") return twin ? $"2 fork lugs under one screw{with}" : "insulated fork lug";
            if (tag is "X-LIM" or "Y-LIM" or "Z-LIM" or "XT" || tag.StartsWith("XM-")) return "solder cup (GX16 connector)";
            if (w.number == "127") return tag == "X1" ? "360° clamp in the HDC-HA metal hood (no pigtail)" : "360° EMC clamp on the bared braid, bonded to VFD PE";
            if (tag == "X1") return "crimp contact (HDC-HA insert)";
            if (tag is "XL" or "XN") return $"ferrule {sec} mm² (own point on the distribution block)";
            if (string.IsNullOrEmpty(sec)) return w.kind == WireKind.PE ? "shield pigtail, ring lug" : "ferrule";
            return twin ? $"<b>twin ferrule 2 × {sec} mm²</b>{with}" : $"ferrule {sec} mm²";
        }

        public static string Where(Terminal t) => $"<b>{t.dev.tag} {t.name}</b>{Nema(t)}  <color=#8fa3bf>{ShortTitle(t.dev)}</color>";

        // K1 sits on a 14-pin socket marked IEC and NEMA; give the NEMA number too (NEMA 14 is the coil A2, not IEC 14).
        static readonly Dictionary<string, string> K1Nema = new()
        {
            ["A1"] = "13", ["A2"] = "14", ["A2b"] = "14, 2nd A2 screw", ["11"] = "9", ["21"] = "10", ["31"] = "11", ["41"] = "12",
            ["12"] = "1", ["22"] = "2", ["32"] = "3", ["42"] = "4", ["14"] = "5", ["24"] = "6", ["34"] = "7", ["44"] = "8",
        };

        static string Nema(Terminal t) => t.dev.tag == "K1" && K1Nema.TryGetValue(t.name, out var n) ? $" <color=#ffb050>(socket NEMA {n})</color>" : "";

        static string ShortTitle(Device d)
        {
            string s = d.title ?? "";
            int i = s.IndexOf(" — ");
            if (i >= 0) s = s.Substring(i + 3);
            return s.Length > 34 ? s.Substring(0, 33) + "…" : s;
        }

        public static string Metres(float m) => m.ToString("0.00", CultureInfo.InvariantCulture) + " m";
    }
}
