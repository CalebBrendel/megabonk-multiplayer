namespace MegabonkMultiplayer.Net
{
  using LiteNetLib;
  using LiteNetLib.Utils;
  using UnityEngine;

  public static class Client
  {
    static Vector3 _pos;
    static Quaternion _rot;
    static bool _have;
    static readonly EventBasedNetListener _l = NetCommon.Listener;
    static NetPeer? _peer;

    static Client()
    {
      _l.PeerConnectedEvent += p => {
        _peer = p;
        var w = NetCommon.Writer; w.Reset();
        w.Put((byte)OpCode.Hello); p.Send(w, DeliveryMethod.ReliableOrdered);
      };
      _l.NetworkReceiveEvent += OnReceive;
    }

    public static void Connect(string addr = "127.0.0.1", int port = 27015)
    {
      if (NetCommon.Manager.ConnectedPeerList.Count == 0)
        NetCommon.Manager.Connect(addr, port, "mbonk");
    }

    public static void Tick()
    {
      if (_peer == null) Connect();
      NetCommon.Poll();
    }

    static void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
      var op = (OpCode)reader.GetByte();
      switch (op)
      {
        case OpCode.PlayerXform:
          _pos = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
          _rot = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
          _have = true;
          break;
      }
      reader.Recycle();
    }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      pos = _pos; rot = _rot; return _have;
    }
  }
}
