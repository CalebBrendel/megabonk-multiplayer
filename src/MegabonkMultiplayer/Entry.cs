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
    public static bool IsHost = true; // toggle temporarily

    public override void OnInitializeMelon()
    {
      MelonLogger.Msg("Megabonk Multiplayer loaded");
      Net.NetCommon.Init();
      // Only patch our safe class so we don’t accidentally scan all types
      HarmonyInstance.PatchAll(typeof(PlayerController_Update_Patch));
    }

    public override void OnUpdate()
    {
      try
      {
        if (IsHost) Net.Host.Tick();
        else Net.Client.Tick();

        // optional role toggle while testing
        if (Input.GetKeyDown(KeyCode.F9))
        {
          IsHost = !IsHost;
          MelonLogger.Msg($"Role toggled: {(IsHost ? "HOST" : "CLIENT")}");
        }
      }
      catch (Exception e) { MelonLogger.Error(e); }
    }
  }

  [HarmonyPatch]
  internal static class PlayerController_Update_Patch
  {
    // Replace this later with the fully-qualified IL2CPP type name (e.g., "Game.Player.PlayerController")
    private const string PlayerControllerTypeName = "PlayerController";
    private const string UpdateMethodName = "Update";

    static MethodBase TargetMethod()
    {
      var t = AccessTools.TypeByName(PlayerControllerTypeName);
      if (t == null)
      {
        MelonLogger.Warning($"Type not found: {PlayerControllerTypeName}. Multiplayer patch is idle.");
        return null;
      }
      var m = AccessTools.Method(t, UpdateMethodName);
      if (m == null) MelonLogger.Warning($"Method not found: {t.FullName}.{UpdateMethodName}()");
      return m;
    }

    static void Postfix(object __instance)
    {
      if (__instance is not Component comp) return;
      var tr = comp.transform;
      if (tr == null) return;

      if (Entry.IsHost)
      {
        Net.Host.BroadcastPlayerTransform(tr.position, tr.rotation);
      }
      else if (Net.Client.TryGetHostTransform(out var pos, out var rot))
      {
        tr.SetPositionAndRotation(pos, rot);
      }
    }
  }
}
