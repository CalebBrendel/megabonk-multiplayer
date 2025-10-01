using System;
using System.IO;
using UnityEngine;


namespace Megabonk.Multiplayer.Net
{
public enum Op : byte
{
Hello = 1,
Ready = 2,
SeedSync = 3,
PlayerState = 4,
Chat = 5,
Goodbye = 6,
}


public static class MsgIO
{
public const ushort Protocol = 1;


public static void WriteHeader(BinaryWriter w, Op op)
{
w.Write(Protocol);
w.Write((byte)op);
}


public static bool ReadHeader(BinaryReader r, out Op op)
{
op = 0;
var proto = r.ReadUInt16();
if (proto != Protocol) return false;
op = (Op)r.ReadByte();
return true;
}


public static void WriteVec3(BinaryWriter w, Vector3 v)
{ w.Write(v.x); w.Write(v.y); w.Write(v.z); }
public static Vector3 ReadVec3(BinaryReader r)
{ return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
public static void WriteQuat(BinaryWriter w, Quaternion q)
{ w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
public static Quaternion ReadQuat(BinaryReader r)
{ return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
}
}
