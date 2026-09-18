import pathlib, subprocess, textwrap
root = pathlib.Path.cwd()
cec = (root / "References" / "core" / "Mono.Cecil.dll").resolve()
import pathlib as pl
proj_dir = root / "_tmp_msg2"
proj_dir.mkdir(exist_ok=True)
cs = r'''
using System;
using System.Linq;
using Mono.Cecil;
class P{
 static void Main(string[] args){
  string dll = args[0];
  var asm = AssemblyDefinition.ReadAssembly(dll);
  int c=0;
  foreach(var t in asm.MainModule.Types){
    if(t.Name!="Texture2D" && t.Name!="Material") continue;
    Console.WriteLine($"--- {t.FullName} in {System.IO.Path.GetFileName(dll)}");
    foreach(var m in t.Methods.Where(m=>m.IsConstructor)){
      var o = m.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="ObsoleteAttribute");
      string sig = m.ToString();
      if(o!=null){
        var msg = o.ConstructorArguments.FirstOrDefault().Value as string;
        var isErr = o.Properties.FirstOrDefault(p=>p.Name=="IsError").Argument.Value;
        Console.WriteLine($" OBSOLETE CTOR {sig} MSG=\"{msg}\" IsError={isErr}");
      } else {
        Console.WriteLine($" OK CTOR {sig}");
      }
      c++; if(c>40) break;
    }
    foreach(var m in t.Methods.Where(m=>m.CustomAttributes.Any(x=>x.AttributeType.Name=="ObsoleteAttribute"))){
      if(m.IsConstructor) continue;
      var o=m.CustomAttributes.First(x=>x.AttributeType.Name=="ObsoleteAttribute");
      var msg = o.ConstructorArguments.FirstOrDefault().Value as string;
      Console.WriteLine($" OBSOLETE METHOD {m.Name} {m} MSG=\"{msg}\"");
    }
  }
  string gp = args.Length>1 ? args[1] : null;
  if(gp!=null){
    var asm2 = AssemblyDefinition.ReadAssembly(gp);
    foreach(var t in asm2.MainModule.Types.Where(x=>x.Name=="GamePlayer")){
      Console.WriteLine($"--- {t.FullName}");
      foreach(var m in t.Methods.Where(m=>m.CustomAttributes.Any(x=>x.AttributeType.Name=="ObsoleteAttribute"))){
        var o=m.CustomAttributes.First(x=>x.AttributeType.Name=="ObsoleteAttribute");
        var msg = o.ConstructorArguments.FirstOrDefault().Value as string;
        Console.WriteLine($" OBSOLETE {m.Name} {m} MSG=\"{msg}\"");
      }
    }
  }
 }
}
'''
(proj_dir / "Program.cs").write_text(cs)
(proj_dir / "msg2.csproj").write_text(f'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net462</TargetFramework><LangVersion>8.0</LangVersion></PropertyGroup>
  <ItemGroup><Reference Include="Mono.Cecil"><HintPath>{cec}</HintPath></Reference></ItemGroup>
</Project>
''')
import subprocess as sp
sp.run(["dotnet","build",str(proj_dir/"msg2.csproj"),"-c","Release","-v","q"], check=True)
for dll in ["UnityEngine.CoreModule.dll","UnityEngine.ImageConversionModule.dll","UnityEngine.SharedInternalsModule.dll"]:
    p = root / "References" / "Managed" / dll
    if not p.exists(): continue
    print(f"\n===== {dll} =====")
    r = sp.run(["dotnet","run","--project",str(proj_dir/"msg2.csproj"),"--", str(p), str(root/"References/Managed/Assembly-CSharp.dll")], capture_output=True, text=True, timeout=20)
    print(r.stdout[:8000])
    if r.stderr: print("ERR", r.stderr[:1000])
