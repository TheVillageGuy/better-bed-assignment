using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BetterBedAssign;

public class AssignDialogSearchBar
{
    private static readonly QuickSearchWidget searchWidget = new();

    public static void DoPatches(Harmony harm)
    {
        harm.Patch(AccessTools.Method(typeof(Dialog_AssignBuildingOwner), "DoWindowContents"),
            transpiler: new HarmonyMethod(typeof(AssignDialogSearchBar), nameof(Transpiler)));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var info1 = AccessTools.PropertySetter(typeof(Rect), nameof(Rect.width));
        var idx1 = codes.FindIndex(ins => ins.Calls(info1));
        codes.InsertRange(idx1 + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldloca, 0),
            CodeInstruction.Call(typeof(AssignDialogSearchBar), nameof(RenderSearchBar))
        });
        var idx2 = codes.FindIndex(ins => ins.opcode == OpCodes.Stloc_S && ins.operand is LocalBuilder { LocalIndex: 31 });
        codes.InsertRange(idx2 + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldloca, 29),
            new CodeInstruction(OpCodes.Ldloca, 28),
            new CodeInstruction(OpCodes.Ldloc, 25),
            CodeInstruction.LoadField(AccessTools.Inner(typeof(Dialog_AssignBuildingOwner), "<>c__DisplayClass12_0"), "pawn"),
            new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.LoadField(typeof(Dialog_AssignBuildingOwner), "assignable"),
            new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.Call(typeof(AssignDialogSearchBar), nameof(DoAssignDouble))
        });
        var info2 = AccessTools.PropertyGetter(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.AssignedPawnsForReading));
        var info3 = AccessTools.PropertyGetter(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.AssigningCandidates));
        foreach (var code in codes)
        {
            yield return code;
            if (code.Calls(info3) || code.Calls(info2))
            {
                yield return CodeInstruction.Call(typeof(AssignDialogSearchBar), nameof(Check));
                if (code.Calls(info2)) yield return CodeInstruction.Call(typeof(Enumerable), nameof(Enumerable.ToList), generics: new[] { typeof(Pawn) });
            }
        }
    }


    public static void DoAssignDouble(ref RectDivider buttonRect, ref RectDivider parentRect, Pawn pawn, CompAssignableToPawn catp,
        Dialog_AssignBuildingOwner dialog)
    {
        var rel = LovePartnerRelationUtility.ExistingMostLikedLovePartnerRel(pawn, false);
        var lover = rel?.otherPawn;
        if (catp.Props.maxAssignedPawnsCount >= 2 && lover != null && lover.IsColonist && !lover.IsWorldPawn() &&
            lover.relations.everSeenByPlayer && pawn.relations.OpinionOf(lover) > 0)
        {
            var rect1 = parentRect.NewCol(95f, HorizontalJustification.Right).Rect;
            var rect2 = buttonRect.NewCol(buttonRect.Rect.width * 0.5f).Rect;
            var rect3 = new Rect
            {
                xMin = rect1.xMin,
                yMin = rect1.yMin,
                xMax = rect2.xMax,
                yMax = rect2.yMax
            };
            if (Widgets.ButtonText(rect3, "BBA.AssignWith".Translate(rel.def.label)))
            {
                catp.TryAssignPawn(pawn);
                catp.TryAssignPawn(lover);
                if (catp.Props.maxAssignedPawnsCount == 2) dialog.Close();
            }
        }
    }

    public static void RenderSearchBar(ref Rect inRect)
    {
        if (KeyBindingDefOf.Cancel.IsDownEvent && searchWidget.CurrentlyFocused())
        {
            searchWidget.Reset();
            searchWidget.Unfocus();
            Event.current.Use();
        }
        else if (Event.current.type == EventType.KeyDown && !searchWidget.CurrentlyFocused()) searchWidget.Focus();

        var rect = new Rect(inRect.x + 5f, inRect.y, inRect.width - 5f, 20f);
        inRect.yMin += 20f;
        searchWidget.OnGUI(rect);
        inRect.yMin += 5f;
        Widgets.DrawLineHorizontal(inRect.x, inRect.y, inRect.width);
        inRect.yMin += 5f;
    }

    public static IEnumerable<Pawn> Check(IEnumerable<Pawn> pawns)
    {
        return pawns.Where(p => searchWidget.filter.Matches(p.LabelCap));
    }
}