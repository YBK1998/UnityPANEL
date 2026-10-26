using System;
using System.Collections.Generic;
using UnityEngine;

namespace A11Sim
{
    /// What a net carries. A net can carry several flags (for example 0 V and PE after the PELV bond).
    [Flags]
    public enum Pot
    {
        None = 0,
        L = 1 << 0,     // 230 V line
        N = 1 << 1,     // neutral
        P24 = 1 << 2,   // +24 V DC (G2)
        Z0 = 1 << 3,    // 0 V of G2 (PELV, bonded to PE)
        P48 = 1 << 4,   // +48 V DC (G1)
        Z48 = 1 << 5,   // 48 V return
        P5 = 1 << 6,    // A11 axis 5 V
        PH = 1 << 7,    // VFD output phase
        DCB = 1 << 8,   // VFD DC bus (P+)
        BRK = 1 << 9,   // braking current in R1
        PE = 1 << 10,   // protective earth
        MOT = 1 << 11,  // stepper phase current
        ACT = 1 << 12,  // active logic signal (pulse / direction)
    }

    public enum WireKind { AC, Phase, Brake, PE, DC48, DC24, Sig, Sig5, Pulse, Dir, Motor }

    public class Terminal
    {
        public int index;
        public string id;
        public string name;
        public Vector2 pos;
        public Device dev;
        public bool hidden;
    }

    public class Wire
    {
        public string number = "";
        public Terminal a, b;
        public Vector2[] pts;
        public WireKind kind;
        public string desc = "", spec = "";
        public bool notConnected;
        public bool isBus;
        public bool stub; // drawn only near its "from" end (PE stubs labelled "→ PE bar")
        public bool noLabel;
        public Vector2? labelAt;
        public string labelText;
        public Pot pot;
        public Func<float> speedOf; // flow speed hint (spindle frequency, axis speed)
    }

    public class Hotspot
    {
        public Device dev;
        public Rect rect;
        public Action onDown;
        public Action onUp;
        public Func<string> tip;
    }

    public class CautionMark
    {
        public string id;
        public Vector2 pos;
        public string text;
    }

    public abstract class Device
    {
        public Circuit circuit;
        public string tag = "";
        public string title = "";
        public string info = "";
        protected Circuit C => circuit;

        protected Terminal Term(string name, float x, float y, bool hidden = false) =>
            circuit.AddTerminal(this, name, new Vector2(x, y), hidden);

        public abstract void Build();
        public virtual void Links() { }
        public virtual void Sources() { }
        /// Read the solved nets. Return true if a state that changes Links/Sources has changed.
        public virtual bool Evaluate() => false;
        public virtual void Tick(float dt) { }
        public virtual void DrawStatic(Gfx g, Labels lb) { }
        public virtual void DrawDynamic(Gfx g, Gfx glow, float time) { }
        public virtual IEnumerable<Hotspot> Hotspots() { yield break; }
        public virtual string Status() => "";

        public virtual string Tip()
        {
            string s = $"<b>{title}</b>";
            if (!string.IsNullOrEmpty(info)) s += "\n" + info;
            string st = Status();
            if (!string.IsNullOrEmpty(st)) s += "\n<color=#9fe8ff>State: " + st + "</color>";
            return s;
        }

        protected Hotspot Spot(Rect r, Action down = null, Action up = null) =>
            new() { dev = this, rect = r, onDown = down, onUp = up, tip = Tip };
    }

    public class Circuit
    {
        public readonly List<Terminal> terminals = new();
        public readonly Dictionary<string, Terminal> byId = new();
        public readonly List<Device> devices = new();
        public readonly List<Wire> wires = new();
        public readonly List<CautionMark> cautions = new();
        public readonly List<(string text, Vector2 pos, float size)> titles = new();
        readonly List<(Terminal, Terminal)> fixedLinks = new();
        public Machine machine;
        public Rect bounds;

        int[] parent;
        Pot[] pot;

        public Terminal AddTerminal(Device d, string name, Vector2 pos, bool hidden)
        {
            var t = new Terminal { index = terminals.Count, id = d.tag + "." + name, name = name, pos = pos, dev = d, hidden = hidden };
            terminals.Add(t);
            byId.Add(t.id, t);
            return t;
        }

        public Terminal T(string id) =>
            byId.TryGetValue(id, out var t) ? t : throw new Exception("Unknown terminal " + id);

        public TDev Add<TDev>(TDev d) where TDev : Device
        {
            d.circuit = this;
            devices.Add(d);
            d.Build();
            return d;
        }

        public TDev Get<TDev>(string tag) where TDev : Device
        {
            foreach (var d in devices)
                if (d.tag == tag && d is TDev t) return t;
            throw new Exception("Unknown device " + tag);
        }

        public void Fixed(Terminal a, Terminal b) => fixedLinks.Add((a, b));
        public IReadOnlyList<(Terminal, Terminal)> FixedLinks => fixedLinks;

        // ---- used by devices during Solve ----
        public void Link(Terminal a, Terminal b) => Union(a.index, b.index);
        public void Source(Terminal t, Pot p) => pot[Find(t.index)] |= p;
        public bool Has(Terminal t, Pot p) => (pot[Find(t.index)] & p) != 0;
        public Pot PotOf(Terminal t) => pot[Find(t.index)];
        public bool Same(Terminal a, Terminal b) => Find(a.index) == Find(b.index);
        public int NetOf(Terminal t) => Find(t.index);

        /// Union-find over wires, fixed links and closed contacts, then sources.
        /// Device states (coils, PSU outputs) feed back into the links, so iterate until stable.
        public void Solve()
        {
            int n = terminals.Count;
            if (parent == null || parent.Length != n)
            {
                parent = new int[n];
                pot = new Pot[n];
            }
            for (int iter = 0; iter < 16; iter++)
            {
                for (int i = 0; i < n; i++)
                {
                    parent[i] = i;
                    pot[i] = Pot.None;
                }
                foreach (var w in wires)
                    if (!w.notConnected) Union(w.a.index, w.b.index);
                foreach (var (a, b) in fixedLinks) Union(a.index, b.index);
                foreach (var d in devices) d.Links();
                foreach (var d in devices) d.Sources();
                bool changed = false;
                foreach (var d in devices) changed |= d.Evaluate();
                if (!changed) break;
            }
            foreach (var w in wires) w.pot = w.notConnected ? Pot.None : pot[Find(w.a.index)];
        }

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b) parent[a] = b;
        }

        // ---- layout helpers ----
        static Vector2[] P(float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        public Wire W(string num, string from, string to, WireKind kind, string desc, string spec, params float[] xy)
        {
            var w = new Wire { number = num, a = T(from), b = T(to), kind = kind, desc = desc, spec = spec, pts = P(xy) };
            wires.Add(w);
            return w;
        }

        public Wire Bus(string node, WireKind kind, params float[] xy)
        {
            var t = T(node);
            var w = new Wire { a = t, b = t, kind = kind, pts = P(xy), isBus = true, noLabel = true };
            wires.Add(w);
            return w;
        }

        /// Unnumbered conductor (motor cores, cords).
        public Wire Core(string from, string to, WireKind kind, string desc, params float[] xy)
        {
            var w = W("", from, to, kind, desc, "", xy);
            w.noLabel = true;
            return w;
        }

        public void Caution(string id, float x, float y, string text) =>
            cautions.Add(new CautionMark { id = id, pos = new Vector2(x, y), text = text });
    }
}
