#define DEBUG

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
using System.Runtime.InteropServices;
using System.Threading;

namespace RuntimeCode
{
	public class Initializer
	{
		[DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
		private static List<H.PatchInfo> Patches = new List<H.PatchInfo>();
        private static HarmonyMethod m(string mName, int priority = Priority.Normal) => typeof(Initializer).GetMethod(mName, AccessTools.all).ToHarmonyMethod(priority);

		private static void UnpatchThread()
		{
			while (true) {
				if (Convert.ToBoolean(GetAsyncKeyState(0x23) & 0x8000)) {
					Log.Warning("VK_END pressed. Unpatch all...");
					foreach (var p in Patches) p.Disable();
					break;
				}
				Thread.Sleep(1000);
			}
		}

		static void DemoPatch()
		{
			Find.WindowStack.ImmediateWindow(832838177, new Rect(UI.screenWidth * 0.5f, 50, 400, 100).Rounded(), WindowLayer.GameUI, () => {
				var wr = new WidgetRow();
				wr.Init(0f, 0f, UIDirection.RightThenUp, 99999f, 4f);
				
				var prevColr = GUI.color;
				var prevText = Text.Font;
				GUI.color = Color.red;
				Text.Font = GameFont.Medium;
				new WidgetRow().Label("DEMO RUNTIME CODE INJECTION");
				GUI.color = prevColr;
				Text.Font = prevText;
			}, false, false, 0f);
		}

		private static Texture2D iconTex = ContentFinder<Texture2D>.Get("Buttons/InjectCode");
		static IEnumerable<CodeInstruction> TranspilerDemo(IEnumerable<CodeInstruction> codeInstructions, ILGenerator iLGenerator)
		{
			// Transpile method: Debug panel with buttons on top 
			return new TranspilerFactory("TranspilerDemo") // name for compare code before and after in directory:
														   // %AppData%\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\TranspilerDebug
				// remove first 2 buttons
				.Replace("ldarg.0;ldfld *;ldsfld *;ldstr *;ldloca.s *;initobj *;ldloc.0;ldc.i4.1;callvirt *;brfalse.s *;ldarg.0;call *", "nop", 2)
				// replacing the icon of the next button with my own
				.Replace("ldarg.0;ldfld Verse.DebugWindowsOpener:widgetRow;ldsfld *", "ldarg.0;ldfld Verse.DebugWindowsOpener:widgetRow;ldsfld RuntimeCode.Initializer:iconTex")
				// search end code for next button
				.Search("brfalse.s *;ldarg.0;call *;ldarg.0")
				// inject random code
				.Insert("ldstr log string;ldc.i4.0;call Verse.Log:Warning")
				.Transpiler(iLGenerator, codeInstructions);
		}

		public static void Start()
		{
			// Unpatch all when pressing END if something went wrong
			new Thread(UnpatchThread).Start();

			// Patches
			typeof(DebugWindowsOpener).Method("DrawButtons").Patch(ref Patches, transpiler: m("TranspilerDemo", Priority.First));
			"DebugWindowsOpener:DrawButtons".Method().Patch(ref Patches, postfix: m("DemoPatch"));

			foreach (var p in Patches) p.Disable(); // disable patches from previous injections
            foreach (var p in Patches) p.Enable();

			// Some utils
			U.DisableFunction("Verse.Steam.SteamManager:Update");
			U.DumpDefs<ThingDef>("a:\\");
			U.DumpDefs<RecipeDef>("a:\\");

			Log.Warning("Code injected!");
		}
	}
}