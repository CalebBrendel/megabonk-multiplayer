using HarmonyLib;
using MelonLoader;

namespace MegabonkMultiplayer.Patches
{
  // Replace with actual RunManager / GameState types later
  [HarmonyPatch]
  public static class Lifecycle_Patch
  {
    // [HarmonyTargetMethods] can locate overloads via reflection once you know names
    // For now, this just shows how you’d reset state between runs.
    static void OnRunStart()
    {
      MelonLogger.Msg("Run start -> resetting net state");
      Net.NetCommon.ResetForRun(); // add this helper if you like
    }

    static void OnRunEnd()
    {
      MelonLogger.Msg("Run end -> flushing/closing peers");
    }
  }
}
