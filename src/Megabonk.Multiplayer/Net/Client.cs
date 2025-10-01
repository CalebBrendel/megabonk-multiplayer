using System;
using (var w = new BinaryWriter(ms))
{
MsgIO.WriteHeader(w, Op.PlayerState);
MsgIO.WriteVec3(w, pos);
MsgIO.WriteQuat(w, rot);
NetCommon.Send(_conn, ms.ToArray(), false);
}
}


public void Tick()
{
if (_conn.m_HSteamNetConnection == 0) return;


int got = NetCommon.Receive(_conn, ref _rx);
if (got > 0) ProcessPacket(_rx, got);


_stateTimer += Time.deltaTime;
if (_stateTimer >= 0.1f)
{
_stateTimer = 0f;
SendPlayerState();
}
}


void ProcessPacket(byte[] data, int len)
{
using (var ms = new MemoryStream(data, 0, len))
using (var r = new BinaryReader(ms))
{
if (!MsgIO.ReadHeader(r, out var op)) return;
switch (op)
{
case Op.PlayerState:
var pos = MsgIO.ReadVec3(r);
var rot = MsgIO.ReadQuat(r);
HarmonyPatches.GameHooks.ApplyRemotePlayerState(SteamUser.GetSteamID(), pos, rot);
break;
case Op.Ready:
bool ready = r.ReadBoolean();
MelonLoader.MelonLogger.Msg($"Host ready: {ready}");
break;
}
}
}
}
}
