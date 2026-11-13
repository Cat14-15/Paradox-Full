using System.Collections.Generic;

namespace Paradox.Language
{
    /// <summary>
    /// 한 플레이어(뷰어)의 "이해 상태". 게임의 핵심 성장 요소.
    ///
    /// - 자기 모국어(nativeLanguageId)의 단어는 항상 이해한다.
    /// - 다른 언어의 단어는 처음엔 못 읽고, 협동에 성공하면서 하나씩 "배운다".
    ///   Learn(다른언어, 개념) → 그 언어의 그 개념 뜻을 이제 이해하게 된다.
    ///
    /// 이 사전이 채워질수록 소통이 원활해지는 것 = "협동하니 점점 통한다"는
    /// 게임 주제를 그대로 표현한다.
    /// </summary>
    public class PlayerPhrasebook
    {
        public string nativeLanguageId;

        // 배운 항목: "언어id:개념id" 형태로 저장
        private readonly HashSet<string> _learned = new HashSet<string>();

        public PlayerPhrasebook(string nativeLanguageId)
        {
            this.nativeLanguageId = nativeLanguageId;
        }

        private static string Key(string languageId, string conceptId) => languageId + ":" + conceptId;

        /// <summary>해당 언어의 해당 개념 뜻을 배운다. (협동 성공 시 호출)</summary>
        public void Learn(string languageId, string conceptId)
        {
            _learned.Add(Key(languageId, conceptId));
        }

        /// <summary>여러 언어에서 같은 개념을 한 번에 배우고 싶을 때(선택).</summary>
        public void LearnConceptInAllLanguages(LanguageDatabase db, string conceptId)
        {
            foreach (var lang in db.languages)
                _learned.Add(Key(lang.id, conceptId));
        }

        /// <summary>이 뷰어가 (보낸 언어의) 개념 뜻을 이해하는가?</summary>
        public bool Understands(string senderLanguageId, string conceptId, LanguageDatabase db)
        {
            var concept = db.GetConcept(conceptId);
            if (concept != null && concept.universal) return true;      // 이모트 등은 항상
            if (senderLanguageId == nativeLanguageId) return true;      // 내 모국어는 항상
            return _learned.Contains(Key(senderLanguageId, conceptId)); // 배운 것만
        }

        public int LearnedCount => _learned.Count;
    }
}
