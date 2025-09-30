using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using System.Collections.Generic;

namespace MegabonkMultiplayer.Net
{
  public enum OpCode : byte { Hello=1, PlayerXform=2, FireEvent=3, Spawn=4, Despawn=5, EnemyHP=6 }

  public static class NetCommon
  {
    public static EventBasedNetListener Listener;
    public static NetManager Manager;
    public static NetPeer Peer; // single peer reference for client; hosts keep a list
    public static NetDataWriter Writer = new NetDataWriter();
    public static bool Started;

    public static void Init()
    {
      if (Started) return;
      Listener = new EventBasedNetListener();
      Manager = new NetManager(Listener) { IPv6Enabled = false, AutoRecycle = true, DisconnectTimeout = 6000 };
      Started = true;
      // Do not Start() yet; host/client will decide
    }

    public static void Poll() { if (Manager != null) Manager.PollEvents(); }
    public static void ResetForRun() { /* clear maps if you add any later */ }
  }

  public static class Host
  {
    static readonly List<NetPeer> _peers = new List<NetPeer>();
    static bool _listening;

    public static void Start(int port)
    {
      if (!_listening)
      {
        NetCommon.Manager.Start(port);
        Hook();
        _listening = true;
        MelonLoader.MelonLogger.Msg($"[Host] Listening on {port}");
      }
    }

    static void Hook()
    {
      NetCommon.Listener.ConnectionRequestEvent += req => req.AcceptIfKey("megabonk");
      NetCommon.Listener.PeerConnectedEvent += peer =>
      {
        _peers.Add(peer);
        MelonLoader.MelonLogger.Msg($"[Host] Peer connected: {peer.EndPoint}");
      };
      NetCommon.Listener.PeerDisconnectedEvent += (peer, info) =>
      {
        _peers.Remove(peer);
        MelonLoader.MelonLogger.Msg($"[Host] Peer disconnected: {peer.EndPoint} ({info.Reason})");
      };
      NetCommon.Listener.NetworkReceiveEvent += (peer, reader, method) =>
      {
        // handle opcodes as you add them
        byte op = reader.GetByte();
        // ...
      };
    }

    public static void Tick()
    {
      // nothing special for now, just polling in Entry.OnUpdate
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
    static bool _connected;

    public static void Connect(string ip, int port)
    {
      if (!_connected)
      {
        if (!NetCommon.Manager.IsRunning) NetCommon.Manager.Start(); // outgoing only
        Hook();
        NetCommon.Peer = NetCommon.Manager.Connect(ip, port, "megabonk");
        MelonLoader.MelonLogger.Msg($"[Client] Connecting to {ip}:{port}...");
      }
    }

    static void Hook()
    {
      NetCommon.Listener.PeerConnectedEvent += peer =>
      {
        if (peer == NetCommon.Peer) { _connected = true; MelonLoader.MelonLogger.Msg("[Client] Connected!"); }
      };
      NetCommon.Listener.PeerDisconnectedEvent += (peer, info) =>
      {
        if (peer == NetCommon.Peer) { _connected = false; MelonLoader.MelonLogger.Msg($"[Client] Disconnected: {info.Reason}"); }
      };
      NetCommon.Listener.NetworkReceiveEvent += (peer, reader, method) =>
      {
        byte op = reader.GetByte();
        if (op == (byte)OpCode.PlayerXform)
        {
          float px = reader.GetFloat(), py = reader.GetFloat(), pz = reader.GetFloat();
          float rx = reader.GetFloat(), ry = reader.GetFloat(), rz = reader.GetFloat(), rw = reader.GetFloat();
          _pos = new Vector3(px, py, pz); _rot = new Quaternion(rx, ry, rz, rw); _have = true;
        }
      };
    }

    public static void Tick() { /* polling handled in Entry.OnUpdate */ }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      pos = _pos; rot = _rot; return _have;
    }
  }
}
