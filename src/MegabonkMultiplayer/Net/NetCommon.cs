using System;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  // Minimal single-file networking core that avoids the LiteNetLib APIs
  // that vary between versions (e.g., RegisterReusable, peer arg overloads, etc.)
  public static class NetCommon
  {
    private static NetManager? _manager;
    private static Listener? _listener;
    private static bool _isHost;

    private static Vector3 _lastHostPos;
    private static Quaternion _lastHostRot;
    private static volatile bool _hasHostTransform;

    // simple message id
    private const byte MSG_TRANSFORM = 1;

    public static void Init()
    {
      if (_manager != null) return;
      _listener = new Listener();
      _manager  = new NetManager(_listener);
      _manager.UnsyncedEvents = true; // polling from Unity thread
      _listener.OnReceiveTransform = (pos, rot) =>
      {
        _lastHostPos = pos;
        _lastHostRot = rot;
        _hasHostTransform = true;
      };
      _listener.OnLog = s => MelonLoader.MelonLogger.Msg($"[Net] {s}");
    }

    private static void Ensure()
    {
      if (_manager == null) Init();
    }

    public static void Poll()
    {
      _manager?.PollEvents();
    }

    public static void StartHost(int port)
    {
      Ensure();
      _isHost = true;
      if (_manager!.IsRunning)
        _manager.Stop();

      // Host: bind on provided port
      if (!_manager.Start(port))
        MelonLoader.MelonLogger.Warning($"[Net] Host failed to start on {port}");
      else
        MelonLoader.MelonLogger.Msg($"[Net] Host listening on {port}");
    }

    public static void Connect(string ip, int port)
    {
      Ensure();
      _isHost = false;

      if (_manager!.IsRunning)
        _manager.Stop();

      // Client: start on random port
      if (!_manager.Start())
      {
        MelonLoader.MelonLogger.Warning("[Net] Client failed to start");
        return;
      }

      // Avoid Connect ambiguity by passing an explicit key
      _manager.Connect(ip, port, ""); // empty connection key
      MelonLoader.MelonLogger.Msg($"[Net] Connecting to {ip}:{port} ...");
    }

    public static void HostTick() { /* currently unused */ }
    public static void ClientTick() { /* currently unused */ }

    public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
    {
      if (!_isHost || _manager == null) return;

      var writer = new NetDataWriter(1 + 7 * sizeof(float));
      writer.Put(MSG_TRANSFORM);
      writer.Put(pos.x); writer.Put(pos.y); writer.Put(pos.z);
      writer.Put(rot.x); writer.Put(rot.y); writer.Put(rot.z); writer.Put(rot.w);

      // Send to everyone; DeliveryMethod chosen to tolerate loss but keep newest
      var peers = _manager.ConnectedPeerList;
      for (int i = 0; i < peers.Count; i++)
        peers[i].Send(writer, DeliveryMethod.UnreliableSequenced);
    }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      if (!_hasHostTransform) { pos = default; rot = default; return false; }
      pos = _lastHostPos; rot = _lastHostRot; return true;
    }

    // Inner listener to keep NetCommon static and simple
    private sealed class Listener : INetEventListener
    {
      public Action<string>? OnLog;
      public Action<Vector3, Quaternion>? OnReceiveTransform;

      public void OnPeerConnected(NetPeer peer)
        => OnLog?.Invoke($"Peer connected");

      public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        => OnLog?.Invoke($"Peer disconnected: {disconnectInfo.Reason}");

      public void OnNetworkError(IPEndPoint endPoint, SocketError error)
        => OnLog?.Invoke($"Socket error: {error}");

      public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
      {
      }

      public void OnConnectionRequest(ConnectionRequest request)
      {
        // Keep it simple: always accept; if you want a key, use AcceptIfKey("")
        request.Accept();
      }

      public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
      {
        try
        {
          byte kind = reader.GetByte();
          if (kind == MSG_TRANSFORM)
          {
            var pos = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            var rot = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            OnReceiveTransform?.Invoke(pos, rot);
          }
        }
        catch (Exception e)
        {
          OnLog?.Invoke($"Receive error: {e.Message}");
        }
        finally
        {
          reader.Recycle();
        }
      }
    }
  }
}
