using System;
using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  // Shared networking primitives used by Host/Client.
  public static class NetCommon
  {
    public static EventBasedNetListener Listener { get; private set; } = null!;
    public static NetManager Manager { get; private set; } = null!;
    public static NetPeer?  Peer    { get; internal set; }

    // Simple transform message (must be a reference type for NetPacketProcessor)
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

      // Accept with a light "key" so random LAN noise doesn't connect.
      Listener.ConnectionRequestEvent += req =>
      {
        // Accept everyone for now. You can gate with req.Data if you want a key.
        req.AcceptIfKey(null);
      };

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

      // NOTE: Old LiteNetLib uses NetDataReader + 4 params here.
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

      // Register message + handlers
      Processor.RegisterReusable<TransformMsg>();
      Processor.SubscribeReusable<TransformMsg>((peer, msg) =>
      {
        var pos = new Vector3(msg.x, msg.y, msg.z);
        var rot = new Quaternion(msg.qx, msg.qy, msg.qz, msg.qw);
        Client.ReceiveHostTransform(pos, rot);
      });
    }

    public static void StartIfNeeded()
    {
      if (!Manager.IsRunning)
        Manager.Start();
    }

    public static void StopIfRunning()
    {
      if (Manager.IsRunning)
        Manager.Stop();
      Peer = null;
    }

    public static void Poll()
    {
      Manager?.PollEvents();
    }

    public static void SendTo(NetPeer peer, Transform tf)
    {
      var w = new NetDataWriter();
      Processor.Write(w, new TransformMsg
      {
        x = tf.position.x, y = tf.position.y, z = tf.position.z,
        qx = tf.rotation.x, qy = tf.rotation.y, qz = tf.rotation.z, qw = tf.rotation.w
      });
      peer.Send(w, DeliveryMethod.Unreliable);
    }
  }
}
