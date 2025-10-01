using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  public static class Client
  {
    public static void Connect(string ip, int port) => NetCommon.Connect(ip, port);
    public static void Tick() => NetCommon.ClientTick();
    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
      => NetCommon.TryGetHostTransform(out pos, out rot);
  }
}
