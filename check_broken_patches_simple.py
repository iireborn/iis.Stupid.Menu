import pathlib, re, os, collections
root = pathlib.Path(".")
managed = root / "References" / "Managed"

patch_rx = re.compile(r'\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*nameof\s*\(\s*\w+\.(\w+)')
patch_rx2 = re.compile(r'\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(\w+)\s*\)')
import subprocess, textwrap
import pathlib as pl

cec = (root / "References" / "core" / "Mono.Cecil.dll").resolve()
proj_dir = root / "_tmp_broken"
proj_dir.mkdir(exist_ok=True)
cs = r'''
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Mono.Cecil;
class P{
 static void Main(string[] args){
   string managedDir = args[0];
   var resolver = new DefaultAssemblyResolver();
   resolver.AddSearchDirectory(managedDir);
   resolver.AddSearchDirectory("References/core");
   var allTypes = new Dictionary<string, List<string>>(); // type -> dll
   foreach(var dll in Directory.GetFiles(managedDir, "*.dll")){
     try{
       var asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters{ AssemblyResolver = resolver });
       foreach(var t in asm.MainModule.Types){
         if(!allTypes.ContainsKey(t.Name)) allTypes[t.Name]=new List<string>();
         allTypes[t.Name].Add(Path.GetFileName(dll));
       }
     } catch{}
   }
   string srcRoot = Directory.GetCurrentDirectory();
   var files = Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories);
   int ok=0, broken=0, skipped=0;
   foreach(var f in files){
     if(!f.Contains("Patches")) continue;
     var lines = File.ReadAllLines(f);
     for(int i=0;i<lines.Length;i++){
       if(!lines[i].Contains("HarmonyPatch")) continue;
       var m = System.Text.RegularExpressions.Regex.Match(lines[i], @"typeof\s*\(\s*(\w+)\s*\)");
       if(!m.Success) continue;
       string typeName = m.Groups[1].Value;
       var mm = System.Text.RegularExpressions.Regex.Match(lines[i], @"nameof\s*\(\s*\w+\.(\w+)");
       string methodName = mm.Success ? mm.Groups[1].Value : null;
       if(methodName==null && i+1<lines.Length){
         mm = System.Text.RegularExpressions.Regex.Match(lines[i+1], @"nameof\s*\(\s*\w+\.(\w+)");
         if(mm.Success) methodName = mm.Groups[1].Value;
       }
       if(methodName==null){
         var m2 = System.Text.RegularExpressions.Regex.Match(lines[i], "\"(\w+)\"");
         if(m2.Success) methodName = m2.Groups[1].Value;
       }
       if(!allTypes.ContainsKey(typeName)){
         Console.WriteLine($"BROKEN TYPE {Path.GetFileName(f)}:{i+1} type {typeName} not in any managed dll");
         broken++;
         continue;
       }
       if(methodName==null){
         skipped++;
         continue;
       }
       bool foundMethod = false;
       string foundDll = "";
       foreach(var dllName in allTypes[typeName]){
         string path = Path.Combine(managedDir, dllName);
         try{
           var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters{ AssemblyResolver = resolver });
           var t = asm.MainModule.Types.FirstOrDefault(x=>x.Name==typeName);
           if(t==null) continue;
           if(t.Methods.Any(x=>x.Name==methodName) || t.Properties.Any(x=>x.Name==methodName) || t.Fields.Any(x=>x.Name==methodName) || t.Events.Any(x=>x.Name==methodName)){
             foundMethod = true; foundDll = dllName; break;
           }
         } catch{}
       }
       if(!foundMethod){
         Console.WriteLine($"BROKEN PATCH {Path.GetFileName(f)}:{i+1}  {typeName}::{methodName}");
         broken++;
       } else {
         ok++;
       }
     }
   }
   Console.WriteLine($"DONE ok={ok} broken={broken} skipped={skipped}");
 }
}
'''
(proj_dir / "Program.cs").write_text(cs)
(proj_dir / "broken.csproj").write_text(f'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net462</TargetFramework></PropertyGroup>
  <ItemGroup><Reference Include="Mono.Cecil"><HintPath>{cec}</HintPath></Reference></ItemGroup>
</Project>
''')
import subprocess as sp
sp.run(["dotnet","build",str(proj_dir / "broken.csproj"),"-c","Release","-v","q"], check=True)
r = sp.run(["dotnet","run","--project",str(proj_dir / "broken.csproj"),"--", str(managed)], capture_output=True, text=True, timeout=30)
print(r.stdout[:30000])
print(r.stderr[:2000])
