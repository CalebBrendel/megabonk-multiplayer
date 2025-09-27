using MelonLoader;
using HarmonyLib;
using System;
using UnityEngine;

[assembly: MelonInfo(typeof(MegabonkMultiplayer.Entry), "Megabonk Multiplayer", "0.1.0", "Caleb")]
[assembly: MelonGame(null, "Megabonk")] // adjust if needed

namespace MegabonkMultiplayer
{
  public class Entry : MelonMod
  {
    public static bool IsHost = true; // quick toggle for testing
    public override void OnInitializeMelon()
    {
      MelonLogger.Msg("Megabonk Multiplayer loaded");
      HarmonyInstance.PatchAll();
      Net.NetCommon.Init(); // set up sockets/threads
    }

    public override void OnUpdate()
    {
      try
      {
        if (IsHost)
          Net.Host.Tick();
        else
          Net.Client.Tick();
      }
      catch (Exception e) { MelonLogger.Error(e); }
    }
  }

  // Example: patch into player movement update (replace with the real type/method names)
  [HarmonyPatch(typeof(PlayerController), "Update")]
  public static class PlayerController_Update_Patch
  {
    static void Postfix(PlayerController __instance)
    {
      if (__instance == null) return;

      // Grab pose for networking
      Vector3 pos = __instance.transform.position;
      Quaternion rot = __instance.transform.rotation;

      if (Entry.IsHost)
      {
        // Broadcast to clients
        Net.Host.BroadcastPlayerTransform(pos, rot);
      }
      else
      {
        // Client: apply latest from host if remote-controlled
        if (Net.Client.TryGetHostTransform(out var hPos, out var hRot))
        {
          __instance.transform.SetPositionAndRotation(hPos, hRot);
        }
      }
    }
  }
}
