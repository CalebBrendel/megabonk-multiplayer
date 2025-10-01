using HarmonyLib;
using UnityEngine;
using Steamworks;


namespace Megabonk.Multiplayer.HarmonyPatches
{
public static class GameHooks
{
public static Vector3 LastPos; public static Quaternion LastRot;


public static bool TryGetLocalPlayerPos(out Quaternion rot)
{
// TODO: Replace with real game class lookups
var player = GameObject.FindWithTag("Player");
if (player != null)
{
LastPos = player.transform.position;
LastRot = player.transform.rotation;
rot = LastRot; return true;
}
rot = Quaternion.identity; return false;
}


public static void ApplyRemotePlayerState(CSteamID who, Vector3 pos, Quaternion rot)
{
var tag = "RemotePlayer_" + who.m_SteamID;
var avatar = GameObject.Find(tag);
if (avatar == null)
{
avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
avatar.name = tag;
}
avatar.transform.position = pos;
avatar.transform.rotation = rot;
}


// Example patch stubs — fill these once you inspect Megabonk with a decompiler
//[HarmonyPatch(typeof(GameManager), nameof(GameManager.OnSceneLoaded))]
//[HarmonyPostfix]
public static void OnSceneLoaded_Postfix()
{
if (Megabonk.Multiplayer.MegabonkMultiplayer.IsHost)
{
// TODO: capture seed/map info and broadcast via NetHost
}
}
}
}
