import pathlib, subprocess
root = pathlib.Path.cwd()
cec = (root / "References" / "core" / "Mono.Cecil.dll").resolve()
proj_dir = root / "_tmp_gp"
proj_dir.mkdir(exist_ok=True)
cs = r'''
using System;
using System.Linq;
using Mono.Cecil;
class P{
 static void Main(string[] args){
  var asm = AssemblyDefinition.ReadAssembly(args[0]);
  var t = asm.MainModule.Types.First(x=>x.Name=="GamePlayer");
  Console.WriteLine($"Type {t.FullName}");
  foreach(var m in t.Methods.Where(m=>m.Name.Contains("TryGet")||m.Name.Contains("GetGamePlayer")||m.Name.Contains("GetRig")||m.Name.Contains("GetGrabbed")||m.Name.Contains("IsHolding"))){
    string obs = "";
    var o = m.CustomAttributes.FirstOrDefault(x=>x.AttributeType.Name=="ObsoleteAttribute");
    if(o!=null) obs = " [OBSOLETE "+o.ConstructorArguments.First().Value+"]";
    Console.WriteLine($" {m.Name} {m} {obs}");
    foreach(var p in m.Parameters) Console.WriteLine($"   param {p.ParameterType} {p.Name}");
    Console.WriteLine($"   ret {m.ReturnType}");
  }
  Console.WriteLine("--- all static methods ---");
  foreach(var m in t.Methods.Where(m=>m.IsStatic).OrderBy(m=>m.Name)) Console.WriteLine($" {m.Name} {m}");
 }
}
'''
(proj_dir / "Program.cs").write_text(cs)
(proj_dir / "gp.csproj").write_text(f'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net462</TargetFramework></PropertyGroup>
  <ItemGroup><Reference Include="Mono.Cecil"><HintPath>{cec}</HintPath></Reference></ItemGroup>
</Project>
''')
import subprocess as sp
sp.run(["dotnet","build",str(proj_dir/"gp.csproj"),"-c","Release","-v","q"], check=True)
r=sp.run(["dotnet","run","--project",str(proj_dir/"gp.csproj"),"--", str(root/"References/Managed/Assembly-CSharp.dll")], capture_output=True, text=True, timeout=20)
print(r.stdout[:8000])
print(r.stderr[:1000])
