using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModBase;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterBedAssign
{
    public class BetterBedAssignMod : BaseMod<BetterBedAssignSettings>
    {
        public static MethodInfo ShouldShowAssignmentGizmo;

        public BetterBedAssignMod(ModContentPack content) : base("legodude17.BetterBedAssign", null, content)
        {
            Harm.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders"),
                postfix: new HarmonyMethod(GetType(), "AddOwnerAssign"));
            Harm.Patch(AccessTools.Method(typeof(CompAssignableToPawn), "CompGetGizmosExtra"),
                postfix: new HarmonyMethod(GetType(), "AddGizmos"));
            ShouldShowAssignmentGizmo = AccessTools.Method(typeof(CompAssignableToPawn), "ShouldShowAssignmentGizmo");
            AssignDialogSearchBar.DoPatches(Harm);
        }

        public override string SettingsCategory()
        {
            return "";
        }

        public static void AddOwnerAssign(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            var c = IntVec3.FromVector3(clickPos);
            foreach (var thing in c.GetThingList(pawn.Map))
                if (thing.TryGetComp<CompAssignableToPawn>() is CompAssignableToPawn catp &&
                    catp.AssigningCandidates.Contains(pawn))
                {
                    var res = catp.CanAssignTo(pawn);
                    var text = (catp.AssignedAnything(pawn)
                        ? "BuildingReassign".Translate()
                        : "BuildingAssign".Translate()) + " " + "BBA.To".Translate(pawn, thing);
                    opts.Add(res.Accepted
                        ? new FloatMenuOption(text, () => catp.TryAssignPawn(pawn))
                        : new FloatMenuOption(text + " (" + res.Reason.StripTags() + ")", null));
                }
        }

        public static IEnumerable<Gizmo> AddGizmos(IEnumerable<Gizmo> gizmos, CompAssignableToPawn __instance)
        {
            foreach (var gizmo in gizmos) yield return gizmo;

            if (__instance.AssignedPawns.Any() && (bool) ShouldShowAssignmentGizmo.Invoke(__instance, null))
                yield return new Command_Action
                {
                    defaultLabel = "BuildingUnassign".Translate() + " " + "BBA.All".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/UnassignAll"),
                    activateSound = SoundDefOf.Click,
                    action = () =>
                    {
                        var pawns = __instance.AssignedPawns.ToList();
                        foreach (var pawn in pawns) __instance.TryUnassignPawn(pawn);
                    }
                };

            if (__instance.AssignedPawns.Any())
                foreach (var pawn in __instance.AssignedPawns)
                    yield return new Command_Action
                    {
                        defaultLabel = "BBA.GoTo".Translate(pawn),
                        icon = ContentFinder<Texture2D>.Get("UI/GoToOwner"),
                        activateSound = SoundDefOf.Click,
                        action = () =>
                        {
                            Find.Selector.ClearSelection();
                            Find.Selector.Select(pawn);
                            Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
                        }
                    };
        }
    }

    public class BetterBedAssignSettings : BaseModSettings
    {
    }
}