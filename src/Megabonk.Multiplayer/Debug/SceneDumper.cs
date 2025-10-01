using System.Text;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megabonk.Multiplayer.Debugging
{
    public static class SceneDumper
    {
        public static void Dump(int maxDepth = 3)
        {
            var scn = SceneManager.GetActiveScene();
            var roots = GetSceneRootsSafe(scn);
            MelonLogger.Msg($"[DUMP] Scene '{scn.name}' roots={roots.Length}");

            for (int i = 0; i < roots.Length; i++)
                DumpTransform(roots[i].transform, 0, maxDepth);

            LogCameraChain();
        }

        static void DumpTransform(Transform t, int depth, int maxDepth)
        {
            if (t == null) return;
            var sb = new StringBuilder();
            for (int i = 0; i < depth; i++) sb.Append("  ");
            var go = t.gameObject;

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

            var lower = go.name.ToLowerInvariant();
            if (lower.Contains("player") || lower.Contains("pawn") || lower.Contains("character"))
                sb.Append("  <-- LIKELY PLAYER");

            MelonLogger.Msg(sb.ToString());

            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++)
                DumpTransform(t.GetChild(i), depth + 1, maxDepth);
        }

        static void LogCameraChain()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                MelonLogger.Msg("[DUMP] MainCamera not found.");
                return;
            }

            var t = cam.transform;
            MelonLogger.Msg($"[DUMP] MainCamera path: {GetPath(t)}");
            var p = t.parent;
            int i = 0;
            while (p != null && i < 4)
            {
                MelonLogger.Msg($"[DUMP] parent[{i}]: {p.name}");
                p = p.parent;
                i++;
            }
        }

        static string GetPath(Transform t)
        {
            var sb = new StringBuilder();
            var stack = new System.Collections.Generic.List<Transform>();
            for (var x = t; x != null; x = x.parent) stack.Add(x);
            for (int i = stack.Count - 1; i >= 0; i--) sb.Append('/').Append(stack[i].name);
            return sb.ToString();
        }

        // IL2CPP-safe roots using Resources.FindObjectsOfTypeAll(typeof(Transform))
        static GameObject[] GetSceneRootsSafe(Scene scn)
        {
            try
            {
                var objs = Resources.FindObjectsOfTypeAll(typeof(Transform));
                int count = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    var t = objs[i] as Transform;
                    if (t == null) continue;
                    var go = t.gameObject;
                    if (t.parent == null &&
                        go.scene.handle == scn.handle &&
                        (go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.HideAndDontSave)) == 0)
                        count++;
                }
                if (count == 0) return System.Array.Empty<GameObject>();

                var result = new GameObject[count];
                int idx = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    var t = objs[i] as Transform;
                    if (t == null) continue;
                    var go = t.gameObject;
                    if (t.parent == null &&
                        go.scene.handle == scn.handle &&
                        (go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.HideAndDontSave)) == 0)
                        result[idx++] = go;
                }
                return result;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[DUMP] GetSceneRootsSafe failed: {ex.GetType().Name}: {ex.Message}");
                return System.Array.Empty<GameObject>();
            }
        }
    }
}
