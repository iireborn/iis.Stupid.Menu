import pathlib, subprocess, textwrap, sys, os

root = pathlib.Path.cwd()
cec = (root / "References" / "core" / "Mono.Cecil.dll").resolve()
cec_rocks = (root / "References" / "core" / "Mono.Cecil.Rocks.dll").resolve()
managed = (root / "References" / "Managed" / "Assembly-CSharp.dll").resolve()

proj_dir = root / "_tmp_obsolete"
proj_dir.mkdir(exist_ok=True)
cs = r'''
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

class Prog {
 static void Main(string[] args) {
  string target = args[0];
  var asm = AssemblyDefinition.ReadAssembly(target);
  foreach(var t in asm.MainModule.Types) {
    var tobs = t.CustomAttributes.FirstOrDefault(a=>a.AttributeType.Name=="ObsoleteAttribute");
    if(tobs!=null) Console.WriteLine($"OBSOLETE TYPE {t.FullName} MSG:{string.Join(";", tobs.ConstructorArguments.Select(x=>x.Value))}");
    foreach(var m in t.Methods) {
      var o = m.CustomAttributes.FirstOrDefault(a=>a.AttributeType.Name=="ObsoleteAttribute");
      if(o!=null) Console.WriteLine($"OBSOLETE METHOD {t.FullName}::{m.Name} MSG:{string.Join(";", o.ConstructorArguments.Select(x=>x.Value))} SIG:{m}");
    }
    foreach(var f in t.Fields) {
      var o = f.CustomAttributes.FirstOrDefault(a=>a.AttributeType.Name=="ObsoleteAttribute");
      if(o!=null) Console.WriteLine($"OBSOLETE FIELD {t.FullName}::{f.Name} MSG:{string.Join(";", o.ConstructorArguments.Select(x=>x.Value))}");
    }
    foreach(var p in t.Properties) {
      var o = p.CustomAttributes.FirstOrDefault(a=>a.AttributeType.Name=="ObsoleteAttribute");
      if(o!=null) Console.WriteLine($"OBSOLETE PROP {t.FullName}::{p.Name} MSG:{string.Join(";", o.ConstructorArguments.Select(x=>x.Value))}");
    }
  }
 }
}
'''
(proj_dir / "Program.cs").write_text(cs)
proj = f'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net462</TargetFramework>
    <LangVersion>8.0</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Mono.Cecil"><HintPath>{cec}</HintPath></Reference>
  </ItemGroup>
</Project>
'''
(proj_dir / "obsolete.csproj").write_text(proj)
import subprocess as sp
print("BUILD")
r=sp.run(["dotnet","build",str(proj_dir/"obsolete.csproj"),"-c","Release","-v","q"], capture_output=True, text=True, timeout=30)
print(r.stdout[-2000:])
print(r.stderr[-2000:])
print("RUN Assembly-CSharp")
r2=sp.run(["dotnet","run","--project",str(proj_dir/"obsolete.csproj"),"--",str(managed)], capture_output=True, text=True, timeout=30)
out=r2.stdout
print(out[:80000])
print("---ERR---")
print(r2.stderr[:3000])
for dll in ["PhotonUnityNetworking.dll","PhotonRealtime.dll","Photon3Unity3D.dll","UnityEngine.CoreModule.dll"]:
    p=root/"References"/"Managed"/dll
    if p.exists():
        r3=sp.run(["dotnet","run","--project",str(proj_dir/"obsolete.csproj"),"--",str(p)], capture_output=True, text=True, timeout=30)
        print(f"\n=== OBSOLETE in {dll} ===")
        print(r3.stdout[:8000])
