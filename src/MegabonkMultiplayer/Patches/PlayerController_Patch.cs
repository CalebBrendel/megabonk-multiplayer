using HarmonyLib;
using UnityEngine;

namespace MegabonkMultiplayer.Patches
{
  // This runs after Unity's Update on any GameObject tagged "Player" (placeholder)
  [HarmonyPatch(typeof(GameObject), "Update")] // WARNING: placeholder; change to real method once known
  public static class PlayerController_Patch
  {
    static void Postfix(GameObject __instance)
    {
      if (__instance == null) return;
      if (__instance.tag != "Player") return; // temporary filter

      var t = __instance.transform;
      if (MegabonkMultiplayer.Entry.IsHost)
        Net.Host.BroadcastPlayerTransform(t.position, t.rotation);
      else if (Net.Client.TryGetHostTransform(out var pos, out var rot))
        t.SetPositionAndRotation(pos, rot);
    }
  }
}
