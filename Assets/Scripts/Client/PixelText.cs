using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 도트 글꼴 데이터(Resources/PixelFont/atlas.png + glyphs.txt). Tools/make_pixel_font.py 가 만든다.
    /// 16x16 칸에 글자가 한 칸씩, glyphs.txt 는 "글자&lt;TAB&gt;가로폭" 한 줄씩.
    /// </summary>
    public static class PixelFontData
    {
        public const int Cell = 16, Columns = 64;
        private static Texture2D _tex;
        private static Dictionary<char, int> _index;
        private static Dictionary<char, int> _advance;

        public static Texture2D Texture { get { Load(); return _tex; } }

        private static void Load()
        {
            if (_index != null && _tex != null) return;
            _tex = Resources.Load<Texture2D>("PixelFont/atlas");
            _index = new Dictionary<char, int>();
            _advance = new Dictionary<char, int>();
            var asset = Resources.Load<TextAsset>("PixelFont/glyphs");
            if (asset == null) return;
            string[] lines = asset.text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].TrimEnd('\r');
                if (l.Length < 3) continue;
                char c = l[0];
                int tab = l.IndexOf('\t');
                int adv = 8;
                if (tab > 0) int.TryParse(l.Substring(tab + 1), out adv);
                _index[c] = i;
                _advance[c] = adv;
            }
        }

        public static bool Has(char c) { Load(); return _index.ContainsKey(c); }
        public static int Index(char c) { Load(); return _index.TryGetValue(c, out var i) ? i : -1; }
        public static int Advance(char c) { Load(); return _advance.TryGetValue(c, out var a) ? a : 8; }

        /// <summary>글자 폭 합(도트 단위).</summary>
        public static int Measure(string s, int spacing = 0)
        {
            int w = 0;
            for (int i = 0; i < s.Length; i++) w += Advance(s[i]) + (i > 0 ? spacing : 0);
            return w;
        }
    }

    /// <summary>
    /// 도트 글씨 그래픽. 아틀라스 칸을 정수 배율(scale)로 그려서 어떤 문구든(닉네임 포함) 도트 그대로 보여 준다.
    /// Text 처럼 색(color)·정렬(align)·여러 줄(\n)을 쓴다. 아틀라스에 없는 글자는 건너뛴다.
    /// </summary>
    [AddComponentMenu("UI/Pixel Text")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class PixelText : MaskableGraphic
    {
        [SerializeField, TextArea] private string _text = "";
        [SerializeField] private int _scale = 2;
        [SerializeField] private TextAnchor _align = TextAnchor.MiddleCenter;
        [SerializeField] private int _spacing = 0;
        [SerializeField] private int _lineGap = 4;

        public override Texture mainTexture => PixelFontData.Texture;

        public string text { get => _text; set { if (_text == value) return; _text = value ?? ""; SetVerticesDirty(); } }
        // 같은 값이면 다시 그리지 않는다(화면 코드가 매 프레임 같은 값을 넣어도 렉이 없게)
        public int scale { get => _scale; set { int v = Mathf.Max(1, value); if (_scale == v) return; _scale = v; SetVerticesDirty(); } }
        public TextAnchor align { get => _align; set { if (_align == value) return; _align = value; SetVerticesDirty(); } }
        public int spacing { get => _spacing; set { if (_spacing == value) return; _spacing = value; SetVerticesDirty(); } }

        /// <summary>0 보다 크면: 가장 긴 줄이 이 폭(캔버스 단위)을 넘을 때 글씨 배율을 줄여 UI 밖으로 나가지 않게 한다.</summary>
        public float maxWidth;

        private int EffScale
        {
            get
            {
                int s = _scale;
                if (maxWidth <= 0f) return s;
                int w = 0;
                foreach (var l in (_text ?? "").Split('\n')) w = Mathf.Max(w, PixelFontData.Measure(l, _spacing));
                while (s > 1 && w * s > maxWidth) s--;
                return s;
            }
        }

        protected override void Awake() { base.Awake(); raycastTarget = false; }

        public Vector2 ContentSize
        {
            get
            {
                string[] lines = (_text ?? "").Split('\n');
                int w = 0;
                foreach (var l in lines) w = Mathf.Max(w, PixelFontData.Measure(l, _spacing));
                int h = lines.Length * PixelFontData.Cell + (lines.Length - 1) * _lineGap;
                return new Vector2(w * _scale, h * _scale);
            }
        }

        /// <summary>RectTransform 크기를 글씨 크기에 맞춘다.</summary>
        public void Fit() { var s = ContentSize; rectTransform.sizeDelta = new Vector2(s.x + 2 * _scale, s.y); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var tex = PixelFontData.Texture;
            if (tex == null || string.IsNullOrEmpty(_text)) return;
            string[] lines = _text.Split('\n');
            Rect r = GetPixelAdjustedRect();
            int sc = EffScale;
            float lineH = (PixelFontData.Cell + _lineGap) * sc;
            float totalH = lines.Length * PixelFontData.Cell * sc + (lines.Length - 1) * _lineGap * sc;
            int row = (int)_align / 3;     // 0 위, 1 가운데, 2 아래
            int col = (int)_align % 3;     // 0 왼쪽, 1 가운데, 2 오른쪽
            float top = row == 0 ? r.yMax : row == 1 ? r.center.y + totalH * 0.5f : r.yMin + totalH;
            Color32 c = color;
            float tw = tex.width, th = tex.height;
            float cell = PixelFontData.Cell;
            int vi = 0;
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                float w = PixelFontData.Measure(line, _spacing) * sc;
                float x = col == 0 ? r.xMin : col == 1 ? r.center.x - w * 0.5f : r.xMax - w;
                x = Mathf.Round(x / sc) * sc;
                float y1 = Mathf.Round((top - li * lineH) / sc) * sc;
                float y0 = y1 - cell * sc;
                for (int i = 0; i < line.Length; i++)
                {
                    char ch = line[i];
                    int adv = PixelFontData.Advance(ch);
                    int idx = PixelFontData.Index(ch);
                    if (idx >= 0 && ch != ' ')
                    {
                        int cx = idx % PixelFontData.Columns, cy = idx / PixelFontData.Columns;
                        float u0 = cx * cell / tw, u1 = (cx + 1) * cell / tw;
                        float v1 = 1f - cy * cell / th, v0 = 1f - (cy + 1) * cell / th;
                        float x1 = x + cell * sc;
                        vh.AddVert(new Vector3(x, y0), c, new Vector2(u0, v0));
                        vh.AddVert(new Vector3(x, y1), c, new Vector2(u0, v1));
                        vh.AddVert(new Vector3(x1, y1), c, new Vector2(u1, v1));
                        vh.AddVert(new Vector3(x1, y0), c, new Vector2(u1, v0));
                        vh.AddTriangle(vi, vi + 1, vi + 2);
                        vh.AddTriangle(vi + 2, vi + 3, vi);
                        vi += 4;
                    }
                    x += (adv + _spacing) * sc;
                }
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate() { base.OnValidate(); _scale = Mathf.Max(1, _scale); SetVerticesDirty(); }
#endif
    }
}
