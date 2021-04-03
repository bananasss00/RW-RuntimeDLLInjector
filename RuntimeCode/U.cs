using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RuntimeCode
{
    public static class U
    {
#region Patches
		private static bool DisablePrefix() => false;
		
        private static IEnumerable<CodeInstruction> EmptyTranspiler(IEnumerable<CodeInstruction> code) => code;

        private static bool MakeConfigFloatMenu(Bill_Production bill)
        {
            if (DebugSettings.godMode && Event.current.shift)
                foreach (var bench in Find.CurrentMap.listerThings.ThingsMatching(
                    ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver)))
                    if (bench is IBillGiver billGiver)
                        if (billGiver.BillStack.Bills.Exists(x => x == bill))
                        {
                            var d = bill.recipe?.ProducedThingDef;
                            if (d != null)
                            {
                                var t = ThingMaker.MakeThing(d);
                                GenPlace.TryPlaceThing(t, bench.Position, bench.Map, ThingPlaceMode.Near);
                            }

                            return false;
                        }

            return true;
        }

		public static void DisableFunction(string method)
        {
            var p = method.Method().Patch(prefix: typeof(U).GetMethod(nameof(DisablePrefix), AccessTools.all).ToHarmonyMethod(Priority.First));
            p.Disable();
            p.Enable();
        }
		
		public static void EnableFunction(string method)
        {
            var p = method.Method().Patch(prefix: typeof(U).GetMethod(nameof(DisablePrefix), AccessTools.all).ToHarmonyMethod(Priority.First));
            p.Disable();
        }
		

        // Empty harmony patch. Reoptimize JIT?
		public static void JitOptimize(string method)
        {
            var p = method.Method().Patch(transpiler: typeof(U).GetMethod(nameof(EmptyTranspiler), AccessTools.all).ToHarmonyMethod());
            p.Disable();
            p.Enable();
        }
		
        public static void ShiftClick_InstantCraftingBillsInDevmode()
        {
            var p = "RimWorld.BillRepeatModeUtility:MakeConfigFloatMenu".Method().Patch(prefix: typeof(U).GetMethod(nameof(MakeConfigFloatMenu), AccessTools.all).ToHarmonyMethod());
            p.Disable();
            p.Enable();
        }
#endregion
        
#region Extensions
        public static string ToString(this IEnumerable<string> list, string separator)
        {
            return string.Join(separator, list.ToArray());
        }

        public static string DefAbout(this Def x) => $"{x.defName}({x.LabelCap})[{x.modContentPack?.Name}]";
#endregion

#region Utils
		public static void DumpDefs<T>(string saveDir) where T : Def, new()
        {
            if (!Directory.Exists(saveDir)) {
                Log.Error($"Directory {saveDir} not exist");
                return;
            }
            File.WriteAllLines($"{saveDir}\\DefDatabase_{typeof(T).Name}.txt", DefDatabase<T>.AllDefs
                .Select(x => x.DefAbout())
                .ToArray());
        }

        public static void DumpImplants(string saveDir)
        {
            if (!Directory.Exists(saveDir)) {
                Log.Error($"Directory {saveDir} not exist");
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("HediffDef;HediffDef-LabelCap;HediffDef-Mod;ThingDef;ThingDef-LabelCap;ThingDef-Mod;ThingDef-Tags");
            DefDatabase<HediffDef>.AllDefs
                .Where(x => x.spawnThingOnRemoved != null)
                .ToList()
                .ForEach(d => sb.AppendLine(
                        $"{d.defName};{d.LabelCap};{d.modContentPack.Name};" + $"{d.spawnThingOnRemoved.defName};{d.spawnThingOnRemoved.LabelCap};{d.spawnThingOnRemoved.modContentPack.Name};{d.spawnThingOnRemoved.techHediffsTags?.ToString(", ")}"));
            File.WriteAllText($"{saveDir}\\DumpImplants.csv", sb.ToString());
        }

        public static void DumpRecipesFromWorkTables(string saveDir)
        {
            if (!Directory.Exists(saveDir)) {
                Log.Error($"Directory {saveDir} not exist");
                return;
            }
            var sb = new StringBuilder();
            var workTables = DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x.IsWorkTable).ToList();
            foreach (var workTable in workTables)
            {
                sb.AppendLine($"{workTable.DefAbout()}:");
                var recipes = new HashSet<RecipeDef>(workTable.recipes ?? Enumerable.Empty<RecipeDef>());
                recipes.UnionWith(DefDatabase<RecipeDef>.AllDefsListForReading.Where(x => x.recipeUsers?.Contains(workTable) ?? false));
                foreach (var recipe in recipes)
                {
                    sb.AppendLine($"  {recipe.DefAbout()}:");
                    sb.AppendLine($"    workAmount:{recipe.workAmount}");
                    sb.AppendLine($"    researchPrerequisite:{recipe.researchPrerequisite?.DefAbout()}");
                    sb.AppendLine($"    workSkill:{recipe.workSkill?.DefAbout()}");
                    sb.AppendLine($"    workSkillLearnFactor:{recipe.workSkillLearnFactor}");
                    sb.AppendLine($"    skillRequirements:{recipe.skillRequirements?.Select(x => x.Summary).ToString("; ")}");
                    sb.AppendLine($"    products:{recipe.products?.Select(x => $"{x.count}x {x.thingDef.DefAbout()}").ToString("; ")}");
                    sb.AppendLine($"    productMarketValue:{recipe.products?.Select(x => $"{x.thingDef.GetStatValueAbstract(StatDefOf.MarketValue)}").ToString("; ")}");
                    sb.AppendLine($"    ingredients:");
                    recipe.ingredients?.Select(x => x.Summary).ToList().ForEach(x => 
                    sb.AppendLine($"      {x}"));
                }
            }
            File.WriteAllText($"{saveDir}\\DumpRecipesFromWorkTables.txt", sb.ToString());
        }

        public static void DumpBodyParts(string saveDir)
        {
            if (!Directory.Exists(saveDir)) {
                Log.Error($"Directory {saveDir} not exist");
                return;
            }
            // generate defs db
                var recipeParts = DefDatabase<RecipeDef>.AllDefsListForReading
                    .Where(x => x.appliedOnFixedBodyParts != null && x.addsHediff != null)
                    .GroupBy(x => x.addsHediff)
                    .ToDictionary(x => x.Key, 
                        y => y.First().appliedOnFixedBodyParts.Select(z => z.label));
                var protethes = DefDatabase<HediffDef>.AllDefsListForReading
                    .Where(d => d.addedPartProps != null || d.spawnThingOnRemoved != null || d.countsAsAddedPartOrImplant);
                    //.GroupBy(d => d.modContentPack?.Name ?? "PATCH_INJECTED")
                    //.ToDictionary(x => x.Key, y => y.ToList());

                //File.WriteAllLines($"a:\\from_recipes.txt",  DefDatabase<RecipeDef>.AllDefsListForReading.Where(d => d.addsHediff != null).Select(d => $"{d.addsHediff.LabelCap} ({d.addsHediff.defName})").OrderBy(d => d).Distinct());
                //File.WriteAllLines($"a:\\from_hediffs.txt",  DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.addedPartProps != null || d.spawnThingOnRemoved != null || d.countsAsAddedPartOrImplant).Select(d => $"{d.LabelCap} ({d.defName})").OrderBy(d => d).Distinct());


                // generate table
                var csvContent = new Dictionary<string, List<(string column, object value)>>();
                foreach (var h in protethes)
                {
                    if (!csvContent.TryGetValue(h.LabelCap, out var row))
                    {
                        row = new List<(string column, object value)>();
                        csvContent.Add(h.LabelCap, row);
                    }

                    row.Add(("Name", h.LabelCap));
                    row.Add(("defName", h.defName));
                    row.Add(("Mod", h.modContentPack?.Name ?? "PATCH_INJECTED"));
                    row.Add(("partEfficiency", h.addedPartProps?.partEfficiency));
                    row.Add(("solid", h.addedPartProps?.solid));
                    if (recipeParts.TryGetValue(h, out var parts))
                        row.Add(("appliedOnFixedBodyParts", String.Join(", ", parts.ToArray())));
                    if (h.stages != null)
                    {
                        var stage = h.stages.FirstOrDefault();
                        var statOffsets = stage.statOffsets;
                        foreach (var statModifier in statOffsets ?? Enumerable.Empty<StatModifier>())
                            row.Add(($"SO_{statModifier.stat.LabelCap}", statModifier.value));
                        foreach (var cap in stage.capMods)
                            row.Add(($"CO_{cap.capacity.LabelCap}", cap.offset));
                    }
                }

                // generate csv content
                var sb = new StringBuilder();
                var columns = csvContent.Values
                    .SelectMany(x => x)
                    .Select(x => x.column)
                    .Distinct()
                    .ToArray();
                // header
                foreach (var column in columns)
                    sb.Append($"{column};");
                sb.AppendLine();

                // rows
                foreach (var row in csvContent.Values)
                {
                    var values = new string[columns.Length];
                    foreach (var column in row)
                        values[columns.FirstIndexOf(x => x.Equals(column.column))] = column.value is string s ? s : column.value?.ToString().Replace(".", ",");
                    foreach (var value in values)
                        sb.Append($"{value};");
                    sb.AppendLine();
                }
                File.WriteAllText($"{saveDir}/protheses.csv", sb.ToString());
        }

        public static void DumpRecipesFromWorkTableCSV(string saveDir, string workTableDefname)
        {
            if (!Directory.Exists(saveDir)) {
                Log.Error($"Directory {saveDir} not exist");
                return;
            }
            var sb = new StringBuilder();
            var workTable = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(x => x.defName.Equals(workTableDefname));
            if (workTable == null)
            {
                Log.Error($"{workTableDefname} null");
                return;
            }
            var recipes = new HashSet<RecipeDef>(workTable.recipes ?? Enumerable.Empty<RecipeDef>());
            recipes.UnionWith(DefDatabase<RecipeDef>.AllDefsListForReading.Where(x => x.recipeUsers?.Contains(workTable) ?? false));

            string getCol(IngredientCount x) => x.IsFixedIngredient ? x.FixedIngredient.LabelCap.ToString() : x.filter.Summary;
            var ingsColumns = recipes
                .Where(x => x.ingredients != null)
                .SelectMany(x => x.ingredients)
                .Select(getCol)
                .Distinct()
                .ToArray();

            // define columns
            sb.Append($"defName;label;mod;workAmount;researchPrerequisite;workSkill;workSkillLearnFactor;skillRequirements;products;productMarketValue;");
            foreach (var ic in ingsColumns) sb.Append(ic + ";");
            sb.AppendLine();

            // fill columns
            foreach (var recipe in recipes)
            {
                sb.Append($"{recipe.defName};");
                sb.Append($"{recipe.LabelCap};");
                sb.Append($"{recipe.modContentPack?.Name};");
                sb.Append($"{recipe.workAmount};");
                sb.Append($"{recipe.researchPrerequisite?.DefAbout()};");
                sb.Append($"{recipe.workSkill?.LabelCap};");
                sb.Append($"{recipe.workSkillLearnFactor};");
                sb.Append($"{recipe.skillRequirements?.Select(x => x.Summary).ToString(", ")};");
                sb.Append($"{recipe.products?.Select(x => $"{x.count}x {x.thingDef.DefAbout()}").ToString(", ")};");
                sb.Append($"{recipe.products?.Select(x => $"{x.thingDef.GetStatValueAbstract(StatDefOf.MarketValue)}").ToString(", ")};");

                var ings = new string[ingsColumns.Length];
                foreach (var i in recipe.ingredients ?? Enumerable.Empty<IngredientCount>())
                {
                    string colName = getCol(i);
                    ings[ingsColumns.FirstIndexOf(x => x.Equals(colName))] = i.GetBaseCount().ToString();
                }

                foreach (var ing in ings)
                {
                    sb.Append($"{ing};");
                }

                sb.AppendLine();
            }
            File.WriteAllText($"{saveDir}\\Recipes_{workTableDefname}.csv", sb.ToString());
        }

        public static void AddModnameToLabel<T>() where T : Def, new()
        {
            DefDatabase<T>.AllDefs.ToList().ForEach(x => x.label += $"[{x.modContentPack?.Name}, {x.defName}]");
        }
#endregion
    }
}