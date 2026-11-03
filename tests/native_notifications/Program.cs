using System.Reflection;
using Kf3;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime;

// Pure CPU/guest-memory fixture. It neither initializes a host nor opens a disc.
void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
var memory = new PSMemory();
var cpu = new CpuContext { SP = 0x801FF000, GP = 0x801E0000, RA = 123, HI = 456, S0 = 789 };
const uint descriptor = 0x801D0000, arena = 0x801C0000, ot = 0x801D1000;
const int arenaSize = 0x1000;
void Arena(int available = arenaSize)
{
    memory.WriteU32(0x80199170, descriptor);
    memory.WriteU32(descriptor + 8, arena);
    memory.WriteU32(descriptor + 4, arena + (uint)available);
    memory.WriteU32(0x801A9174, ot);
    for (uint i = 0; i < 64; i += 4) memory.WriteU32(ot + i, 0x00FFFFFF);
    for (uint i = 0; i < arenaSize + 16; i++) memory.WriteU8(arena + i, 0xCD);
    memory.WriteU32(cpu.GP + 0x164, 0x12345678);
    memory.WriteU8(cpu.GP + 0x68, 83);
}
for (uint i = 0; i < 512; i++) memory.WriteU8(cpu.SP - 512 + i, (byte)i);
for (uint i = 0; i < 8; i++) memory.WriteU8(0x801AEAED + i, 255);
// Native font template: CLUT, texture page, width/height. No game assets needed.
memory.WriteU16(0x8007E572, 0x1234);
memory.WriteU16(0x8007E578, 7);
memory.WriteU16(0x8007E57A, 12);
for (int i = 0; i < 32; i++) { Gte.Write(i, (uint)(500 + i)); Gte.WriteControl(i, (uint)(1000 + i)); }
var gteData = Enumerable.Range(0, 32).Select(Gte.Read).ToArray();
var gteControl = Enumerable.Range(0, 32).Select(Gte.ReadControl).ToArray();
var snapshot = cpu.Snapshot();
void Restored()
{
    Check(cpu.Snapshot().Equals(snapshot), "all CPU registers restored");
    Check(memory.ReadU8(cpu.GP + 0x68) == 83 && memory.ReadU32(cpu.GP + 0x164) == 0x12345678, "brightness and current primitive restored");
    for (uint i = 0; i < 512; i++) Check(memory.ReadU8(cpu.SP - 512 + i) == (byte)i, "stack canary restored");
    Check(gteData.SequenceEqual(Enumerable.Range(0, 32).Select(Gte.Read)) && gteControl.SequenceEqual(Enumerable.Range(0, 32).Select(Gte.ReadControl)), "GTE unchanged");
}
uint Used() => memory.ReadU32(descriptor + 8) - arena;
void Draw() => NativeNotifications.Draw(cpu, memory);
NativeNotifications.Clear(); Arena();
NativeNotifications.Hold("inspection", new byte[] { 4, 0, 17, 19, 7, 127, 7, 4, 17, 1 });
Draw();
Check(Used() == 412, "actual native font allocates one draw-mode and two sprites per glyph");
Check(memory.ReadU8(arena + 12 + 7) == 0x64, "actual native textured sprite packet");
Check(memory.ReadU16(arena + 12 + 10) == 202, "native shadow Y");
Check(memory.ReadU16(arena + 32 + 10) == 203, "native face Y");
Check(memory.ReadU8(arena + 412) == 0xCD, "packet boundary canary");
Restored();
// Repeated rendering does not allocate outside this frame's arena or own vanilla state.
Arena(411); Draw(); Check(Used() == 0, "insufficient primitive arena defers whole message"); Restored();
Arena(); memory.WriteU8(0x801AEAF7, 1); Draw(); Check(Used() == 0, "active vanilla banner wins");
memory.WriteU8(0x801AEAF7, 0); memory.WriteU8(0x801AEAED, 25); Draw(); Check(Used() == 0, "queued gold banner wins");
memory.WriteU8(0x801AEAED, 255);
Check(NativeNotifications.PostText("poison", "POISONED", 2), "finite notice accepted");
Arena(); Draw(); Check(Used() == 412, "held inspection pauses finite poison notice");
NativeNotifications.Cancel("inspection");
Arena(); Draw(); Check(Used() == 332, "pending poison survives other owner's cancel");
Arena(); Draw(); Check(Used() == 332, "finite notice exact second tick");
Arena(); Draw(); Check(Used() == 0, "finite notice expires");
NativeNotifications.Hold("inspection", new byte[] { 0 });
NativeNotifications.PostText("poison", "POISON CURED");
NativeNotifications.Cancel("poison"); Arena(); Draw(); Check(Used() == 52, "cancelling poison leaves inspection intact");
NativeNotifications.Clear();
for (int i = 0; i < 32; i++) Check(NativeNotifications.PostText("poison", "POISONED"), "bounded queue slot");
Check(!NativeNotifications.PostText("poison", "POISONED"), "queue does not grow unbounded");
NativeNotifications.Cancel("poison"); Check(NativeNotifications.PostText("poison", "POISONED"), "owner cancellation releases queue capacity");
NativeNotifications.Clear();
for (int i = 0; i < 32; i++) NativeNotifications.Hold("owner" + i, new byte[] { 0 });
try { NativeNotifications.Hold("overflow", new byte[] { 0 }); throw new Exception("missing hold bound"); } catch (InvalidOperationException) { }
NativeNotifications.Clear();
NativeNotifications.Hold("inspection", new byte[] { 0 });
cpu.V0 = 1; NativeNotifications.Loaded(cpu, memory); Arena(); Draw(); Check(Used() == 52, "failed load preserves hold");
cpu.V0 = 0; NativeNotifications.Loaded(cpu, memory); Arena(); Draw(); Check(Used() == 0, "successful load resets holds and notices");
NativeNotifications.Hold("inspection", new byte[] { 0 }); NativeNotifications.NewPlayer(cpu, memory); Arena(); Draw(); Check(Used() == 0, "new player clears stale text");
// Notice duration follows world ticks rather than render rate.
typeof(FramePacing).GetProperty("Enabled")!.SetValue(null, true);
void Tick(long value) => typeof(FramePacing).GetProperty("Ticks")!.SetValue(null, value);
NativeNotifications.PostText("poison", "POISONED", 2);
Tick(0); Arena(); Draw(); Check(Used() == 332, "first logic tick");
for (int i = 0; i < 10; i++) { Arena(); Draw(); Check(Used() == 332, "extra renders retain notice without consuming ticks"); }
Tick(1); Arena(); Draw(); Check(Used() == 332, "last logic tick");
Tick(2); Arena(); Draw(); Check(Used() == 0, "notice expired independently of render rate");
NativeNotifications.Clear();
Console.WriteLine("PASS: real native font/allocator packets; CPU/stack/GTE canaries; native priority; owner cancellation; bounded queues; reset and tick duration");
