namespace MegabonkMultiplayer.Net
{
  using LiteNetLib;
  using LiteNetLib.Utils;
  using UnityEngine;
  using System.Collections.Generic;

  public enum OpCode : byte { Hello=1, PlayerXform=2, FireEvent=3, Spawn=4, Despawn=5, EnemyHP=6 }

  public static class NetCommon
  {
    public static EventBasedNetListener Listener;
    public static NetManager Manager;
    public static NetPeer Peer;             // host<->single client for MVP
    public static NetDataWriter Writer = new NetDataWriter();

    public static void Init()
    {
      Listener = new EventBasedNetListener();
      Manager = new NetManager(Listener){ IPv6Enabled = false, AutoRecycle = true };
      Manager.Start(); // host: Start(27015); client: Start() also fine for outgoing
    }

    public static void Poll() => Manager.PollEvents();
  }

  public static class Host
  {
    static List<NetPeer> _peers = new List<NetPeer>();

    public static void Tick()
    {
      NetCommon.Poll();
      // Accept connections etc. (left brief for clarity)
    }

    public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
    {
      var w = NetCommon.Writer;
      w.Reset();
      w.Put((byte)OpCode.PlayerXform);
      w.Put(pos.x); w.Put(pos.y); w.Put(pos.z);
      w.Put(rot.x); w.Put(rot.y); w.Put(rot.z); w.Put(rot.w);
      foreach (var p in _peers) p.Send(w, DeliveryMethod.Unreliable);
    }
  }

  public static class Client
  {
    static Vector3 _pos; static Quaternion _rot; static bool _have;

    public static void Tick()
    {
      NetCommon.Poll();
      // Read packets, set _pos/_rot
    }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      pos = _pos; rot = _rot; return _have;
    }
  }
}
