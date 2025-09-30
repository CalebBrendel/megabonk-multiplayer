using System;
using System.Net;
using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  internal static class NetCommon
  {
    public static EventBasedNetListener Listener { get; private set; }
    public static NetManager            Manager  { get; private set; }
    public static NetPeer               Peer     { get; internal set; }

    private static readonly NetPacketProcessor _proc = new NetPacketProcessor();

    // Simple demo message (position/rotation)
    internal struct TransformMsg
    {
      public float px, py, pz;
      public float rx, ry, rz, rw;
    }

    public static void Init()
    {
      if (Manager != null) return;

      Listener = new EventBasedNetListener();
      Manager  = new NetManager(Listener)
      {
        IPv6Enabled = false,
        UnconnectedMessagesEnabled = true,
        AutoRecycle = true
      };

      // Wire up events (signatures as of LiteNetLib 1.x):
      Listener.PeerConnectedEvent += peer =>
      {
        MelonLogger.Msg($"[Net] Peer connected: {peer.RemoteEndPoint}");
        Peer = peer;
      };

      Listener.PeerDisconnectedEvent += (peer, info) =>
      {
        MelonLogger.Msg($"[Net] Peer disconnected: {peer.RemoteEndPoint} ({info.Reason})");
        if (Peer == peer) Peer = null;
      };

      // NOTE: This delegate is (NetPeer, NetPacketReader, DeliveryMethod)
      Listener.NetworkReceiveEvent += (peer, reader, method) =>
      {
        try
        {
          _proc.ReadAllPackets(reader);
        }
        catch (Exception ex)
        {
          MelonLogger.Warning($"[Net] ReadAllPackets error: {ex}");
        }
        finally
        {
          reader.Recycle();
        }
      };

      // Optional: log unconnected (pings/NAT punch etc.)
      Listener.NetworkReceiveUnconnectedEvent += (remoteEndPoint, reader, messageType) =>
      {
        // no-op
      };

      // Register handlers
      _proc.RegisterNestedType((w, v) => w.Put(v), r => r.GetVector3());
      _proc.RegisterNestedType((w, q) => { w.Put(q.x); w.Put(q.y); w.Put(q.z); w.Put(q.w); },
                               r => new Quaternion(r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat()));

      _proc.SubscribeReusable<TransformMsg>(OnTransformMsg);
    }

    public static void Poll()
    {
      Manager?.PollEvents();
    }

    private static void OnTransformMsg(TransformMsg m, NetPeer from)
    {
      // Client side: store “host” transform for application by your postfix
      var pos = new Vector3(m.px, m.py, m.pz);
      var rot = new Quaternion(m.rx, m.ry, m.rz, m.rw);
      Client.SetHostTransform(pos, rot);
    }

    internal static void SendTransform(NetPeer to, Vector3 pos, Quaternion rot, DeliveryMethod method = DeliveryMethod.Sequenced)
    {
      var msg = new TransformMsg
      {
        px = pos.x, py = pos.y, pz = pos.z,
        rx = rot.x, ry = rot.y, rz = rot.z, rw = rot.w
      };
      var writer = new NetDataWriter();
      _proc.Write(writer, msg);
      to.Send(writer, method);
    }

    // ---------------- Host ----------------
    internal static class Host
    {
      private static bool _isUp;

      public static void Start(int port)
      {
        if (_isUp) return;

        if (!Manager.IsRunning)
          Manager.Start(port);

        _isUp = true;
        MelonLogger.Msg($"[Host] Listening on UDP {port}");
      }

      public static void Tick()
      {
        // nothing special; Poll() in Entry.OnUpdate handles events
      }

      public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
      {
        if (!Manager.IsRunning) return;

        for (int i = 0; i < Manager.ConnectedPeerList.Count; i++)
          NetCommon.SendTransform(Manager.ConnectedPeerList[i], pos, rot);
      }
    }

    // ---------------- Client ----------------
    internal static class Client
    {
      private static Vector3    _hostPos;
      private static Quaternion _hostRot;
      private static bool       _haveHost;

      public static void Connect(string ip, int port)
      {
        if (!Manager.IsRunning)
          Manager.Start();

        Peer = Manager.Connect(IPAddress.TryParse(ip, out _) ? ip : Dns.GetHostEntry(ip).AddressList[0].ToString(), port, string.Empty);
        MelonLogger.Msg($"[Client] Connecting to {ip}:{port} ...");
      }

      internal static void SetHostTransform(Vector3 pos, Quaternion rot)
      {
        _hostPos = pos;
        _hostRot = rot;
        _haveHost = true;
      }

      public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
      {
        pos = _hostPos; rot = _hostRot;
        return _haveHost;
      }

      public static void Tick()
      {
        // nothing special; Poll() in Entry.OnUpdate handles events
      }

      // For host-to-client direct send (unused here; kept for parity)
      public static void SendToHost(Vector3 pos, Quaternion rot)
      {
        if (Peer == null) return;
        NetCommon.SendTransform(Peer, pos, rot);
      }
    }
  }
}
