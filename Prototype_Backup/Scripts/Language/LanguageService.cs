using System.Collections.Generic;
using System.Text;

namespace Paradox.Language
{
    /// <summary>
    /// 언어 시스템의 중심 API. UI/네트워킹 계층은 이 클래스만 알면 된다.
    ///
    /// 같은 메시지라도 "누가 보느냐"에 따라 다르게 렌더링하는 것이 핵심:
    ///  - 보낸 사람 화면: 자기 언어라 전부 이해됨
    ///  - 받는 사람 화면: 낯선 기호만 보이고, 배운 개념만 뜻이 드러남
    /// </summary>
    public class LanguageService
    {
        private readonly LanguageDatabase _db;

        public LanguageService(LanguageDatabase db)
        {
            _db = db;
        }

        public LanguageDatabase Database => _db;

        /// <summary>메시지를 특정 뷰어(phrasebook) 기준으로 토큰 단위로 렌더링한다.</summary>
        public List<RenderedToken> Render(GameMessage message, PlayerPhrasebook viewer)
        {
            var result = new List<RenderedToken>(message.conceptIds.Count);
            foreach (var conceptId in message.conceptIds)
            {
                var concept = _db.GetConcept(conceptId);
                bool understood = viewer.Understands(message.senderLanguageId, conceptId, _db);

                result.Add(new RenderedToken
                {
                    conceptId = conceptId,
                    glyph = _db.GetGlyph(message.senderLanguageId, conceptId),
                    understood = understood,
                    meaning = understood && concept != null ? concept.meaning : string.Empty
                });
            }
            return result;
        }

        /// <summary>
        /// 렌더링 결과를 사람이 읽을 수 있는 한 줄 문자열로 만든다(디버그·콘솔용).
        /// 이해 못 한 토큰은 기호만, 이해한 토큰은 "기호(뜻)" 형태로 보여준다.
        /// </summary>
        public string RenderToString(GameMessage message, PlayerPhrasebook viewer)
        {
            var tokens = Render(message, viewer);
            var sb = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                var t = tokens[i];
                sb.Append(t.understood ? $"{t.glyph}(={t.meaning})" : $"{t.glyph}(?)");
            }
            return sb.ToString();
        }
    }
}
