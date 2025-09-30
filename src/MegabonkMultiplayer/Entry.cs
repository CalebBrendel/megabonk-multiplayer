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
    // Change these once we learn the real controller + method
    private const string PlayerControllerTypeName = "PlayerController"; // e.g. "Game.Player.PlayerController"
    private const string PlayerTickMethodName     = "Update";           // e.g. "FixedUpdate" or "Tick"

    private Harmony _harmony;
    private bool _patchTried, _patchOK;

    // Simple UI state
    private bool _showUI = true;
    private string _ip = "127.0.0.1";
    private int _port = 27015;

    public static bool IsHost = false;

    public override void OnInitializeMelon()
    {
      MelonLogger.Msg("Megabonk Multiplayer loaded");
      _harmony = new Harmony("Megabonk_Multiplayer");

      // Init networking core (but don't start host/client yet)
      Net.NetCommon.Init();

      // Defer patch until Il2CPP interop & game assemblies are live.
      MelonCoroutines.Start(DeferredPatch());
    }

    private System.Collections.IEnumerator DeferredPatch()
    {
      // Give the Il2CppInterop a few frames to settle
      for (int i = 0; i < 120; i++) yield return null;
      TryPatch();
    }

    private void TryPatch()
    {
      if (_patchTried) return;
      _patchTried = true;

      try
      {
        // Use Harmony AccessTools with string: "Namespace.Type:Method"
        // No compile-time type needed.
        string qualified = string.IsNullOrEmpty(PlayerControllerTypeName)
          ? null
          : $"{PlayerControllerTypeName}:{PlayerTickMethodName}";

        if (string.IsNullOrEmpty(qualified))
        {
          MelonLogger.Warning("No target specified for patch.");
          return;
        }

        var target = AccessTools.Method(qualified);
        if (target == null)
        {
          MelonLogger.Warning($"Patch target not found: {qualified}. The mod will run idle. " +
                              "Once we know the real type/method, set PlayerControllerTypeName & PlayerTickMethodName.");
          return;
        }

        var postfix = new HarmonyMethod(typeof(Entry).GetMethod(nameof(PlayerTick_Postfix),
                          BindingFlags.Static | BindingFlags.NonPublic));

        _harmony.Patch(target, postfix: postfix);
        _patchOK = true;
        MelonLogger.Msg($"Patched OK: {qualified}");
      }
      catch (Exception e)
      {
        MelonLogger.Error($"Harmony patch failed: {e}");
      }
    }

    private static void PlayerLog(object o, string msg) =>
      MelonLogger.Msg($"[PlayerPatch] {msg} {(o != null ? o.GetType().FullName : "<null>")}");

    // Postfix runs after the player's per-frame method.
    private static void PlayerTick_Postfix(object __instance)
    {
      if (__instance == null) return;

      try
      {
        // Most player controllers are MonoBehaviours; try to treat it as a Component to read Transform.
        if (__instance is Component comp)
        {
          Vector3 pos = comp.transform.position;
          Quaternion rot = comp.transform.rotation;

          if (IsHost)
          {
            MegabonkMultiplayer.Net.Host.BroadcastPlayerTransform(pos, rot);
          }
          else
          {
            if (MegabonkMultiplayer.Net.Client.TryGetHostTransform(out var hPos, out var hRot))
              comp.transform.SetPositionAndRotation(hPos, hRot);
          }
        }
        else
        {
          // If not a Component, try reflection fallback for 'transform'
          var tProp = __instance.GetType().GetProperty("transform", BindingFlags.Instance | BindingFlags.Public);
          if (tProp?.GetValue(__instance) is Transform tf)
          {
            if (IsHost)
              MegabonkMultiplayer.Net.Host.BroadcastPlayerTransform(tf.position, tf.rotation);
            else if (MegabonkMultiplayer.Net.Client.TryGetHostTransform(out var hPos, out var hRot))
              tf.SetPositionAndRotation(hPos, hRot);
          }
        }
      }
      catch (Exception ex)
      {
        MelonLogger.Warning($"PlayerTick_Postfix exception: {ex.Message}");
      }
    }

    public override void OnUpdate()
    {
      try
      {
        // Poll networking even if we haven't started host/client yet
        Net.NetCommon.Poll();

        // Toggle UI with F1
        if (UnityEngine.Input.GetKeyDown(KeyCode.F1))
          _showUI = !_showUI;

        // Quick host/client toggle with F9 (optional)
        if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
          IsHost = !IsHost;

        // Drive per-role tick if started
        if (IsHost) Net.Host.Tick();
        else Net.Client.Tick();
      }
      catch (Exception e) { MelonLogger.Error(e); }
    }

    public override void OnGUI()
    {
      if (!_showUI) return;

      const int pad = 8;
      int w = 300, h = 155;
      var rect = new Rect(pad, pad, w, h);
      GUI.Box(rect, "Megabonk MP (F1 hide)");

      GUILayout.BeginArea(new Rect(pad + 10, pad + 30, w - 20, h - 40));
      GUILayout.Label($"Patch: {(_patchOK ? "OK" : "idle")}  Role: {(IsHost ? "Host" : "Client")}");

      GUILayout.BeginHorizontal();
      GUILayout.Label("IP:", GUILayout.Width(25));
      _ip = GUILayout.TextField(_ip, GUILayout.Width(160));
      GUILayout.Label("Port:", GUILayout.Width(40));
      var portStr = GUILayout.TextField(_port.ToString(), GUILayout.Width(60));
      if (int.TryParse(portStr, out var p)) _port = p;
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Start Host", GUILayout.Width(120)))
      {
        try { Net.Host.Start(_port); IsHost = true; }
        catch (Exception e) { MelonLogger.Error($"Host.Start failed: {e}"); }
      }
      if (GUILayout.Button("Join", GUILayout.Width(120)))
      {
        try { Net.Client.Connect(_ip, _port); IsHost = false; }
        catch (Exception e) { MelonLogger.Error($"Client.Connect failed: {e}"); }
      }
      GUILayout.EndHorizontal();

      GUILayout.EndArea();
    }
  }
}
