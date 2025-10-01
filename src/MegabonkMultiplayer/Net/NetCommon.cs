using System;
using System.Net;
using System.Net.Sockets; // <-- needed for SocketError
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  public static class NetCommon
  {
    private static NetManager? _manager;
    private static Listener? _listener;
    private static bool _isHost;

    private static Vector3 _lastHostPos;
    private static Quaternion _lastHostRot;
    private static volatile bool _hasHostTransform;

    private const byte MSG_TRANSFORM = 1;

    public static void Init()
    {
      if (_manager != null) return;

      _listener = new Listener();
      _listener.OnReceiveTransform = (pos, rot) =>
      {
        _lastHostPos = pos;
        _lastHostRot = rot;
        _hasHostTransform = true;
      };
      _listener.OnLog = s => MelonLogger.Msg($"[Net] {s}");

      _manager = new NetManager(_listener)
      {
        UnsyncedEvents = true // we poll from Unity thread
      };
    }

    private static void Ensure() { if (_manager == null) Init(); }

    public static void Poll() => _manager?.PollEvents();

    public static void StartHost(int port)
    {
      Ensure();
      _isHost = true;
      if (_manager!.IsRunning) _manager.Stop();

      if (!_manager.Start(port))
        MelonLogger.Warning($"[Net] Host failed to start on {port}");
      else
        MelonLogger.Msg($"[Net] Host listening on {port}");
    }

    public static void Connect(string ip, int port)
    {
      Ensure();
      _isHost = false;
      if (_manager!.IsRunning) _manager.Stop();

      if (!_manager.Start())
      {
        MelonLogger.Warning("[Net] Client failed to start");
        return;
      }

      // Provide an explicit connection key to avoid overload ambiguity across versions
      _manager.Connect(ip, port, "");
      MelonLogger.Msg($"[Net] Connecting to {ip}:{port} …");
    }

    public static void HostTick() { }
    public static void ClientTick() { }

    public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
    {
      if (!_isHost || _manager == null) return;

      var w = new NetDataWriter(1 + 7 * sizeof(float));
      w.Put(MSG_TRANSFORM);
      w.Put(pos.x); w.Put(pos.y); w.Put(pos.z);
      w.Put(rot.x); w.Put(rot.y); w.Put(rot.z); w.Put(rot.w);

      var peers = _manager.ConnectedPeerList;
      for (int i = 0; i < peers.Count; i++)
        peers[i].Send(w, DeliveryMethod.UnreliableSequenced);
    }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      if (!_hasHostTransform) { pos = default; rot = default; return false; }
      pos = _lastHostPos; rot = _lastHostRot; return true;
    }

    // ---- LiteNetLib listener ----
    private sealed class Listener : INetEventListener
    {
      public Action<string>? OnLog;
      public Action<Vector3, Quaternion>? OnReceiveTransform;

      public void OnPeerConnected(NetPeer peer)
        => OnLog?.Invoke("Peer connected");

      public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        => OnLog?.Invoke($"Peer disconnected: {disconnectInfo.Reason}");

      // Implemented for this LiteNetLib version
      public void OnNetworkError(IPEndPoint endPoint, SocketError error)
        => OnLog?.Invoke($"Socket error {error} from {endPoint}");

      // Required by interface; we don't use unconnected messages
      public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
      {
        reader.Recycle();
        OnLog?.Invoke($"Unconnected message ({messageType}) from {remoteEndPoint} ignored");
      }

      public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

      public void OnConnectionRequest(ConnectionRequest request)
      {
        // Accept all; tighten later if needed
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
