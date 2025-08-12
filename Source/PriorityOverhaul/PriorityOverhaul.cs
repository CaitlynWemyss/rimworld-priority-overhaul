using System.Collections.Generic;
using System.Linq;
using Verse;
using HarmonyLib;

namespace PriorityOverhaul
{
    public class PriorityOverhaulConfig : Mod
    {
        public PriorityOverhaulConfig(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("SoggyDorito65.PriorityOverhaul");
            harmony.PatchAll();
        }
    }

    public static class Global
    {
        public static Dictionary<Pawn, Order> Orders = new Dictionary<Pawn, Order>();
    }

    public class Order : IExposable
    {
        private bool safe = false;
        private Pawn pawn;
        public List<WorkTypeDef> Enabled = new List<WorkTypeDef>();
        public List<WorkTypeDef> Disabled = new List<WorkTypeDef>();
        public List<WorkTypeDef> Incapable = new List<WorkTypeDef>();

        public Order(Pawn pawn)
        {
            this.pawn = pawn;
        }

        public static Order FromPriorities(Pawn pawn, DefMap<WorkTypeDef, int> priorities)
        {
            var Enabled = new List<WorkTypeDef>();
            var Disabled = new List<WorkTypeDef>();
            var Incapable = new List<WorkTypeDef>();
            foreach (var (w, v) in priorities.OrderBy(p => p.Value))
            {
                if (pawn.WorkTypeIsDisabled(w))
                {
                    Incapable.Add(w);
                    continue;
                }
                if (v <= 0)
                {
                    Disabled.Add(w);
                    continue;
                }
                Enabled.Add(w);
            }

            var order = new Order(pawn)
            {
                safe = true,
                Enabled = Enabled,
                Disabled = Disabled,
                Incapable = Incapable
            };
            return order;
        }

        public static void RepairUnsafe(ref Order order, Pawn pawn, DefMap<WorkTypeDef, int> priorities)
        {
            if (order == null) order = FromPriorities(pawn, priorities);
            else order.RepairUnsafe(priorities);
        }
        
        private void RepairUnsafe(DefMap<WorkTypeDef, int> priorities)
        {
            if (safe) return;
            
            if (Enabled == null) Enabled = new List<WorkTypeDef>();
            if (Disabled == null) Disabled = new List<WorkTypeDef>();
            if (Incapable == null) Incapable = new List<WorkTypeDef>();
            
            var concat = Enabled.Concat(Disabled).Concat(Incapable).ToList();

            if (concat.Count == 0 && priorities != null)
            {
                var o = FromPriorities(pawn, priorities);
                Enabled = o.Enabled;
                Disabled = o.Disabled;
                Incapable = o.Incapable;
                safe = true;
                return;
            }
            
            var defs = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            Enabled.RemoveAll(d => !defs.Contains(d));
            Disabled.RemoveAll(d => !defs.Contains(d));
            Incapable.RemoveAll(d => !defs.Contains(d));
            foreach (var d in defs.Where(d => !concat.Contains(d)))
                if (pawn.WorkTypeIsDisabled(d)) Incapable.Add(d);
                else Disabled.Add(d);
            RefreshCapable();
            foreach (var d in Enabled) pawn.workSettings.SetPriority(d, 3);
            foreach (var d in Disabled) pawn.workSettings.SetPriority(d, 0);
            safe = true;
        }

        public void RefreshCapable()
        {
            foreach (var d in Incapable.Where(d => !pawn.WorkTypeIsDisabled(d)).ToList())
            {
                Incapable.Remove(d);
                Disabled.Add(d);
            }
            foreach (var d in Enabled.Concat(Disabled).Where(d => pawn.WorkTypeIsDisabled(d)).ToList())
            {
                Enabled.Remove(d);
                Disabled.Remove(d);
                Incapable.Add(d);
            }
        }
        
