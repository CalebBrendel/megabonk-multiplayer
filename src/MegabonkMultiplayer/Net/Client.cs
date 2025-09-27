using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  public static class Client
  {
    static NetPeer _peer;
    static bool _wired;

    static Vector3 _pos;
    static Quaternion _rot;
    static bool _havePose;

    static void EnsureWired()
    {
      if (_wired) return;
      _wired = true;

      var l = NetCommon.Listener;

      l.PeerConnectedEvent += p =>
      {
        _peer = p;

        // Say hello
        var w = NetCommon.Writer; w.Reset();
        w.Put((byte)OpCode.Hello);
        p.Send(w, DeliveryMethod.ReliableOrdered);
      };

      l.NetworkReceiveEvent += (peer, reader, channel, method) =>
      {
        var op = (OpCode)reader.GetByte();
        switch (op)
        {
          case OpCode.PlayerXform:
            _pos = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            _rot = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            _havePose = true;
            break;

          // case OpCode.Spawn: ...
          // case OpCode.Despawn: ...
          // case OpCode.EnemyHP: ...
        }
        reader.Recycle();
      };
    }

    static void EnsureConnected(string addr = "127.0.0.1", int port = 27015)
    {
      if (_peer != null && _peer.ConnectionState == ConnectionState.Connected) return;

      if (NetCommon.Manager.ConnectedPeerList.Count == 0)
      {
        NetCommon.Manager.Connect(addr, port, "mbonk");
      }
    }

    public static void Tick()
    {
      EnsureWired();
      EnsureConnected();
      NetCommon.Poll();
    }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      pos = _pos;
      rot = _rot;
      return _havePose;
    }
  }
}
