using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.Sound;

namespace PriorityOverhaul
{
    public class MainTabWindow_PriorityOverhaul : MainTabWindow_PawnTable
    {
        protected override PawnTableDef PawnTableDef => DefDatabase<PawnTableDef>.GetNamed("PriorityOverhaul");
    
        protected override float ExtraTopSpace => 10f;

        protected override IEnumerable<Pawn> Pawns => base.Pawns.Where(pawn => !pawn.DevelopmentalStage.Baby());

        public override void DoWindowContents(Rect rect)
        {
            base.DoWindowContents(rect);
            if (Event.current.type == EventType.Layout) return;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(370f, rect.y + 5f, 160f, 30f), "<= " + "HigherPriority".Translate());
            Widgets.Label(new Rect(630f, rect.y + 5f, 160f, 30f), "LowerPriority".Translate() + " =>");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }

    public class ReorderColumn : PawnColumnWorker
    {
        private const int width = 24;
        private const int height = 76;
        private const int iconSize = 36;
        private const int compactIconSize = 32;
        private const int margin = 4;
        private const int compactMargin = 2;
        private static int Width() => PriorityOverhaulMod.settings.useIcons ? PriorityOverhaulMod.settings.compact ? compactIconSize : iconSize : width;
        private static int Height() => PriorityOverhaulMod.settings.useIcons ? PriorityOverhaulMod.settings.compact ? compactIconSize : iconSize : height;
        private static int Margin() => PriorityOverhaulMod.settings.compact ? compactMargin : margin;
        
        public override int GetMinCellHeight(Pawn pawn) => Height() + margin;

        public override int GetMinWidth(PawnTable table)
        {
            var types = DefDatabase<WorkTypeDef>.AllDefsListForReading.Count;
            return (types * (Width() + Margin())) + 28;
        }

        private static Pawn hoveredPawn = null;
        private static WorkTypeDef hoveredType = null;

        private static Pawn selectedPawn = null;
        private static List<WorkTypeDef> selectedTypes = new List<WorkTypeDef>();

        private static bool dragging = false;

        private static bool shiftDrag = false;
        private static bool shiftDisable = false;

        private struct Handle
        {
            public bool inEnabled;
            public WorkTypeDef work;
            public float x;
        }
        
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (pawn.Dead || pawn.workSettings == null || !pawn.workSettings.EverWork) return;
            
            InitControl(rect, pawn);

            Order.RepairUnsafe(pawn);
            var order = Global.Orders[pawn];

            var x = rect.x + 12f;
            var y = rect.y;

            WorkTypeDef click = null;
            WorkTypeDef rightClick = null;
            List<Handle> handles = new List<Handle>();

            if (hoveredPawn == pawn)
            {
                hoveredPawn = null;
                hoveredType = null;
            }

            void render(bool e, bool incapable = false)
            {
                foreach (var w in incapable ? order.Incapable : e ? order.Enabled : order.Disabled)
                {
                    var r = new Rect(x, y + Margin() / 2f, Width(), Height());
                    if (!incapable && CustomWidgets.GetLeftDown(r)) click = w;
                    if (!incapable && CustomWidgets.GetRightDown(r)) rightClick = w;

                    var hover = Mouse.IsOver(r) && !dragging;
                    if (hover)
                    {
                        if (!incapable) {
                            hoveredPawn = pawn;
                            hoveredType = w;
                        }
                        TooltipHandler.TipRegion(r, () => WidgetsWork.TipForPawnWorker(pawn, w, false), pawn.thingIDNumber ^ w.GetHashCode());
                        MouseoverSounds.DoRegion(r);
                    }
                
                    CustomWidgets.PriorityButton(
                        r,
                        w,
                        incapable ? 2 : e ? 0 : 1,
                        incapable || w.relevantSkills.Count == 0 ? -1 : pawn.skills.AverageOfRelevantSkillsFor(w),
                        hover || (pawn == selectedPawn && selectedTypes.Contains(w)) ? 1 : hoveredType == w ? 2 : 0
                    );

                    if (dragging && !incapable) handles.Add(new Handle
                    {
                        inEnabled = e,
                        work = w,
                        x = x + Width() + Margin() / 2f,
                    });
                    
                    x += Width() + Margin();
                }
            }

