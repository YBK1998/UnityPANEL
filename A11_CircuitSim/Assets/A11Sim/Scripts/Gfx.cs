using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace A11Sim
{
    public static class Pal
    {
        public static readonly Color Bg = new(0.030f, 0.038f, 0.052f);
        public static readonly Color Sym = new(0.70f, 0.74f, 0.80f);
        public static readonly Color SymDim = new(0.40f, 0.44f, 0.50f);
        public static readonly Color Box = new(0.55f, 0.60f, 0.68f);
        public static readonly Color WireOff = new(0.29f, 0.32f, 0.37f);
        public static readonly Color L = new(1.00f, 0.45f, 0.08f);
        public static readonly Color N = new(0.25f, 0.55f, 1.00f);
        public static readonly Color Phase = new(0.78f, 0.35f, 1.00f);
        public static readonly Color DCB = new(1.00f, 0.72f, 0.20f);
        public static readonly Color Brake = new(1.00f, 0.20f, 0.05f);
        public static readonly Color PE = new(0.55f, 0.72f, 0.18f);
        public static readonly Color P48 = new(1.00f, 0.86f, 0.10f);
        public static readonly Color P24 = new(1.00f, 0.20f, 0.32f);
        public static readonly Color Ret = new(0.42f, 0.52f, 0.78f);
        public static readonly Color Sig = new(0.15f, 1.00f, 0.50f);
        public static readonly Color P5 = new(0.70f, 1.00f, 0.15f);
        public static readonly Color Mot = new(1.00f, 0.35f, 0.75f);
        public static readonly Color WireNo = new(0.42f, 0.62f, 1.00f);
        public static readonly Color Text = new(0.84f, 0.87f, 0.92f);
        public static readonly Color TextDim = new(0.55f, 0.60f, 0.67f);
        public static readonly Color Title = new(0.95f, 0.96f, 1.00f);
        public static readonly Color Caution = new(1.00f, 0.55f, 0.10f);
        public static readonly Color LampYellow = new(1.00f, 0.85f, 0.15f);
        public static readonly Color LampGreen = new(0.20f, 1.00f, 0.35f);
        public static readonly Color LampRed = new(1.00f, 0.15f, 0.12f);
    }

    /// Collects thick line segments and bakes them into a mesh for the A11/Line shaders.
    public class Gfx
    {
        struct Seg
        {
            public Vector2 a, b;
            public float w, arc;
            public Color c;
        }

        readonly List<Seg> segs = new();
        public Color color = Pal.Sym;
        public float width = 0.16f;

        public int Count => segs.Count;
        public void Clear() => segs.Clear();

        public void Line(Vector2 a, Vector2 b) => Line(a, b, width, color);
        public void Line(Vector2 a, Vector2 b, float w, Color c, float arc = 0) =>
            segs.Add(new Seg { a = a, b = b, w = w, c = c, arc = arc });

        public void Poly(IList<Vector2> p, bool closed = false)
        {
            float arc = 0;
            int n = p.Count;
            int count = closed ? n : n - 1;
            for (int i = 0; i < count; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % n];
                Line(a, b, width, color, arc);
                arc += Vector2.Distance(a, b);
            }
        }

        public void Rect(Vector2 min, Vector2 max)
        {
            Vector2 b = new(max.x, min.y), d = new(min.x, max.y);
            float w = max.x - min.x, h = max.y - min.y;
            Line(min, b, width, color, 0);
            Line(b, max, width, color, w);
            Line(max, d, width, color, w + h);
            Line(d, min, width, color, 2 * w + h);
        }

        public void DashedRect(Vector2 min, Vector2 max, float dash = 0.7f, float gap = 0.45f)
        {
            Dashed(min, new Vector2(max.x, min.y), dash, gap);
            Dashed(new Vector2(max.x, min.y), max, dash, gap);
            Dashed(max, new Vector2(min.x, max.y), dash, gap);
            Dashed(new Vector2(min.x, max.y), min, dash, gap);
        }

        public void Circle(Vector2 c, float r, int n = 28) => Arc(c, r, 0, 360, n);

        public void Arc(Vector2 c, float r, float a0, float a1, int n = 16)
        {
            Vector2 prev = c + Dir(a0) * r;
            for (int i = 1; i <= n; i++)
            {
                var p = c + Dir(Mathf.Lerp(a0, a1, i / (float)n)) * r;
                Line(prev, p);
                prev = p;
            }
        }

        public void Dashed(Vector2 a, Vector2 b, float dash = 0.7f, float gap = 0.45f)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1e-4f) return;
            Vector2 d = (b - a) / len;
            for (float s = 0; s < len; s += dash + gap)
                Line(a + d * s, a + d * Mathf.Min(s + dash, len));
        }

        /// Filled disc: a zero-length segment with round caps.
        public void Disc(Vector2 c, float r) => Line(c, c, r * 2, color);
        public void Disc(Vector2 c, float r, Color col) => Line(c, c, r * 2, col);

        public static Vector2 Dir(float deg) => new(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        // Reused vertex buffers: the dynamic overlay is rebuilt every frame without allocating.
        readonly List<Vector3> v = new();
        readonly List<Vector4> uv0 = new(), uv1 = new();
        readonly List<Color> col = new();
        readonly List<int> idx = new();

        public Mesh Build(Mesh mesh = null)
        {
            mesh ??= new Mesh();
            mesh.Clear();
            int n = segs.Count;
            v.Clear();
            uv0.Clear();
            uv1.Clear();
            col.Clear();
            idx.Clear();
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < n; i++)
            {
                var s = segs[i];
                Vector2 d = s.b - s.a;
                float len = d.magnitude;
                Vector2 dir = len > 1e-5f ? d / len : Vector2.right;
                for (int k = 0; k < 4; k++)
                {
                    float endSign = k < 2 ? -1 : 1;
                    float side = k == 0 || k == 3 ? -1 : 1;
                    v.Add(endSign < 0 ? s.a : s.b);
                    uv0.Add(new Vector4(s.arc + (endSign < 0 ? 0 : len), s.w, side, endSign));
                    uv1.Add(new Vector4(dir.x, dir.y, len, 0));
                    col.Add(s.c);
                }
                int b = i * 4;
                idx.Add(b);
                idx.Add(b + 1);
                idx.Add(b + 2);
                idx.Add(b);
                idx.Add(b + 2);
                idx.Add(b + 3);
                min = Vector2.Min(min, Vector2.Min(s.a, s.b));
                max = Vector2.Max(max, Vector2.Max(s.a, s.b));
            }
            mesh.indexFormat = n * 4 > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(col);
            mesh.SetTriangles(idx, 0, false);
            if (n > 0)
            {
                // Vertices are expanded on the GPU, so pad the bounds generously.
                var size = (Vector3)(max - min) + new Vector3(60, 60, 1);
                mesh.bounds = new Bounds((Vector3)((min + max) * 0.5f), size);
            }
            return mesh;
        }
    }

    /// Text requests collected while the schematic is built; SchematicView turns them into TextMeshPro objects.
    public class Labels
    {
        public class Item
        {
            public string text;
            public Func<string> dynamic;
            public Func<Color> dynColor;
            public Vector2 pos;
            public float size;
            public Color color;
            public TextAlignmentOptions align;
            public float rot;
            public bool bold;
        }

        public readonly List<Item> items = new();

        public Item Add(string text, float x, float y, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float rot = 0, bool bold = false)
        {
            var it = new Item { text = text, pos = new Vector2(x, y), size = size, color = color, align = align, rot = rot, bold = bold };
            items.Add(it);
            return it;
        }

        public Item Dyn(Func<string> text, float x, float y, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var it = Add("", x, y, size, color, align, 0, bold);
            it.dynamic = text;
            return it;
        }
    }

    public enum ContactStyle { Plain, Breaker }

    /// IEC 60617-style symbol helpers.
    public static class Sym
    {
        public const float TermR = 0.42f;

        public static void Terminal(Gfx g, Vector2 p) => g.Circle(p, TermR, 14);

        /// Contact between a (fixed side) and b (pivot side). Returns the blade midpoint.
        public static Vector2 Contact(Gfx g, Vector2 a, Vector2 b, bool closed, ContactStyle style = ContactStyle.Plain)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 u = d / len;
            Vector2 v = new(-u.y, u.x);
            float lead = len * 0.27f;
            Vector2 fixedPt = a + u * lead;
            Vector2 pivot = b - u * lead;
            float blade = len - 2 * lead;
            g.Line(a + u * Sym.TermR, fixedPt);
            g.Line(b - u * Sym.TermR, pivot);
            g.Line(fixedPt, fixedPt + v * 0.7f);
            Vector2 tip;
            if (closed)
                tip = fixedPt + v * 0.45f;
            else
            {
                float ang = 30f * Mathf.Deg2Rad;
                tip = pivot + (-u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * blade * 1.02f;
            }
            g.Line(pivot, tip);
            if (style == ContactStyle.Breaker)
            {
                float s = 0.45f;
                g.Line(fixedPt + (u + v) * s * 0.7f, fixedPt - (u + v) * s * 0.7f);
                g.Line(fixedPt + (u - v) * s * 0.7f, fixedPt - (u - v) * s * 0.7f);
            }
            return (pivot + tip) * 0.5f;
        }

        /// Push-button actuator: dashed rod from the blade, plus the "E[" style head.
        public static void PushActuator(Gfx g, Vector2 bladeMid, Vector2 outward, bool pressed)
        {
            Vector2 head = bladeMid + outward * (pressed ? 2.4f : 3.0f);
            g.Dashed(bladeMid, head, 0.45f, 0.3f);
            Vector2 side = new(-outward.y, outward.x);
            g.Line(head + side * 0.9f, head - side * 0.9f);
            g.Line(head + side * 0.9f, head + side * 0.9f + outward * 0.6f);
            g.Line(head - side * 0.9f, head - side * 0.9f + outward * 0.6f);
        }

        /// E-stop mushroom head.
        public static void Mushroom(Gfx g, Vector2 bladeMid, Vector2 outward, bool pressed)
        {
            Vector2 head = bladeMid + outward * (pressed ? 2.3f : 3.0f);
            g.Dashed(bladeMid, head, 0.45f, 0.3f);
            float baseAng = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
            g.Arc(head + outward * 1.1f, 1.1f, baseAng + 90, baseAng + 270, 14);
            Vector2 side = new(-outward.y, outward.x);
            g.Line(head + outward * 1.1f + side * 1.1f, head + outward * 1.1f - side * 1.1f);
        }

        public static void CoilV(Gfx g, Vector2 top, Vector2 bottom, float w = 3.6f)
        {
            float h = (top.y - bottom.y);
            float bh = Mathf.Min(h * 0.5f, 4f);
            float cy = (top.y + bottom.y) * 0.5f;
            g.Line(top - new Vector2(0, TermR), new Vector2(top.x, cy + bh / 2));
            g.Line(bottom + new Vector2(0, TermR), new Vector2(top.x, cy - bh / 2));
            g.Rect(new Vector2(top.x - w / 2, cy - bh / 2), new Vector2(top.x + w / 2, cy + bh / 2));
        }

        public static void LampSymbol(Gfx g, Vector2 c, float r)
        {
            g.Circle(c, r, 24);
            float k = r * 0.7071f;
            g.Line(c + new Vector2(-k, -k), c + new Vector2(k, k));
            g.Line(c + new Vector2(-k, k), c + new Vector2(k, -k));
        }

        public static void Ground(Gfx g, Vector2 p)
        {
            g.Line(p + new Vector2(-1.2f, 0), p + new Vector2(1.2f, 0));
            g.Line(p + new Vector2(-0.8f, -0.5f), p + new Vector2(0.8f, -0.5f));
            g.Line(p + new Vector2(-0.4f, -1.0f), p + new Vector2(0.4f, -1.0f));
        }

        public static void CautionTriangle(Gfx g, Vector2 c, float s, Color col)
        {
            var old = g.color;
            g.color = col;
            Vector2 a = c + new Vector2(0, s * 0.62f), b = c + new Vector2(-s * 0.6f, -s * 0.42f), d = c + new Vector2(s * 0.6f, -s * 0.42f);
            g.Poly(new[] { a, b, d }, true);
            g.Line(c + new Vector2(0, s * 0.25f), c + new Vector2(0, -s * 0.08f));
            g.Disc(c + new Vector2(0, -s * 0.25f), 0.09f);
            g.color = old;
        }

        public static void Rotor(Gfx g, Vector2 c, float r, float angleDeg, int blades = 2)
        {
            for (int i = 0; i < blades; i++)
            {
                var d = Gfx.Dir(angleDeg + i * 360f / blades) * r;
                g.Line(c, c + d);
            }
        }
    }
}
