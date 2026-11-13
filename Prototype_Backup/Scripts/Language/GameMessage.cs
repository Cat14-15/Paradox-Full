using System.Collections.Generic;

namespace Paradox.Language
{
    /// <summary>
    /// 한 플레이어가 보낸 메시지. "개념 id들의 나열 + 보낸 사람의 언어".
    /// 네트워크로 주고받을 때도 이 가벼운 데이터(문자열 목록)만 전송하면 된다.
    /// 각 수신자 화면에서 자기 상황(배운 단어)에 맞게 렌더링된다.
    /// </summary>
    public class GameMessage
    {
        public string senderLanguageId;
        public List<string> conceptIds = new List<string>();

        public GameMessage(string senderLanguageId)
        {
            this.senderLanguageId = senderLanguageId;
        }

        public GameMessage Add(string conceptId)
        {
            conceptIds.Add(conceptId);
            return this;
        }
    }

    /// <summary>메시지의 한 토큰을 특정 뷰어가 어떻게 보는지 계산된 결과.</summary>
    public struct RenderedToken
    {
        public string conceptId;
        public string glyph;      // 화면에 보이는 (보낸 사람 언어의) 기호/단어. 항상 보인다.
        public bool understood;   // 이 뷰어가 뜻을 이해하는가?
        public string meaning;    // understood == true 일 때의 뜻(한국어). 아니면 비어 있음.
    }
}
