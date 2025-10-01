using System;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  /// <summary>
  /// Networking core + nested Host/Client facades. Single-file version.
  /// </summary>
  public static class NetCommon
  {
    public static EventBasedNetListener Listener { get; private set; } = null!;
    public static NetManager            Manager  { get; private set; } = null!;
    public static NetPeer?              Peer     { get; internal set; }

    // Simple transform message. (reference type for NetPacketProcessor)
    public class TransformMsg
    {
      public float x, y, z;
      public float qx, qy, qz, qw;
    }

    public static readonly NetPacketProcessor Processor = new NetPacketProcessor();

    public static void Init()
    {
      if (Manager != null) return;

      Listener = new EventBasedNetListener();
      Manager  = new NetManager(Listener)
      {
        AutoRecycle = true,
        IPv6Enabled = false,
        UnconnectedMessagesEnabled = false
      };

      // Accept all for now; you can gate with a key later.
      Listener.ConnectionRequestEvent += req => req.AcceptIfKey(null);

      Listener.PeerConnectedEvent += p =>
      {
        Peer = p;
        MelonLogger.Msg($"[Net] Connected: {p.EndPoint}");
      };

      Listener.PeerDisconnectedEvent += (p, reason) =>
      {
        if (Peer == p) Peer = null;
        MelonLogger.Msg($"[Net] Disconnected: {reason}");
      };

      // NOTE: This handler signature matches LiteNetLib builds that raise 4 params:
      // (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
      Listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
      {
        try
        {
          Processor.ReadAllPackets(reader);
        }
        catch (Exception ex)
        {
          MelonLogger.Warning($"[Net] ReadAllPackets error: {ex.Message}");
        }
      };

      // Register message + handler used by Client:
      Processor.RegisterReusable<TransformMsg>();
      Processor.SubscribeReusable<TransformMsg>((peer, msg) =>
      {
        var pos = new Vector3(msg.x, msg.y, msg.z);
        var rot = new Quaternion(msg.qx, msg.qy, msg.qz, msg.qw);
        Client.ReceiveHostTransform(pos, rot);
      });
    }

    public static void Poll() => Manager?.PollEvents();

    internal static void SendTransform(NetPeer to, Vector3 pos, Quaternion rot, DeliveryMethod method = DeliveryMethod.Unreliable)
    {
      var w = new NetDataWriter();
      Processor.Write(w, new TransformMsg
      {
        x = pos.x, y = pos.y, z = pos.z,
        qx = rot.x, qy = rot.y, qz = rot.z, qw = rot.w
      });
      to.Send(w, method);
    }

    // ================= HOST =================

    public static class Host
    {
      private static bool _started;
      public static void Start(int port)
      {
        if (_started) return;
        NetCommon.Init();
        Manager.BroadcastReceiveEnabled = false;
        Manager.Start(port);
        _started = true;
        MelonLogger.Msg($"[Host] Listening UDP {port}");
      }

      public static void Tick() { /* events are polled in Entry.OnUpdate */ }

      public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
      {
        if (!Manager.IsRunning) return;
        var peers = Manager.ConnectedPeerList;
        if (peers.Count == 0) return;

        var w = new NetDataWriter();
        Processor.Write(w, new TransformMsg
        {
          x = pos.x, y = pos.y, z = pos.z,
          qx = rot.x, qy = rot.y, qz = rot.z, qw = rot.w
        });

        foreach (var p in peers)
          p.Send(w, DeliveryMethod.Unreliable);
      }
    }

    // ================= CLIENT =================

    public static class Client
    {
      private static Vector3 _hostPos;
      private static Quaternion _hostRot;
      private static bool _haveHost;

      public static void Connect(string ip, int port)
      {
        NetCommon.Init();
        if (!Manager.IsRunning) Manager.Start();

        Peer = Manager.Connect(ip, port, null);
        MelonLogger.Msg($"[Client] Connecting to {ip}:{port} …");
      }

      internal static void ReceiveHostTransform(Vector3 pos, Quaternion rot)
      {
        _hostPos = pos; _hostRot = rot; _haveHost = true;
      }

      public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
      {
        pos = _hostPos; rot = _hostRot; return _haveHost;
      }

      public static void Tick() { /* events are polled in Entry.OnUpdate */ }
    }
  }
}
