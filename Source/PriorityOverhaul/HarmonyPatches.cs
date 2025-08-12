using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.Profile;

namespace PriorityOverhaul
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        [HarmonyPatch]
        public static class Pawn_WorkSettings_CacheWorkGiversInOrder_Patch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.FirstMethod(typeof(Pawn_WorkSettings), m => m.Name.Contains("CacheWorkGiversInOrder"));
            }
    
            public static bool Prefix (
                Pawn ___pawn,
                ref bool ___workGiversDirty,
                List<WorkGiver> ___workGiversInOrderNormal,
                List<WorkGiver> ___workGiversInOrderEmerg,
                DefMap<WorkTypeDef, int> ___priorities
            )
            {
                var order = Global.Orders[___pawn];
                
                ___workGiversInOrderNormal.Clear();
                ___workGiversInOrderEmerg.Clear();
                var emerg = true;
                foreach (var wg in from workTypeDef in order.Enabled from wgd in workTypeDef.workGiversByPriority select wgd.Worker)
                {
                    if (!wg.def.emergency) emerg = false;
                    if (emerg) ___workGiversInOrderEmerg.Add(wg);
                    else ___workGiversInOrderNormal.Add(wg);
                }

                ___workGiversDirty = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.ExposeData))]
        [HarmonyPostfix]
        public static void Pawn_WorkSettings_ExposeData_Patch(Pawn ___pawn)
        {
            Global.Orders.TryGetValue(___pawn, out var order);
            Scribe_Deep.Look(ref order, "priority_overhaul_order", ___pawn);
            Global.Orders.SetOrAdd(___pawn, order);
            if (Scribe.mode != LoadSaveMode.PostLoadInit || order == null) return;
            order.Repair();
        }

        [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
        [HarmonyPostfix]
        public static void Pawn_WorkSettings_EnableAndInitialize_Patch(Pawn ___pawn, DefMap<WorkTypeDef, int> ___priorities)
        {
            if (!Global.Orders.ContainsKey(___pawn)) Global.Orders.Add(___pawn, Order.FromPriorities(___pawn, ___priorities));
        }

        [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.Notify_DisabledWorkTypesChanged))]
        [HarmonyPostfix]
        public static void Pawn_WorkSettings_Notify_DisabledWorkTypesChanged_Patch(Pawn ___pawn)
        {
            if (Global.Orders.ContainsKey(___pawn)) Global.Orders[___pawn].RefreshCapable();
        }

        [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
        [HarmonyPostfix]
        public static void MemoryUtility_ClearAllMapsAndWorld_Patch()
        {
            Global.Orders.Clear();
        }

        [HarmonyPatch(typeof(WidgetsWork), nameof(WidgetsWork.TipForPawnWorker))]
        [HarmonyPostfix]
        public static void WidgetsWork_TipForPawnWorker_Patch(ref string __result)
        {
            __result = __result.ReplaceFirst("Normal priority", "Will do");
        }
    }
}