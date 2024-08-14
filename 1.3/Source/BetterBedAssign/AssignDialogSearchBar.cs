using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BetterBedAssign
{
    public class AssignDialogSearchBar
    {
        private static string searchTerm = "";

        public static void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(Dialog_AssignBuildingOwner), "DoWindowContents"),
                transpiler: new HarmonyMethod(typeof(AssignDialogSearchBar), "Transpiler"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var info = AccessTools.Method(typeof(Rect), "set_width");
            var idx = list.FindIndex(ins => ins.Calls(info));
            var local = list.Find(ins => ins.opcode == OpCodes.Ldloca_S).operand;
            list.InsertRange(idx + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldloca_S, local),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AssignDialogSearchBar), "RenderSearchBar"))
            });
            var info2 = AccessTools.Method(typeof(CompAssignableToPawn), "get_AssigningCandidates");
            var idx5 = list.FindIndex(ins => ins.Calls(info2));
            var info3 = AccessTools.GetDeclaredMethods(typeof(Enumerable)).First(method =>
                    method.Name == "Where" && method.GetParameters()
                        .All(param => param.ParameterType.GetGenericArguments().Length <= 2))
                .MakeGenericMethod(typeof(Pawn));
            list.InsertRange(idx5 + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Dialog_AssignBuildingOwner), "assignable")),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(AssignDialogSearchBar), "GetShouldConsider")),
                new CodeInstruction(OpCodes.Call, info3)
            });
            var idx2 = list.FindIndex(ins => ins.opcode == OpCodes.Brtrue);
            var idx3 = list.FindIndex(idx2 + 1, ins => ins.opcode == OpCodes.Brtrue);
            var label = list[idx3].operand;
            var idx4 = list.FindLastIndex(idx3, ins => ins.opcode == OpCodes.Ldloc_S);
            local = list[idx4].operand;
            var getPawn = list[idx4 + 1].Clone();
            list.InsertRange(idx3 + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_S, local),
                getPawn,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AssignDialogSearchBar), "ShouldShow")),
                new CodeInstruction(OpCodes.Brfalse, label)
            });
            var info4 = AccessTools.Method(typeof(GUI), "set_color");
            var idx6 = list.FindIndex(ins => ins.Calls(info4));
            var label2 = (Label) list[idx6 - 2].operand;
            list[idx6 + 1].labels.Remove(label2);
            list.InsertRange(idx6 + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldloca_S, 14),
                new CodeInstruction(OpCodes.Ldloc_S, 7),
                getPawn,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Dialog_AssignBuildingOwner), "assignable")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AssignDialogSearchBar), "DoAssignDouble"))
            });
            list[idx6 + 1].labels.Add(label2);
            return list;
        }

        public static void DoAssignDouble(ref Rect rect, Pawn pawn, CompAssignableToPawn catp,
            Dialog_AssignBuildingOwner dialog)
        {
            var rel = LovePartnerRelationUtility.ExistingMostLikedLovePartnerRel(pawn, false);
            var lover = rel?.otherPawn;
            if (catp.Props.maxAssignedPawnsCount >= 2 && lover != null && lover.IsColonist && !lover.IsWorldPawn() &&
                lover.relations.everSeenByPlayer && pawn.relations.OpinionOf(lover) > 0)
            {
                var width = rect.width * 0.4f;
                var rect2 = rect.RightPartPixels(width);
                if (Widgets.ButtonText(rect2, "BBA.AssignWith".Translate(rel.def.label)))
                {
                    catp.TryAssignPawn(pawn);
                    catp.TryAssignPawn(lover);
                    if (catp.Props.maxAssignedPawnsCount == 2) dialog.Close();
                }
            }
        }

        public static void RenderSearchBar(ref Rect inRect)
        {
            var rect = inRect.TopPartPixels(20f);
            searchTerm = Widgets.TextField(rect, searchTerm);
            inRect = inRect.BottomPartPixels(inRect.height - 30f);
        }

        public static bool ShouldShow(Pawn pawn) => pawn.LabelNoCount.ToLower().Contains(searchTerm.ToLower());

        public static Func<Pawn, bool> GetShouldConsider(CompAssignableToPawn catp)
        {
            return pawn => ShouldShow(pawn) || catp.AssignedPawns.Contains(pawn);
        }
    }
}