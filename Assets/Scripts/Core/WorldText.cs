using UnityEngine;

namespace Paradox.Core
{
    /// <summary>월드 공간에 떠 있는 글자(TextMesh) 생성 헬퍼. 이름표·표지판·말풍선에 쓴다.</summary>
    public static class WorldText
    {
        public static TextMesh Make(Transform parent, string text, Vector3 localPos, float characterSize, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter, int fontSize = 64)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            var font = UIFontHelper.Load();
            tm.font = font;
            tm.characterSize = characterSize;
            tm.fontSize = fontSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            tm.richText = true;
            var mr = go.GetComponent<MeshRenderer>();
            mr.material = font.material;
            mr.sortingOrder = order;
            return tm;
        }
    }
}
