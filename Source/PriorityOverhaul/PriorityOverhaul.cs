using System.Collections.Generic;
using System.Linq;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace PriorityOverhaul
{
    public class PriorityOverhaulMod : Mod
    {
        public static PriorityOverhaulSettings settings;
        public override string SettingsCategory() => "Priority Overhaul";
        
        public PriorityOverhaulMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<PriorityOverhaulSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("Use icons", ref settings.useIcons, "If disabled, will use text instead");
            listingStandard.CheckboxLabeled("Highlight disabled", ref settings.highlightDisabled, "If disabled, will draw red border around disabled work types instead of highlighting");
            listingStandard.CheckboxLabeled("Show incapable", ref settings.showIncapable, "Display work types the pawn is unable to do after the disabled work types");
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
    }

    public class PriorityOverhaulSettings : ModSettings
    {
        public bool useIcons = true;
        public bool highlightDisabled = true;
        public bool showIncapable = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useIcons, "useIcons");
            Scribe_Values.Look(ref highlightDisabled, "highlightDisabled");
            Scribe_Values.Look(ref showIncapable, "showIncapable");
            base.ExposeData();
        }
    }

    public class IconPath : DefModExtension
    {
        public string path;
        public static string GetPath(WorkTypeDef def) => def.HasModExtension<IconPath>() ? def.GetModExtension<IconPath>().path : null;
        public static Texture2D GetTexture(WorkTypeDef def) => def.HasModExtension<IconPath>() ? ContentFinder<Texture2D>.Get(GetPath(def)) : null;
    }

    [StaticConstructorOnStartup]
    public static class Global
    {
        public static Dictionary<Pawn, Order> Orders = new Dictionary<Pawn, Order>();

        static Global()
        {
            var harmony = new Harmony("SoggyDorito65.PriorityOverhaul");
            harmony.PatchAll();
        }
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

        public static void RepairUnsafe(Pawn pawn, DefMap<WorkTypeDef, int> priorities)
        {
            Global.Orders.TryGetValue(pawn, out var order);
            if (order == null) order = FromPriorities(pawn, priorities);
            else order.RepairUnsafe(priorities);
            Global.Orders.SetOrAdd(pawn, order);
        }

        public static void RepairUnsafe(Pawn pawn)
        {
            Global.Orders.TryGetValue(pawn, out var order);
            if (order != null && order.safe) return;
            var priorities = new DefMap<WorkTypeDef, int>();
            foreach (var work in DefDatabase<WorkTypeDef>.AllDefsListForReading) priorities[work] = pawn.workSettings.GetPriority(work);
            if (order == null) order = FromPriorities(pawn, priorities);
            else order.RepairUnsafe(priorities);
            Global.Orders.SetOrAdd(pawn, order);
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