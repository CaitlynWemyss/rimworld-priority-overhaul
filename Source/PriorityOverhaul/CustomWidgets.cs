using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;

namespace PriorityOverhaul
{
    [StaticConstructorOnStartup]
    public static class CustomWidgets
    {
        private static Texture2D[] back = null;
        private static Dictionary<WorkTypeDef, Texture2D> iconsLight = null;
        private static Dictionary<WorkTypeDef, Texture2D> iconsDark = null;
        private static Dictionary<WorkTypeDef, Texture2D> iconsIncapable = null;
        private static Texture2D unskilledBack = genTexture(new Color(0.3f, 0.3f, 0.4f));
        private static Texture2D border = genTexture(new Color(0.3f, 0.3f, 0.3f));
        private static Texture2D disabledBorder = genTexture(new Color(0.5f, 0.1f, 0.1f));
        private static Texture2D selectedBorder = genTexture(Color.white);
        private static Texture2D hintBorder = genTexture(new Color(0.4f, 1f, 0.4f));
        private static Texture2D disabled = genTexture(new Color(1f, 0f, 0f, 0.1f));
        private static Texture2D hintBorderDisabled = genTexture(new Color(1f, 0.5f, 0.2f));
        private static Texture2D incapableBack = genTexture(Color.black);
        private static Texture2D ghost = genTexture(new Color(1f, 1f, 1f, 0.3f));
        private static Texture2D handle = genTexture(new Color(1f, 1f, 0.5f));
        private static Color lightText = new Color(0.9f, 0.9f, 0.9f);
        private static Color darkText = new Color(0.1f, 0.1f, 0.1f);
        private static Color incapableText = new Color(0.7f, 0.7f, 0.7f);
        
        public static void PriorityButton(Rect rect, WorkTypeDef work, int state, float skill, int hover)
        {
            genTextures();
            if (state != 2) drawBox(rect,
                skill < 0 ? unskilledBack : back[(int)Mathf.Clamp(Mathf.Round(skill), 0f, 20f)],
                2f,
                hover == 2 ? state == 0 ? hintBorder : hintBorderDisabled : hover == 1 ? selectedBorder : !PriorityOverhaulMod.settings.highlightDisabled && state == 1 ? disabledBorder : border);
            if (PriorityOverhaulMod.settings.highlightDisabled && state == 1) drawBox(rect, disabled);
            if (state == 2) drawBox(rect, incapableBack);

            var dark = skill >= 10;

            if (PriorityOverhaulMod.settings.useIcons)
            {
                if (iconsLight[work] != null)
                {
                    var inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
                    GUI.DrawTexture(inner,
                        state == 2 ? iconsIncapable[work] : dark ? iconsDark[work] : iconsLight[work],
                        ScaleMode.StretchToFill, true, 1f);
                    return;
                }

                // Fallback
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = state == 2 ? incapableText : dark ? darkText : lightText;
                var r = new Rect(0, 0, rect.width + 10f, rect.height);
                r.center = rect.center;
                Widgets.Label(r, shortStr(work.gerundLabel.CapitalizeFirst()));
                GUI.color = Color.white;
            }
            else
            {
                // Label rotation courtesy of Fluffy. I legitimately couldn't have figured this out without stealing his algorithm. This code is ridiculously finicky.
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = state == 2 ? incapableText : dark ? darkText : lightText;
                var retMat = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                GUIUtility.RotateAroundPivot(-90f, rect.center);
                GUI.matrix = retMat * GUI.matrix;
                var r = new Rect(0, 0, rect.height + 10f, rect.width);
                r.center = rect.center;
                Widgets.Label(r, trimStr(work.labelShort.CapitalizeFirst()));
                GUI.matrix = retMat;
                GUI.color = Color.white;
            }
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
            var inner = rect;
            inner.x += borderSize / Prefs.UIScale;
            inner.y += borderSize / Prefs.UIScale;
            inner.width -= borderSize * 2 / Prefs.UIScale;
            inner.height -= borderSize * 2 / Prefs.UIScale;
            
            drawBox(rect, btex);
            drawBox(inner, tex);
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
                var value = i / 16f;
                back[i] = genTexture(new Color(value, value, value));
            }
            for (var i = 17; i <= 20; i++)
            {
                var value = (23f - i) / 8f;
                back[i] = genTexture(new Color(1f, 1f, value));
            }

            var defs = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            iconsLight = new Dictionary<WorkTypeDef, Texture2D>();
            iconsDark = new Dictionary<WorkTypeDef, Texture2D>();
            iconsIncapable = new Dictionary<WorkTypeDef, Texture2D>();
            foreach (var def in defs)
            {
                var tex = IconPath.GetTexture(def);
                if (tex == null)
                {
                    iconsLight.Add(def, null);
                    iconsDark.Add(def, null);
                    continue;
                }
                var pixels = getPixels(tex);

                var lightPixels = (Color[]) pixels.Clone();
                var light = new Texture2D(tex.width, tex.height);
                for (var i = 0; i < lightPixels.Length; i++) lightPixels[i] *= lightText;
                light.SetPixels(lightPixels);
                light.Apply();

                var darkPixels = (Color[]) pixels.Clone();
                var dark = new Texture2D(tex.width, tex.height);
                for (var i = 0; i < darkPixels.Length; i++) darkPixels[i] *= darkText;
                dark.SetPixels(darkPixels);
                dark.Apply();

                var incapablePixels = (Color[]) pixels.Clone();
                var incapable = new Texture2D(tex.width, tex.height);
                for (var i = 0; i < incapablePixels.Length; i++) incapablePixels[i] *= incapableText;
                incapable.SetPixels(incapablePixels);
                incapable.Apply();
                
                iconsLight.Add(def, light);
                iconsDark.Add(def, dark);
                iconsIncapable.Add(def, incapable);
            }
        }
        
        private static Texture2D genTexture(Color color, int width = 1, int height = 1)
        {
            var tex = new Texture2D(width, height);
            var cols = new Color[width * height];
            for (var i = 0; i < width * height; i++) cols[i] = color;
            tex.SetPixels(cols);
            return tex;
        }

        private static Color[] getPixels(Texture2D tex)
        {
            var renderTex = RenderTexture.GetTemporary(
                tex.width,
                tex.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(tex, renderTex);
            var t = renderTex.CreateTexture2D();
            renderTex.Release();
            var p = t.GetPixels();
            t.Release();
            return p;
        }

        private static string trimStr(string str)
        {
            if (str.Length <= 10) return str;
            return str.Substring(0, 8) + "..";
        }

        private static string shortStr(string str)
        {
            if (!str.Contains(" ")) return str.Substring(0, 3).CapitalizeFirst();
            else
            {
                var s = "" + str[0];
                var flag = false;
                for (var i = 1; i < str.Length; i++)
                {
                    if (str[i] == ' ')
                    {
                        flag = true;
                        continue;
                    }
                    if (flag) s += str[i];
                    flag = false;
                }
                return s.ToUpper();
            }
        }
    }
}