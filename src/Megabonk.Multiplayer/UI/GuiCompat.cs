using UnityEngine;

namespace Megabonk.Multiplayer.UI
{
    /// <summary>
    /// Unity IMGUI compatibility helpers that avoid referencing fields/properties
    /// that can differ across Unity/IL2CPP versions (e.g., GUIContent.none).
    /// </summary>
    internal static class GuiCompat
    {
        private static GUIContent _emptyContent;
        /// <summary>
        /// An "empty" content instance that is safe across Unity versions.
        /// Use this instead of GUIContent.none.
        /// </summary>
        public static GUIContent Empty => _emptyContent ??= new GUIContent(string.Empty);

        private static GUIStyle _styleNone;
        /// <summary>
        /// Minimal style equivalent to GUIStyle.none without relying on engine internals.
        /// </summary>
        public static GUIStyle StyleNone => _styleNone ??= new GUIStyle();
    }
}
