namespace MegabonkMultiplayer.Net
{
  using LiteNetLib;
  using LiteNetLib.Utils;
  using System.Collections.Generic;
  using UnityEngine;

  public static class Host
  {
    static readonly List<NetPeer> _peers = new();
    static readonly EventBasedNetListener _l = NetCommon.Listener;

    static Host()
    {
      _l.ConnectionRequestEvent += req => req.AcceptIfKey("mbonk"); // simple key
      _l.PeerConnectedEvent += peer => { _peers.Add(peer); };
      _l.PeerDisconnectedEvent += (peer, info) => { _peers.Remove(peer); };
      _l.NetworkReceiveEvent += OnReceive;
    }

    public static void Tick() => NetCommon.Poll();

    static void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
      var op = (OpCode)reader.GetByte();
      switch (op)
      {
        case OpCode.Hello:
          // could ack here
          break;
        case OpCode.PlayerInput:
          // TODO: apply client input to authoritative sim
          break;
      }
      reader.Recycle();
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
}