        public void Move(List<WorkTypeDef> workTypes, WorkTypeDef target, bool inEnabled)
        {
            if (workTypes.Count == 0) return;
            var targetGroup = inEnabled ? Enabled : Disabled;
            if (target != null && !targetGroup.Contains(target)) return;
            
            WorkTypeDef trueTarget = null;
            if (target != null)
            {
                var i = targetGroup.IndexOf(target);
                while (i >= 0 && workTypes.Contains(targetGroup[i])) i--;
                if (i >= 0) trueTarget = targetGroup[i];
            }
            
            var enabledTypes = workTypes.Where(w => Enabled.Contains(w)).ToList();
            var disabledTypes = workTypes.Where(w => Disabled.Contains(w)).ToList();
            enabledTypes.SortByDescending(w => Enabled.IndexOf(w));
            disabledTypes.SortByDescending(w => Disabled.IndexOf(w));

            if (enabledTypes.Count == 0 && disabledTypes.Count == 0) return;

            Enabled.RemoveAll(w => enabledTypes.Contains(w));
            Disabled.RemoveAll(w => disabledTypes.Contains(w));

            var targetIndex = trueTarget == null ? 0 : targetGroup.IndexOf(trueTarget) + 1;
            foreach (var w in disabledTypes)
            {
                targetGroup.Insert(targetIndex, w);
                pawn.workSettings.SetPriority(w, inEnabled ? 3 : 0);
            }
            foreach (var w in enabledTypes)
            {
                targetGroup.Insert(targetIndex, w);
                pawn.workSettings.SetPriority(w, inEnabled ? 3 : 0);
            }
        }

        public void Increase(WorkTypeDef work)
        {
            if (Enabled.Contains(work))
            {
                var i = Enabled.IndexOf(work);
                if (i == 0) return;
                Enabled.RemoveAt(i);
                Enabled.Insert(i - 1, work);
                pawn.workSettings.SetPriority(work, 3);
                return;
            }
            {
                var i = Disabled.IndexOf(work);
                if (i == 0)
                {
                    Disabled.RemoveAt(i);
                    Enabled.Add(work);
                    pawn.workSettings.SetPriority(work, 3);
                    return;
                };
                Disabled.RemoveAt(i);
                Disabled.Insert(i - 1, work);
                pawn.workSettings.SetPriority(work, 0);
            }
        }

        public void Decrease(WorkTypeDef work)
        {
            if (Enabled.Contains(work))
            {
                var i = Enabled.IndexOf(work);
                if (i == Enabled.Count - 1)
                {
                    Enabled.RemoveAt(i);
                    Disabled.Insert(0, work);
                    pawn.workSettings.SetPriority(work, 0);
                    return;
                };
                Enabled.RemoveAt(i);
                Enabled.Insert(i + 1, work);
                pawn.workSettings.SetPriority(work, 3);
                return;
            }
            {
                var i = Disabled.IndexOf(work);
                if (i == Disabled.Count - 1) return;
                Disabled.RemoveAt(i);
                Disabled.Insert(i + 1, work);
                pawn.workSettings.SetPriority(work, 0);
            }
        }

        public void Enable(WorkTypeDef work)
        {
            if (!Disabled.Contains(work)) return;
            Disabled.Remove(work);
            Enabled.Add(work);
            pawn.workSettings.SetPriority(work, 3);
        }
        
        public void Disable(WorkTypeDef work)
        {
            if (!Enabled.Contains(work)) return;
            Enabled.Remove(work);
            Disabled.Insert(0, work);
            pawn.workSettings.SetPriority(work, 0);
        }

        public void Toggle(WorkTypeDef work)
        {
            if (Enabled.Contains(work)) Disable(work); else Enable(work);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Enabled, "enabled", LookMode.Def);
            Scribe_Collections.Look(ref Disabled, "disabled", LookMode.Def);
            Scribe_Collections.Look(ref Incapable, "incapable", LookMode.Def);
        }
    }
}