            handles.Add(new Handle
            {
                inEnabled = true,
                work = null,
                x = x - Margin() / 2f,
            });
            render(true);
            x += Margin() * 3f;
            handles.Add(new Handle
            {
                inEnabled = false,
                work = null,
                x = x - Margin() / 2f,
            });
            render(false);
            if (PriorityOverhaulMod.settings.showIncapable) render(false, true);

            if (rightClick != null)
            {
                dragging = false;
                shiftDrag = false;
                SoundDefOf.DropElement.PlayOneShotOnCamera();
                if (selectedPawn != pawn || !selectedTypes.Contains(rightClick))
                {
                    selectedTypes.Clear();
                    order.Toggle(rightClick, PriorityOverhaulMod.settings.bestGuessEnable);
                    return;
                }
                var e = order.Enabled.Contains(rightClick);
                foreach (var w in selectedTypes) if (e) order.Disable(w); else order.Enable(w, PriorityOverhaulMod.settings.bestGuessEnable);
                selectedTypes.Clear();
                return;
            }

            if (click != null)
            {
                if (selectedPawn != pawn)
                {
                    selectedTypes.Clear();
                    selectedPawn = pawn;
                }
                if (Event.current.shift)
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    if (selectedTypes.Contains(click))
                    {
                        selectedTypes.Remove(click);
                        shiftDisable = true;
                    }
                    else
                    {
                        selectedTypes.Add(click);
                        shiftDisable = false;
                    }
                    shiftDrag = true;
                    goto exit;
                }
                SoundDefOf.DragElement.PlayOneShotOnCamera();
                if (!selectedTypes.Contains(click))
                {
                    selectedTypes.Clear();
                    selectedTypes.Add(click);
                }
                dragging = true;
            }
            exit:
            
            if (CustomWidgets.GetLeftUp(rect) && !dragging)
            {
                if (!Event.current.shift) selectedTypes.Clear();
                dragging = false;
                shiftDrag = false;
            }
            if (selectedPawn != pawn) return;

            if (hoveredPawn == pawn && shiftDrag)
            {
                if (selectedTypes.Contains(hoveredType) && shiftDisable)
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    selectedTypes.Remove(hoveredType);
                }
                if (!selectedTypes.Contains(hoveredType) && !shiftDisable)
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    selectedTypes.Add(hoveredType);
                }
            }
            
            if (!dragging) return;

            var closestHandle = handles.MinBy(h => Math.Abs(h.x - Event.current.mousePosition.x));

            if (CustomWidgets.GetLeftUp(rect))
            {
                SoundDefOf.DropElement.PlayOneShotOnCamera();
                order.Move(selectedTypes, closestHandle.work, closestHandle.inEnabled);
                selectedTypes.Clear();
                dragging = false;
                shiftDrag = false;
                return;
            }
            
            CustomWidgets.PriorityHandle(new Rect(closestHandle.x - 1f, y + Margin() / 2f + 2f, 2f, rect.height - Margin() - 4f));
            CustomWidgets.PriorityGhost(new Rect(Event.current.mousePosition.x - (Width() / 2f), y + Margin() / 2f, Width(), rect.height - Margin()));
        }

        private void InitControl(Rect rect, Pawn pawn)
        {
            if (selectedTypes.Count == 0 && !shiftDrag) selectedPawn = null;
            if (!Mouse.IsOver(rect) && selectedPawn == pawn)
            {
                if (dragging && selectedTypes.Count == 1) selectedTypes.Clear();
                dragging = false;
                shiftDrag = false;
            };
        }
    }
}