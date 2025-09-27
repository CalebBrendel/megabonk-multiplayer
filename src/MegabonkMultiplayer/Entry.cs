using MelonLoader;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(MegabonkMultiplayer.Entry), "Megabonk Multiplayer", "0.1.0", "Caleb")]
[assembly: MelonGame(null, "Megabonk")]

namespace MegabonkMultiplayer
{
  public class Entry : MelonMod
  {
    public static bool IsHost = true; // temporary toggle

    public override void OnInitializeMelon()
    {
      MelonLogger.Msg("Megabonk Multiplayer loaded");
      Net.NetCommon.Init();                 // sockets
      HarmonyInstance.PatchAll(typeof(PlayerController_Update_Patch)); // only our safe patch
    }

    public override void OnUpdate()
    {
      try
      {
        if (IsHost) Net.Host.Tick();
        else Net.Client.Tick();
      }
      catch (Exception e) { MelonLogger.Error(e); }
    }
  }

  // SAFE patch that compiles before IL2CPP dumps:
  // - Finds type by name at runtime
  // - Patches its "Update" method
  [HarmonyPatch]
  internal static class PlayerController_Update_Patch
  {
    // TODO: replace "PlayerController" with the exact IL2CPP full name once we dump types,
    // e.g., "Game.Player.PlayerController"
    private const string PlayerControllerTypeName = "PlayerController";
    private const string UpdateMethodName = "Update";

    static MethodBase TargetMethod()
    {
      // Try common namespaces too if needed later:
      // var t = AccessTools.TypeByName("Game.Player.PlayerController") ?? AccessTools.TypeByName(PlayerControllerTypeName);
      var t = AccessTools.TypeByName(PlayerControllerTypeName);
      if (t == null)
      {
        MelonLogger.Warning($"Type not found: {PlayerControllerTypeName}. The multiplayer patch is idle.");
        return null;
      }
      var m = AccessTools.Method(t, UpdateMethodName);
      if (m == null) MelonLogger.Warning($"Method not found: {t.FullName}.{UpdateMethodName}()");
      return m;
    }

    static void Postfix(object __instance)
    {
      if (__instance == null) return;

      // Works for IL2CPP components too:
      var comp = __instance as Component;
      var tr = comp != null ? comp.transform : null;
      if (tr == null) return;

      var pos = tr.position;
      var rot = tr.rotation;

      if (Entry.IsHost)
      {
        Net.Host.BroadcastPlayerTransform(pos, rot);
      }
      else
      {
        if (Net.Client.TryGetHostTransform(out var hPos, out var hRot))
          tr.SetPositionAndRotation(hPos, hRot);
      }
    }
  }
}
