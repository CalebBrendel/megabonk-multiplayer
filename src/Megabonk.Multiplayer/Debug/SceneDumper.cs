using System.Text;
using MelonLoader;
using UnityEngine;

namespace Megabonk.Multiplayer.Debugging
{
    /// <summary>
    /// IL2CPP-safe dumper: avoids stripped reflection APIs.
    /// Dumps only the Camera.main root subtree and any currently bound LocalPlayer info.
    /// </summary>
    public static class SceneDumper
    {
        public static void Dump(int maxDepth = 3)
        {
            // 1) Dump camera path and a small subtree
            var cam = Camera.main;
            if (cam == null)
            {
                MelonLogger.Msg("[DUMP] MainCamera not found.");
            }
            else
            {
                var t = cam.transform;
                MelonLogger.Msg($"[DUMP] MainCamera path: {GetPath(t)}");

                // Climb to camera root and dump a few levels of that subtree
                var root = t;
                while (root.parent != null) root = root.parent;

                MelonLogger.Msg($"[DUMP] Dumping camera root subtree: '{root.name}' (depth {maxDepth})");
                DumpTransform(root, 0, maxDepth);
            }

            // 2) If we have a bound LocalPlayer, log its path + a quick subtree
            var lp = HarmonyPatches.GameHooks.LocalPlayer;
            if (lp != null)
            {
                MelonLogger.Msg($"[DUMP] LocalPlayer bound: '{lp.name}' path: {GetPath(lp)}");
                DumpTransform(lp, 0, 2);
            }
            else
            {
                MelonLogger.Msg("[DUMP] LocalPlayer is not bound yet.");
            }
        }

        static void DumpTransform(Transform t, int depth, int maxDepth)
        {
            if (t == null) return;
            var sb = new StringBuilder();
            for (int i = 0; i < depth; i++) sb.Append("  ");
            var go = t.gameObject;

            // Component names (short)
            var comps = go.GetComponents<Component>();
            sb.Append("- ").Append(go.name).Append(" [");
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                var tn = c.GetType().Name;
                sb.Append(tn);
                if (i < comps.Length - 1) sb.Append(',');
            }
            sb.Append(']');

            // Heuristic flag
            var lower = go.name.ToLowerInvariant();
            if (lower.Contains("player") || lower.Contains("pawn") || lower.Contains("character"))
                sb.Append("  <-- LIKELY PLAYER");

            MelonLogger.Msg(sb.ToString());

            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++)
                DumpTransform(t.GetChild(i), depth + 1, maxDepth);
        }

        static string GetPath(Transform t)
        {
            var sb = new StringBuilder();
            var stack = new System.Collections.Generic.List<Transform>();
            for (var x = t; x != null; x = x.parent) stack.Add(x);
            for (int i = stack.Count - 1; i >= 0; i--) sb.Append('/').Append(stack[i].name);
            return sb.ToString();
        }
    }
}
