using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  public static class Host
  {
    static readonly List<NetPeer> _peers = new();
    static bool _wired;

    static void EnsureWired()
    {
      if (_wired) return;
      _wired = true;

      var l = NetCommon.Listener;

      // Simple key to avoid random LAN connects during tests
      l.ConnectionRequestEvent += req => req.AcceptIfKey("mbonk");

      l.PeerConnectedEvent += peer =>
      {
        if (!_peers.Contains(peer)) _peers.Add(peer);
      };

      l.PeerDisconnectedEvent += (peer, info) =>
      {
        _peers.Remove(peer);
      };

      l.NetworkReceiveEvent += (peer, reader, channel, method) =>
      {
        var op = (OpCode)reader.GetByte();
        switch (op)
        {
          case OpCode.Hello:
            // Optionally: send initial snapshot
            break;

          // case OpCode.FireEvent:
          //   // Apply client input/intent to the authoritative sim
          //   break;
        }
        reader.Recycle();
      };
    }

    public static void Tick()
    {
      EnsureWired();
      NetCommon.Poll();
      // Drive the authoritative sim here if needed
    }

    public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
    {
      if (_peers.Count == 0) return;

      NetDataWriter w = NetCommon.Writer;
      w.Reset();
      w.Put((byte)OpCode.PlayerXform);
      w.Put(pos.x); w.Put(pos.y); w.Put(pos.z);
      w.Put(rot.x); w.Put(rot.y); w.Put(rot.z); w.Put(rot.w);

      foreach (var p in _peers)
        p.Send(w, DeliveryMethod.Unreliable); // fine for frequent pose updates
    }
  }
}
