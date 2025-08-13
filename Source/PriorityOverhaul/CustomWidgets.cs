using Verse;
using UnityEngine;

namespace PriorityOverhaul
{
    [StaticConstructorOnStartup]
    public static class CustomWidgets
    {
        private static Texture2D[] back = null;
        private static Texture2D unskilledBack = genTexture(new Color(0.3f, 0.3f, 0.4f));
        private static Texture2D border = genTexture(new Color(0.3f, 0.3f, 0.3f));
        private static Texture2D selectedBorder = genTexture(Color.white);
        private static Texture2D hintBorder = genTexture(new Color(0.4f, 1f, 0.4f));
        private static Texture2D disabled = genTexture(new Color(1f, 0f, 0f, 0.15f));
        private static Texture2D hintBorderDisabled = genTexture(new Color(1f, 0.2f, 0.2f));
        private static Texture2D ghost = genTexture(new Color(1f, 1f, 1f, 0.3f));
        private static Texture2D handle = genTexture(new Color(1f, 1f, 0.5f));
        
        public static void PriorityButton(Rect rect, string label, bool enabled, float skill, int hover)
        {
            genTextures();
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = skill >= 8 ? Color.black : new Color(0.9f, 0.9f, 0.9f, 1f);
            
            drawBox(rect, skill < 0 ? unskilledBack : back[(int)Mathf.Clamp(Mathf.Round(skill), 0f, 20f)], 2f, hover == 2 ? enabled ? hintBorder : hintBorderDisabled : hover == 1 ? selectedBorder : border);
            if (!enabled) drawBox(rect, disabled);
            
            var retMat = GUI.matrix;
            UI.RotateAroundPivot(-90, rect.center);
            var r = new Rect(0, 0, rect.height + 10f, rect.width);
            r.center = rect.center;

            Widgets.Label(r, trimStr(label.CapitalizeFirst()));
            
            GUI.matrix = retMat;
            GUI.color = Color.white;
        }

        public static void PriorityHandle(Rect rect)
        {
            drawBox(rect, handle);
        }

        public static void PriorityGhost(Rect rect)
        {
            drawBox(rect, ghost);
        }

        public static bool GetLeftDown(Rect rect)
        {
            var ev = Event.current;
            return Mouse.IsOver(rect) && ev.type == EventType.MouseDown && ev.button == 0;
        }
        
        public static bool GetLeftUp(Rect rect)
        {
            var ev = Event.current;
            return Mouse.IsOver(rect) && ev.type == EventType.MouseUp && ev.button == 0;
        }

        public static bool GetRightDown(Rect rect)
        {
            var ev = Event.current;
            return Mouse.IsOver(rect) && ev.type == EventType.MouseDown && ev.button == 1;
        }
        
        private static void drawBox(Rect rect, Texture2D tex, float borderSize, Texture2D btex)
        {
            drawBox(rect, tex);
            drawBox(new Rect(rect.x, rect.y, rect.width, borderSize), btex);
            drawBox(new Rect(rect.x, rect.y, borderSize, rect.height), btex);
            drawBox(new Rect(rect.x, rect.y + rect.height - borderSize, rect.width, borderSize), btex);
            drawBox(new Rect(rect.x + rect.width - borderSize, rect.y, borderSize, rect.height), btex);
        }

        private static void drawBox(Rect rect, Texture2D tex)
        {
            var returnColor = GUI.color;
            GUI.color = tex.GetPixel(0, 0);
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false, 1f);
            GUI.color = returnColor;
        }

        private static void genTextures()
        {
            if (back != null) return;
            back = new Texture2D[21];
            for (var i = 0; i <= 16; i++)
            {
                var value = (float)i / 16f;
                back[i] = genTexture(new Color(value, value, value));
            }
            for (var i = 17; i <= 20; i++)
            {
                var value = (23f - (float)i) / 8f;
                back[i] = genTexture(new Color(1f, 1f, value));
            }
        }
        
        private static Texture2D genTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            return tex;
        }

        private static string trimStr(string str)
        {
            if (str.Length <= 10) return str;
            return str.Substring(0, 8) + "..";
        }
    }
}