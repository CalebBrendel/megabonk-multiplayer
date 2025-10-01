using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  public static class Host
  {
    public static void Start(int port) => NetCommon.StartHost(port);
    public static void Tick() => NetCommon.HostTick();
    public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
      => NetCommon.BroadcastPlayerTransform(pos, rot);
  }
}
