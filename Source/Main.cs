using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

using System.Reflection;
using HarmonyLib;
using System.Reflection.Emit;
using System.CodeDom.Compiler;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Mono.Cecil;

namespace RuntimeDLLInjector
{
    // extern alias MC;

    [StaticConstructorOnStartup]
    public class RuntimeDLLInjectorController : Mod
    {
        private static Texture2D iconTex;
        private static string rootDir;

        public RuntimeDLLInjectorController(ModContentPack content) : base(content)
        {
            LongEventHandler.ExecuteWhenFinished(() => iconTex = ContentFinder<Texture2D>.Get("Buttons/InjectCode"));
            new Harmony("pirateby.runtimecodeinjector").PatchAll();
            rootDir = content.RootDir;
            Log.Message($"Runtime DLL Injector loaded successfully! rootDir: {rootDir}");
        }

		internal static void DrawDebugToolbarButton(WidgetRow widgets) {
			if (widgets.ButtonIcon(iconTex, "Inject code from ModDir/RuntimeCode")) {
                var fileName = $"{rootDir}\\RuntimeCode\\Assemblies\\RuntimeCode.dll";
                if (!File.Exists(fileName))
                {
                    Log.Error($"Dll not found: {fileName}");
                    return;
                }

                // need to change internal dll name so that you can inject it many times
                using(MemoryStream memStream = new MemoryStream())
                {
                    var resolver = new DefaultAssemblyResolver();
                    var resolverPath = $"{rootDir}\\packages\\".Replace("\\", "/");
                    resolver.AddSearchDirectory(resolverPath);

                    var newAsmName = Guid.NewGuid().ToString();
                    using (var asmCecil = AssemblyDefinition.ReadAssembly(fileName, new ReaderParameters { AssemblyResolver = resolver }))
                    {
                        asmCecil.Name = new AssemblyNameDefinition(newAsmName, Version.Parse("1.0.0.0"));
                        asmCecil.Write(memStream);
                    }

                    var asm = Assembly.Load(memStream.ToArray());
                    try {
                        var type = asm.GetType("RuntimeCode.Initializer");
                        if (type == null)
                        {
                            Log.Error($"Can't find type: RuntimeCode.Initializer");
                            return;
                        }

                        var method = type.GetMethod("Start");
                        if (method == null)
                        {
                            Log.Error($"Can't find method: RuntimeCode.Initializer:Start");
                            return;
                        }
                        
                        method.Invoke(null, null);
                    } catch (Exception e) {
                        Log.Error($"Exception when loading code: {e}");
                    }
                }
			}
		}
    }
}
