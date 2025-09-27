using LiteNetLib;
using LiteNetLib.Utils;

namespace MegabonkMultiplayer.Net
{
  public enum OpCode : byte
  {
    Hello = 1,
    PlayerXform = 2,
    FireEvent = 3,
    Spawn = 4,
    Despawn = 5,
    EnemyHP = 6
  }

  public static class NetCommon
  {
    public static EventBasedNetListener Listener;
    public static NetManager Manager;
    public static NetDataWriter Writer = new NetDataWriter();

    /// <summary>
    /// Initialize networking. Tries to bind to 27015; if taken, falls back to ephemeral.
    /// Call once on game start (both host and client call this).
    /// </summary>
    public static void Init()
    {
      if (Manager != null) return; // already initialized

      Listener = new EventBasedNetListener();
      Manager  = new NetManager(Listener)
      {
        IPv6Enabled = false,
        AutoRecycle = true,
        UnsyncedEvents = true
      };

      // Host-friendly default: try fixed port; else ephemeral (for clients / second instance)
      if (!Manager.Start(27015))
        Manager.Start();
    }

    /// <summary> Poll LiteNetLib events; call every frame. </summary>
    public static void Poll() => Manager?.PollEvents();

    /// <summary> Reset transient state between runs (expand later). </summary>
    public static void ResetForRun()
    {
      // TODO: clear entity maps, sequence ids, pending RPCs, etc.
    }
  }
}
