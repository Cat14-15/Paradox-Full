using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Paradox.Language
{
    /// <summary>하나의 "개념". 게임 세계의 진짜 의미(ground truth) 단위.</summary>
    [Serializable]
    public class ConceptEntry
    {
        [Tooltip("코드에서 참조할 고유 id (예: DOOR, GO_RIGHT)")]
        public string id;

        public ConceptCategory category;

        [Tooltip("개발/디버그용, 그리고 플레이어가 '이해'했을 때 보여줄 뜻 (한국어)")]
        public string meaning;

        [Tooltip("이모트처럼 언어 장벽 없이 항상 통하는 개념이면 true")]
        public bool universal;
    }

    /// <summary>하나의 "언어". 각 플레이어에게 하나씩 배정된다.</summary>
    [Serializable]
    public class LanguageEntry
    {
        [Tooltip("고유 id (예: solari)")]
        public string id;

        [Tooltip("화면 표시용 이름")]
        public string displayName;

        [Tooltip("이 언어의 색조 — UI 구분용")]
        public Color tint = Color.white;

        [Tooltip("이 언어가 단어를 만들 때 쓰는 음절 뱅크")]
        public List<string> syllables = new List<string>();

        [Tooltip("결정적 단어 생성을 위한 시드")]
        public int seed = 1;
    }

    /// <summary>
    /// 게임의 모든 개념과 언어를 담는 데이터베이스.
    /// 에디터에서 애셋으로 만들 수도 있고, 코드에서 기본값을 생성할 수도 있다
    /// (CreateDefault). 각 언어가 개념마다 서로 다른 "가짜 단어"를 갖도록
    /// 결정적으로(같은 입력이면 항상 같은 결과) 글리프를 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "LanguageDatabase", menuName = "Paradox/Language Database")]
    public class LanguageDatabase : ScriptableObject
    {
        public List<ConceptEntry> concepts = new List<ConceptEntry>();
        public List<LanguageEntry> languages = new List<LanguageEntry>();

        private Dictionary<string, int> _conceptIndex;
        private Dictionary<string, LanguageEntry> _languageById;

        private void BuildLookups()
        {
            _conceptIndex = new Dictionary<string, int>();
            for (int i = 0; i < concepts.Count; i++)
            {
                if (!string.IsNullOrEmpty(concepts[i].id))
                    _conceptIndex[concepts[i].id] = i;
            }

            _languageById = new Dictionary<string, LanguageEntry>();
            foreach (var lang in languages)
            {
                if (!string.IsNullOrEmpty(lang.id))
                    _languageById[lang.id] = lang;
            }
        }

        public ConceptEntry GetConcept(string conceptId)
        {
            if (_conceptIndex == null) BuildLookups();
            return _conceptIndex.TryGetValue(conceptId, out int i) ? concepts[i] : null;
        }

        public LanguageEntry GetLanguage(string languageId)
        {
            if (_languageById == null) BuildLookups();
            return _languageById.TryGetValue(languageId, out var lang) ? lang : null;
        }

        /// <summary>
        /// 주어진 언어에서 특정 개념이 어떤 "가짜 단어"로 보이는지 결정적으로 생성한다.
        /// 예: solari 언어의 DOOR → "kanu", verda 언어의 DOOR → "shelo".
        /// 이모트 등 universal 개념은 언어와 무관하게 뜻(meaning)을 그대로 보여준다.
        /// </summary>
        public string GetGlyph(string languageId, string conceptId)
        {
            if (_conceptIndex == null) BuildLookups();

            var concept = GetConcept(conceptId);
            if (concept == null) return "?";
            if (concept.universal) return concept.meaning; // 항상 통하는 개념

            var lang = GetLanguage(languageId);
            if (lang == null || lang.syllables.Count == 0) return "?";

            int conceptIdx = _conceptIndex[conceptId];
            return MakeWord(lang, conceptIdx);
        }

        // 언어 시드 + 개념 인덱스로부터 2~3음절짜리 결정적 가짜 단어를 만든다.
        private static string MakeWord(LanguageEntry lang, int conceptIndex)
        {
            int hash = Hash(conceptIndex * 92821 + lang.seed * 40499);
            int syllableCount = 2 + (hash % 2); // 2 또는 3음절

            var sb = new StringBuilder();
            int bank = lang.syllables.Count;
            for (int k = 0; k < syllableCount; k++)
            {
                hash = Hash(hash + k * 2749);
                sb.Append(lang.syllables[hash % bank]);
            }
            return sb.ToString();
        }

        // 간단한 결정적 정수 해시 (양수 반환).
        private static int Hash(int x)
        {
            unchecked
            {
                x = (x ^ 61) ^ (x >> 16);
                x = x + (x << 3);
                x = x ^ (x >> 4);
                x = x * 0x27d4eb2d;
                x = x ^ (x >> 15);
            }
            return x & 0x7fffffff;
        }

        /// <summary>
        /// 코드에서 기본 데이터셋을 만든다. 애셋을 손으로 만들지 않아도
        /// 바로 테스트/실행할 수 있게 하기 위한 것.
        /// </summary>
        public static LanguageDatabase CreateDefault()
        {
            var db = CreateInstance<LanguageDatabase>();

            void C(string id, ConceptCategory cat, string meaning, bool universal = false)
                => db.concepts.Add(new ConceptEntry { id = id, category = cat, meaning = meaning, universal = universal });

            // --- 방향 ---
            C("UP", ConceptCategory.Direction, "위");
            C("DOWN", ConceptCategory.Direction, "아래");
            C("LEFT", ConceptCategory.Direction, "왼쪽");
            C("RIGHT", ConceptCategory.Direction, "오른쪽");
            // --- 색 ---
            C("RED", ConceptCategory.Color, "빨강");
            C("BLUE", ConceptCategory.Color, "파랑");
            C("YELLOW", ConceptCategory.Color, "노랑");
            C("GREEN", ConceptCategory.Color, "초록");
            // --- 수 ---
            C("ONE", ConceptCategory.Number, "하나");
            C("TWO", ConceptCategory.Number, "둘");
            C("THREE", ConceptCategory.Number, "셋");
            // --- 사물 ---
            C("DOOR", ConceptCategory.Object, "문");
            C("KEY", ConceptCategory.Object, "열쇠");
            C("LEVER", ConceptCategory.Object, "레버");
            C("BUTTON", ConceptCategory.Object, "버튼");
            C("BRIDGE", ConceptCategory.Object, "다리");
            // --- 동작·의사 ---
            C("GO", ConceptCategory.Action, "가");
            C("STOP", ConceptCategory.Action, "멈춰");
            C("WAIT", ConceptCategory.Action, "기다려");
            C("HELP", ConceptCategory.Action, "도와줘");
            C("DANGER", ConceptCategory.Action, "위험");
            C("YES", ConceptCategory.Action, "응");
            C("NO", ConceptCategory.Action, "아니");
            // --- 이모트 (언어 장벽 없이 항상 통함) ---
            C("EMOTE_HAPPY", ConceptCategory.Emote, "😊", universal: true);
            C("EMOTE_CONFUSED", ConceptCategory.Emote, "❓", universal: true);
            C("EMOTE_SORRY", ConceptCategory.Emote, "🙏", universal: true);

            // --- 언어 3종 (플레이어당 하나) ---
            db.languages.Add(new LanguageEntry
            {
                id = "solari",
                displayName = "Solari",
                tint = new Color(1f, 0.78f, 0.28f),
                seed = 7,
                syllables = new List<string> { "ka", "so", "ri", "lu", "na", "to", "mi", "va" }
            });
            db.languages.Add(new LanguageEntry
            {
                id = "verda",
                displayName = "Verda",
                tint = new Color(0.42f, 0.82f, 0.55f),
                seed = 23,
                syllables = new List<string> { "she", "lo", "vin", "da", "ru", "en", "ba", "ti" }
            });
            db.languages.Add(new LanguageEntry
            {
                id = "nokta",
                displayName = "Nokta",
                tint = new Color(0.55f, 0.62f, 0.95f),
                seed = 51,
                syllables = new List<string> { "zu", "mor", "ek", "ta", "pi", "on", "gah", "il" }
            });

            db.BuildLookups();
            return db;
        }
    }
}
