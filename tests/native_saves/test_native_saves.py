"""Standalone canonical save-observer check; no source mod or private game data."""
from pathlib import Path
import argparse
import os
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import quoteattr
HARNESS = r'''
using System.Reflection;
using Kf3;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Modding;
using RecompOne.Runtime.Config;
using Rt=RecompOne.Runtime.Runtime;
using Game=Recompiled.KingsField3_game;
void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
var m=new PSMemory();var c=new CpuContext{SP=0x801FF000,GP=0x8009C214};
Dispatcher.Register("game",new Recompiled.GameDispatchTable());SymbolRegistry.Build();
var fixture=new ModInfo{Id="fixture.native-save-observer",Name="Private CPU resource boundary"};
foreach(string method in new[]{"func_80019538","func_80018358","func_80079E2C"})
 Check(HookManager.AddReplace(fixture,typeof(Game).GetMethod(method)!,typeof(Probe).GetMethod("Noop")!),"resource boundary "+method);
Check(HookManager.AddPre(fixture,typeof(Game).GetMethod("func_800795E0")!,typeof(Probe).GetMethod("CardB")!,-100),"fixture selects actual cardB argument before observer");
HookManager.Commit();NativeSaves.Install();Check(NativeSaves.Attach(),"canonical native observer installed");
void Text(uint at,string value){for(uint i=0;i<=value.Length;i++)m.WriteU8(at+i,i==value.Length?(byte)0:(byte)value[(int)i]);}
Text(0x80011758,"bu00:");Text(0x80081818,"BASLUS-00255");
for(uint i=0;i<396;i++)m.WriteU16(0x80191A5C+i*0x44+6,255);
m.WriteU32(c.GP+0x1B4,Probe.Buffer);m.WriteU8(0x800C85E8+0x6B,7);m.WriteU8(0x800C867E+0x6B,2);m.WriteU16(0x801B24FA,100);m.WriteU16(0x801B24FC,75);
Rt.CardA=new MemoryCard(Path.GetFullPath("a.sav"));Rt.CardB=new MemoryCard(Path.GetFullPath("b.sav"));
c.A0=15;Game.func_80028750(c,m);
Check(c.V0==0 && NativeSaves.LastSuccessful is {Card:0,Slot:1},"actual saver chooses file1, observer does not guess requested15");
var a=NativeSaves.LastSuccessful!;
Check(a.NativeName=="bu00:BASLUS-002551"&&a.CardPath==Rt.CardA.FilePath&&ReferenceEquals(a.CardObject,Rt.CardA),"native filename and active card object/path authoritative");
ConfigManager.Game.CardAPath="pending.sav";Check(NativeSaves.IsCurrentCardAvailable(a),"pending settings path does not replace active card");
Rt.CardA.Enabled=false;Check(!NativeSaves.IsCurrentCardAvailable(a),"disabled actual card rejected");Rt.CardA.Enabled=true;
var originalA=Rt.CardA;Rt.CardA=Rt.CardB;Check(!NativeSaves.IsCurrentCardAvailable(a),"replaced card object rejected");Rt.CardA=originalA;
int head=Rt.CardB.Create("BASLUS-00255?",3);Check(head!=0,"synthetic private cardB file15 allocated");
var ac=Rt.CardA.Chain(Rt.CardA.Find("BASLUS-002551"));var bc=Rt.CardB.Chain(head);
for(int i=0;i<0x6000;i++)Rt.CardB.WriteByte(bc,i,Rt.CardA.ReadByte(ac,i));Rt.CardB.Flush();
byte[] diskA=File.ReadAllBytes(Rt.CardA.FilePath),diskB=File.ReadAllBytes(Rt.CardB.FilePath);
m.WriteU8(0x800C85E8+0x6B,1);m.WriteU16(0x801B24FC,1);
Probe.SelectB=true;c.A0=15;Game.func_8002860C(c,m);Probe.SelectB=false;
Check(c.V0==0&&m.ReadU8(0x800C85E8+0x6B)==7&&m.ReadU16(0x801B24FC)==75,"actual BIOS native load/checksum/unpack");
var b=NativeSaves.LastSuccessful!;Check(b is {Card:1,Slot:15}&&b.NativeName=="bu10:BASLUS-00255?"&&ReferenceEquals(b.CardObject,Rt.CardB),"cardB file15 captured from actual successful native argument");
Event.Dispatch(new OverlayLoadedEvent{Name="fdat02"});Check(ReferenceEquals(b,NativeSaves.LastSuccessful),"area overlay retains identity for later consumers");
// A reader arriving now adopts the same fact without its own I/O observer.
Check(ReferenceEquals(b,NativeSaves.LastSuccessful)&&NativeSaves.IsCurrentCardAvailable(b),"mid-session consumer can adopt already-observed save");
Probe.SelectB=true;c.A0=6;Game.func_8002860C(c,m);Probe.SelectB=false;
Check(c.V0==1&&ReferenceEquals(b,NativeSaves.LastSuccessful),"missing native load preserves last successful identity");
byte[] cardBytes=(byte[])typeof(MemoryCard).GetField("_d",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(Rt.CardB)!;
int checksum=bc[0]*0x2000+0x200;cardBytes[checksum]^=1;
Probe.SelectB=true;c.A0=15;Game.func_8002860C(c,m);Probe.SelectB=false;
Check(c.V0==2&&ReferenceEquals(b,NativeSaves.LastSuccessful),"checksum failure preserves last successful identity");cardBytes[checksum]^=1;
Check(diskA.SequenceEqual(File.ReadAllBytes(Rt.CardA.FilePath))&&diskB.SequenceEqual(File.ReadAllBytes(Rt.CardB.FilePath)),"read-only observer and loads leave both private cards unchanged");
Event.Dispatch(new OverlayLoadedEvent{Name="game"});Check(NativeSaves.LastSuccessful==null,"new game image/reset clears history");
HookManager.RemoveMod(fixture);HookManager.Commit();
Console.WriteLine("PASS: canonical NativeSaves actual native save/load/BIOS, cardA1/cardB15, late consumer, enabled/object/path identity, failed I/O, area/reset, card bytes unchanged");
public static class Probe{
 public const uint Buffer=0x800D0000;
 public static bool SelectB;
 public static void Noop(Action<CpuContext,IMemory> original,CpuContext c,IMemory m){}
 public static void CardB(CpuContext c,IMemory m){if(SelectB)m.WriteU8(c.A0+2,(byte)'1');}
}
'''

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-build', type=Path, default=os.environ.get('KF3_GAME_BUILD'))
    parser.add_argument('--dotnet', default=os.environ.get('DOTNET_HOST_PATH') or shutil.which('dotnet'))
    options = parser.parse_args()
    if options.game_build is None or not options.dotnet:
        parser.error('Pass --game-build and --dotnet, or set KF3_GAME_BUILD and DOTNET_HOST_PATH.')
    build = options.game_build.resolve()
    if not all((build / name).is_file() for name in ('KingsField3.dll', 'RecompOne.Runtime.dll')):
        parser.error('--game-build must contain KingsField3.dll and RecompOne.Runtime.dll.')
    with tempfile.TemporaryDirectory(prefix='kf3-native-saves-') as temporary:
        root = Path(temporary)
        references = ''.join(f'<Reference Include="{name}" HintPath={quoteattr((build / name).as_posix())}/>' for name in ('KingsField3.dll', 'RecompOne.Runtime.dll'))
        (root / 'Check.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>annotations</Nullable></PropertyGroup><ItemGroup>{references}</ItemGroup></Project>''')
        (root / 'Program.cs').write_text(HARNESS)
        subprocess.run([options.dotnet, 'build', str(root / 'Check.csproj'), '-c', 'Release', '-v:q', '-m:1', '-p:UseSharedCompilation=false'], cwd=root, check=True, timeout=60)
        subprocess.run([options.dotnet, str(root / 'bin/Release/net10.0/Check.dll')], cwd=root, check=True, timeout=15)

if __name__ == '__main__':
    main()
