using System.Collections.Generic;
using UnityEngine;

namespace A11Sim
{
    /// Hexagonal prism mesh for nuts and bolt heads.
    public static class Hex
    {
        static Mesh unit;

        /// Hexagonal prism, 1 across flats and 1 high, axis +Y (scale it per nut size).
        public static Mesh Prism()
        {
            if (unit != null) return unit;
            var v = new List<Vector3>();
            var t = new List<int>();
            float r = 0.5f / Mathf.Cos(Mathf.PI / 6);
            Vector3 P(int i, float y) => new(r * Mathf.Cos(i * Mathf.PI / 3), y, r * Mathf.Sin(i * Mathf.PI / 3));
            for (int i = 0; i < 6; i++)
            {
                // Side face (own vertices for flat shading).
                int s = v.Count;
                v.Add(P(i, 0)); v.Add(P(i + 1, 0)); v.Add(P(i + 1, 1)); v.Add(P(i, 1));
                t.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            foreach (float y in new[] { 0f, 1f })
            {
                int c = v.Count;
                v.Add(new Vector3(0, y, 0));
                for (int i = 0; i < 6; i++) v.Add(P(i, y));
                for (int i = 0; i < 6; i++)
                {
                    int a = c + 1 + i, b2 = c + 1 + (i + 1) % 6;
                    if (y > 0) t.AddRange(new[] { c, b2, a });
                    else t.AddRange(new[] { c, a, b2 });
                }
            }
            unit = new Mesh { name = "hex prism" };
            unit.SetVertices(v);
            unit.SetTriangles(t, 0);
            unit.RecalculateNormals();
            unit.RecalculateBounds();
            return unit;
        }
    }
}
