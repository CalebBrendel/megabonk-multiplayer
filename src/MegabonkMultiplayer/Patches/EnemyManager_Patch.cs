using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace MegabonkMultiplayer.Patches
{
  // TODO: replace with actual type (e.g., Game.Enemies.EnemyManager)
  internal static class EnemyManager_Patch
  {
    // --- SPAWN HOOK ----------------------------------------------------------
    // [HarmonyPatch(typeof(EnemyManager), "SpawnEnemy")]
    // static class SpawnEnemy_Patch
    // {
    //   static bool Prefix(object __instance, /* actual args: EnemyType type, Vector3 pos, ... */)
    //   {
    //     if (!MegabonkMultiplayer.Entry.IsHost)
    //     {
    //       // Clients do not spawn; they wait for host’s spawn message
    //       return false;
    //     }
    //     // Host: let the original method run AND also broadcast spawn to clients
    //     // Net.Host.BroadcastSpawn(...);
    //     return true;
    //   }
    // }

    // --- DESPAWN / DEATH HOOK ------------------------------------------------
    // [HarmonyPatch(typeof(Enemy), "Die")]
    // static class Enemy_Die_Patch
    // {
    //   static void Postfix(Enemy __instance)
    //   {
    //     if (MegabonkMultiplayer.Entry.IsHost)
    //     {
    //       // Host informs clients to remove this entity id
    //       // Net.Host.BroadcastDespawn(__instance.Id);
    //     }
    //   }
    // }

    // --- DAMAGE / HP HOOK ----------------------------------------------------
    // [HarmonyPatch(typeof(Enemy), "ApplyDamage")]
    // static class Enemy_ApplyDamage_Patch
    // {
    //   static bool Prefix(Enemy __instance, ref float dmg, /* other args */)
    //   {
    //     if (!MegabonkMultiplayer.Entry.IsHost)
    //     {
    //       // Client: don’t apply real damage; host is authoritative
    //       return false;
    //     }
    //     // Host: allow, then broadcast resulting HP delta in Postfix
    //     return true;
    //   }

    //   static void Postfix(Enemy __instance /*, args */)
    //   {
    //     if (MegabonkMultiplayer.Entry.IsHost)
    //     {
    //       // Net.Host.BroadcastEnemyHp(__instance.Id, __instance.CurrentHp);
    //     }
    //   }
    // }

    // --- TEMP “FINDER” (OPTIONAL): helps while you don’t know exact names ----
    [HarmonyPatch(typeof(Object), "Instantiate", typeof(Object), typeof(Vector3), typeof(Quaternion))]
    static class Temp_Instantiate_Patch
    {
      // This fires for all instantiates; filter by component name to spot enemies.
      static void Postfix(Object __result)
      {
        if (__result is GameObject go)
        {
          if (LooksLikeEnemy(go))
          {
            // Debug: see spawns fly by; later replace with real SpawnEnemy hook
            MelonLogger.Msg($"Enemy-ish spawn: {go.name}");
            if (MegabonkMultiplayer.Entry.IsHost)
            {
              // Net.Host.BroadcastSpawn(guessType, go.transform.position, seedOrGuid);
            }
          }
        }
      }

      static bool LooksLikeEnemy(GameObject go)
      {
        // Heuristic until we know actual component types/names
        var name = go.name.ToLowerInvariant();
        if (name.Contains("enemy") || name.Contains("mob") || name.Contains("boss"))
          return true;

        // Or: check for a likely “Health” / “AI” component by name
        foreach (var c in go.GetComponents<Component>())
        {
          var t = c.GetType().Name.ToLowerInvariant();
          if (t.Contains("enemy") || t.Contains("ai") || t.Contains("health"))
            return true;
        }
        return false;
      }
    }
  }
}
