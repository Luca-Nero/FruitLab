using System.Collections.Generic;
using UnityEngine;

namespace FruitLab
{
    internal static class FruitLabHud
    {
        public static readonly Color Normal = new Color(0.92f, 0.94f, 0.95f, 1f);
        public static readonly Color Dim    = new Color(0.70f, 0.73f, 0.75f, 1f);
        public static readonly Color Good   = new Color(0.45f, 0.85f, 0.45f, 1f);
        public static readonly Color Warn   = new Color(0.95f, 0.75f, 0.30f, 1f);
        public static readonly Color Bad    = new Color(0.90f, 0.40f, 0.40f, 1f);
        public static readonly Color Held   = new Color(0.45f, 0.72f, 1f,    1f);
        public static readonly Color Posture = new Color(0.58f, 0.60f, 0.64f, 1f);
        public static readonly Color Rare   = new Color(0.98f, 0.82f, 0.42f, 1f);

        private static readonly Dictionary<int, GUIStyle> _bySize = new Dictionary<int, GUIStyle>();

        public static GUIStyle Text(int size)
        {
            if (_bySize.TryGetValue(size, out var s) && s != null) return s;

            s = new GUIStyle(GUI.skin.label) { fontSize = size };
            _bySize[size] = s;
            return s;
        }

        public static void Reset() => _bySize.Clear();
    }
}
