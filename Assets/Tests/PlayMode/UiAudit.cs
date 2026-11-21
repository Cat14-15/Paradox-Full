using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Paradox.Client;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Tests
{
    /// <summary>
    /// 화면의 도트 글자(PixelText)가 자기 칸이나 감싼 패널 밖으로 삐져나오는지 찾는다(테스트가 화면을 찍을 때마다).
    /// 결과는 PARADOX_SHOTS 폴더의 ui_overflow.txt 에 쌓인다. 실패시키지는 않고 목록만 남긴다(눈으로 확인하고 고치기).
    /// </summary>
    public static class UiAudit
    {
        private static readonly PropertyInfo Eff = typeof(PixelText).GetProperty("EffScale", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Gap = typeof(PixelText).GetField("_lineGap", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool Visible(Component c)
        {
            if (!c.gameObject.activeInHierarchy) return false;
            for (var t = c.transform; t != null; t = t.parent)
            {
                var g = t.GetComponent<CanvasGroup>();
                if (g != null && g.alpha < 0.05f) return false;
            }
            return true;
        }

        /// <summary>글자가 실제로 그려지는 범위(월드 좌표).</summary>
        private static Rect TextWorld(PixelText t)
        {
            var rt = t.rectTransform;
            int s = Eff != null ? (int)Eff.GetValue(t) : t.scale;
            int gap = Gap != null ? (int)Gap.GetValue(t) : 4;
            string[] lines = (t.text ?? "").Split('\n');
            int w = 0; foreach (var l in lines) w = Mathf.Max(w, PixelFontData.Measure(l, t.spacing));
            float cw = w * s, ch = (lines.Length * PixelFontData.Cell + (lines.Length - 1) * gap) * s;
            Rect r = rt.rect;
            float x = t.align == TextAnchor.UpperLeft || t.align == TextAnchor.MiddleLeft || t.align == TextAnchor.LowerLeft ? r.xMin
                    : t.align == TextAnchor.UpperRight || t.align == TextAnchor.MiddleRight || t.align == TextAnchor.LowerRight ? r.xMax - cw
                    : r.center.x - cw * 0.5f;
            float y = t.align == TextAnchor.UpperLeft || t.align == TextAnchor.UpperCenter || t.align == TextAnchor.UpperRight ? r.yMax - ch
                    : t.align == TextAnchor.LowerLeft || t.align == TextAnchor.LowerCenter || t.align == TextAnchor.LowerRight ? r.yMin
                    : r.center.y - ch * 0.5f;
            Vector3 a = rt.TransformPoint(new Vector3(x, y)), b = rt.TransformPoint(new Vector3(x + cw, y + ch));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Rect World(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>지금 화면을 점검해 문제 목록을 돌려준다(없으면 빈 목록).</summary>
        public static int Checked, InPanel;

        public static List<string> Check()
        {
            var found = new List<string>();
            Checked = 0; InPanel = 0;
            foreach (var t in Object.FindObjectsByType<PixelText>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(t.text) || !Visible(t) || t.color.a < 0.05f) continue;
                Checked++;
                Rect tw = TextWorld(t);
                float unit = Mathf.Max(0.0001f, t.rectTransform.lossyScale.x);   // 캔버스 1단위 = 월드 몇
                // 감싼 패널(보이는 그림이 있는 가장 가까운 부모)
                for (var p = t.transform.parent; p != null; p = p.parent)
                {
                    if (p.GetComponent<Canvas>() != null) break;
                    var img = p.GetComponent<Image>();
                    if (img == null || !img.enabled || img.color.a < 0.3f)
                    {
                        // 박물관 패널(MuseumUI.Panel)은 바탕 그림이 자식 "Back" 에 있다
                        var back = p.Find("Back");
                        img = back != null ? back.GetComponent<Image>() : null;
                        if (img == null || !img.enabled || img.color.a < 0.3f) continue;
                    }
                    Rect pw = World((RectTransform)p);
                    InPanel++;
                    float over = Mathf.Max(pw.xMin - tw.xMin, tw.xMax - pw.xMax, pw.yMin - tw.yMin, tw.yMax - pw.yMax) / unit;
                    if (over > 4f && pw.width / unit < 3000f)
                        found.Add(Path(t.transform) + "  패널(" + p.name + ") 밖으로 " + Mathf.RoundToInt(over) + "  \"" + Short(t.text) + "\"");
                    break;
                }
            }
            // 패널끼리 겹침: 캔버스 바로 아래의 보이는 패널(바탕 그림이 있는 것)끼리
            var panels = new List<(RectTransform r, Rect w, string name)>();
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!cv.isRootCanvas || !cv.gameObject.activeInHierarchy || cv.name == "DarkCanvas") continue;   // 어둠(정전) 판은 일부러 덮는다
                float unit = Mathf.Max(0.0001f, cv.transform.lossyScale.x);
                foreach (Transform ch in cv.transform)
                {
                    var r = ch as RectTransform;
                    if (r == null || !Visible(r)) continue;
                    var img = r.GetComponent<Image>(); var back = r.Find("Back");
                    bool drawn = (img != null && img.enabled && img.color.a > 0.3f) || (back != null && back.GetComponent<Image>() != null);
                    if (!drawn) continue;
                    Rect w = World(r);
                    if (w.width / unit > 1700f || w.height / unit > 950f) continue;   // 화면 전체 덮개 · 큰 창은 제외(일부러 위에 뜬다)
                    panels.Add((r, w, cv.name + "/" + r.name));
                }
            }
            for (int i = 0; i < panels.Count; i++)
                for (int j = i + 1; j < panels.Count; j++)
                {
                    Rect a = panels[i].w, b = panels[j].w;
                    float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    float unit = Mathf.Max(0.0001f, panels[i].r.lossyScale.x);
                    if (ox / unit > 6f && oy / unit > 6f) found.Add("겹침  " + panels[i].name + "  ↔  " + panels[j].name + "  (" + Mathf.RoundToInt(ox / unit) + "x" + Mathf.RoundToInt(oy / unit) + ")");
                }
            // 누를 수 있는 것(단추)끼리 겹침 — 부모가 달라도(로비 스피드런 ↔ 설정 같은 경우)
            var btns = new List<(RectTransform r, Rect w)>();
            foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                var r = g.rectTransform;
                bool clickable = r.GetComponent<Selectable>() != null || r.GetComponent<PointerRelay>() != null || r.name.Contains("Button") || r.name.StartsWith("Btn");
                if (!clickable || !Visible(r) || btns.Exists(b => b.r == r)) continue;
                btns.Add((r, World(r)));
            }
            for (int i = 0; i < btns.Count; i++)
                for (int j = i + 1; j < btns.Count; j++)
                {
                    if (btns[i].r.IsChildOf(btns[j].r) || btns[j].r.IsChildOf(btns[i].r)) continue;
                    if (btns[i].r.root != btns[j].r.root) continue;   // 다른 캔버스(위에 뜬 창 아래 깔린 단추)는 제외
                    Rect a = btns[i].w, b = btns[j].w;
                    float unit = Mathf.Max(0.0001f, btns[i].r.lossyScale.x);
                    float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (ox / unit > 4f && oy / unit > 4f) found.Add("단추 겹침  " + Path(btns[i].r) + "  ↔  " + Path(btns[j].r));
                }
            return found;
        }

        public static void Report(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            var list = Check();
            var sb = new StringBuilder();
            sb.Append("== ").Append(name).Append("  글자 ").Append(Checked).Append(" 패널안 ").Append(InPanel).Append("  (").Append(list.Count).Append(")").Append((char)10);
            foreach (var l in list) sb.Append("  ").Append(l).Append('\n');
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "ui_overflow.txt"), sb.ToString());
        }

        private static string Short(string s) { s = s.Replace('\n', ' '); return s.Length > 40 ? s.Substring(0, 40) + "…" : s; }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; t != null && i < 4; i++, t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }
    }
}
