using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 한국어 표시가 가능한 폰트를 런타임에 확보한다.
    /// 개발 중엔 OS 폰트(맑은 고딕)를 쓰고, 최종 빌드에선 한글 TTF 애셋으로 교체 권장.
    /// </summary>
    public static class UIFontHelper
    {
        private static Font _font;

        public static Font Load()
        {
            if (_font != null) return _font;
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial Unicode MS", "Arial" }, 18);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }
}
