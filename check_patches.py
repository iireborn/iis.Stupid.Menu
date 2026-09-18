import pathlib, subprocess, re, os
root = pathlib.Path.cwd()
cec = (root / "References" / "core" / "Mono.Cecil.dll").resolve()
managed_dir = (root / "References" / "Managed").resolve()
proj_dir = root / "_tmp_patchcheck"
proj_dir.mkdir(exist_ok=True)

cs = r'''
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mono.Cecil;

class P {
  static void Main(string[] args){
    string managedDir = args[0];
    string menuDll = args[1];
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(managedDir);
    resolver.AddSearchDirectory("References/core");
    resolver.AddSearchDirectory(Path.GetDirectoryName(menuDll));
    var rp = new ReaderParameters{ AssemblyResolver = resolver };
    var menu = AssemblyDefinition.ReadAssembly(menuDll, rp);

    var gameAsms = new Dictionary<string, AssemblyDefinition>();
    foreach(var dll in Directory.GetFiles(managedDir, "*.dll")){
      try{
        var a = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters{ AssemblyResolver = resolver });
        gameAsms[Path.GetFileNameWithoutExtension(dll)] = a;
      } catch{}
    }
    string pubDir = Path.Combine(Path.GetDirectoryName(menuDll), "..", "..", "obj", "Release", "netstandard2.1", "publicized");
    if(Directory.Exists(pubDir)){
      foreach(var dll in Directory.GetFiles(pubDir, "*.dll")){
        try{
          var a = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters{ AssemblyResolver = resolver });
          gameAsms[Path.GetFileNameWithoutExtension(dll)] = a;
        } catch{}
      }
    }

    string srcRoot = Directory.GetCurrentDirectory();
    var csFiles = Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories);
    var patchRx = new Regex(@"\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*nameof\s*\(\s*\w+\.(\w+)\s*\)", RegexOptions.Compiled);
    var patchRx2 = new Regex(@"\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*""(\w+)""", RegexOptions.Compiled);
    var patchRx3 = new Regex(@"HarmonyPatch\s*\(\s*typeof\s*\(\s*(\w+)\s*\)", RegexOptions.Compiled);

    var patches = new List<(string file,int line,string type,string method)>();
    foreach(var f in csFiles){
      if(!f.Contains("Patches")) continue;
      var lines = File.ReadAllLines(f);
      for(int i=0;i<lines.Length;i++){
        string line = lines[i];
        if(!line.Contains("HarmonyPatch")) continue;
        var m = patchRx.Match(line);
        if(m.Success){
          patches.Add((f,i+1,m.Groups[1].Value,m.Groups[2].Value));
          continue;
        }
        var m2 = patchRx2.Match(line);
        if(m2.Success){
          patches.Add((f,i+1,m2.Groups[1].Value,m2.Groups[2].Value));
          continue;
        }
        var m3 = patchRx3.Match(line);
        if(m3.Success){
          var next = i+1 < lines.Length ? lines[i+1] : "";
          var mm = Regex.Match(line + " " + next, @"nameof\s*\(\s*\w+\.(\w+)\s*\)");
          if(mm.Success) patches.Add((f,i+1,m3.Groups[1].Value,mm.Groups[1].Value));
          else patches.Add((f,i+1,m3.Groups[1].Value,"?"));
        }
      }
    }
    Console.WriteLine($"Found {patches.Count} patch declarations");
    int broken=0, ok=0;
    foreach(var p in patches){
      string typeName = p.type;
      string methodName = p.method;
      if(methodName=="?"){ Console.WriteLine($"SKIP {Path.GetFileName(p.file)}:{p.line} {typeName}::{methodName} (could not parse)"); continue; }
      bool found = false;
      string foundIn = "";
      foreach(var asm in gameAsms.Values){
        var t = asm.MainModule.Types.FirstOrDefault(x=>x.Name==typeName || x.FullName.EndsWith("."+typeName));
        if(t==null) continue;
        var meth = t.Methods.FirstOrDefault(m=>m.Name==methodName);
        if(meth!=null){ found=true; foundIn=Path.GetFileName(asm.MainModule.FileName); break; }
        var field = t.Fields.FirstOrDefault(f=>f.Name==methodName);
        if(field!=null){ found=true; foundIn=Path.GetFileName(asm.MainModule.FileName)+" (field)"; break; }
        var prop = t.Properties.FirstOrDefault(pr=>pr.Name==methodName);
        if(prop!=null){ found=true; foundIn=Path.GetFileName(asm.MainModule.FileName)+" (prop)"; break; }
      }
      if(!found){
        Console.WriteLine($"BROKEN PATCH {Path.GetFileName(p.file)}:{p.line}  {typeName}::{methodName}  NOT FOUND in any game dll");
        broken++;
      } else {
        ok++;
      }
    }
    Console.WriteLine($"DONE ok={ok} broken={broken}");

    Console.WriteLine("\n=== Menu dll method refs that FAIL to resolve ===");
    int missing=0;
    foreach(var mod in menu.Modules) foreach(var t in mod.GetTypes()) foreach(var m in t.Methods){
      if(m.Body==null) continue;
      foreach(var ins in m.Body.Instructions){
        if(ins.Operand is MethodReference mr){
          string decl = mr.DeclaringType.FullName;
          string typeOnly = mr.DeclaringType.Name;
          bool isGameType = gameAsms.Values.Any(a=>a.MainModule.Types.Any(x=>x.Name==typeOnly));
          if(!isGameType) continue;
          try{
            var def = mr.Resolve();
            if(def==null) throw new Exception("null");
          } catch{
            Console.WriteLine($"MISSING REF {t.FullName}::{m.Name} -> {mr.DeclaringType.FullName}::{mr.Name} {mr}");
            missing++;
            if(missing>200) break;
          }
        }
      }
      if(missing>200) break;
    }
    Console.WriteLine($"Missing refs done {missing}");
  }
}
'''
(proj_dir/"Program.cs").write_text(cs)
(proj_dir/"patch.csproj").write_text(f'''
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net462</TargetFramework></PropertyGroup>
  <ItemGroup><Reference Include="Mono.Cecil"><HintPath>{cec}</HintPath></Reference></ItemGroup>
</Project>
''')
import subprocess as sp
sp.run(["dotnet","build",str(proj_dir/"patch.csproj"),"-c","Release","-v","q"], check=True)
import pathlib as pl
r = sp.run(["dotnet","run","--project",str(proj_dir/"patch.csproj"),"--", str(managed_dir), str(root/"bin/Release/netstandard2.1/ii's Stupid Menu.dll")], capture_output=True, text=True, timeout=30)
print(r.stdout[:30000])
print("ERR", r.stderr[:3000])
