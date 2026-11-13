using UnityEngine;

namespace Paradox.Language
{
    /// <summary>
    /// 언어 시스템 코어를 콘솔에서 시연/검증하는 스크립트.
    /// 빈 GameObject에 붙여 Play 하면, 같은 메시지가 플레이어마다
    /// 어떻게 다르게 보이는지 + 협동(배우기) 후 어떻게 통하기 시작하는지를
    /// Console 창에 출력한다. (아직 UI는 없음 — 로직 검증용)
    ///
    /// 실행 중 L 키: 플레이어 B가 개념 하나를 "배운다" (협동 성공 시뮬레이션)
    /// </summary>
    public class LanguageSystemDemo : MonoBehaviour
    {
        private LanguageService _service;
        private LanguageDatabase _db;

        private PlayerPhrasebook _playerA; // 모국어 Solari
        private PlayerPhrasebook _playerB; // 모국어 Verda

        private GameMessage _message;

        // B가 순서대로 배워나갈 개념들 (L 키로 하나씩)
        private readonly string[] _learnOrder = { "DOOR", "RIGHT", "GO" };
        private int _learnStep;

        private void Start()
        {
            _db = LanguageDatabase.CreateDefault();
            _service = new LanguageService(_db);

            _playerA = new PlayerPhrasebook("solari");
            _playerB = new PlayerPhrasebook("verda");

            // 플레이어 A가 Solari어로 "가 → 오른쪽 → 문" 이라고 말한다.
            _message = new GameMessage("solari").Add("GO").Add("RIGHT").Add("DOOR");

            Debug.Log("<b>[언어 시스템 데모]</b> A(Solari)가 메시지를 보냈습니다: 뜻은 [가, 오른쪽, 문]");
            Debug.Log("실행 중 <b>L 키</b>를 누르면 B가 협동으로 단어를 하나씩 배웁니다.\n");

            PrintBothViews("초기 상태 — B는 아직 Solari어를 전혀 모름");
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.lKey.wasPressedThisFrame)
            {
                if (_learnStep < _learnOrder.Length)
                {
                    string learned = _learnOrder[_learnStep++];
                    _playerB.Learn("solari", learned);
                    string meaning = _db.GetConcept(learned).meaning;
                    string glyph = _db.GetGlyph("solari", learned);
                    PrintBothViews($"B가 협동으로 <b>{meaning}</b> 를 배움 (Solari어 '{glyph}')");
                }
                else
                {
                    Debug.Log("B가 이 메시지의 모든 단어를 배웠습니다. 이제 완전히 통합니다! 🎉");
                }
            }
        }

        private void PrintBothViews(string header)
        {
            string aView = _service.RenderToString(_message, _playerA);
            string bView = _service.RenderToString(_message, _playerB);

            Debug.Log(
                $"── {header} ──\n" +
                $"  <color=#E8B84B>A(Solari, 보낸 사람)가 보는 화면</color>: {aView}\n" +
                $"  <color=#6BD18C>B(Verda, 받는 사람)가 보는 화면</color>: {bView}");
        }
    }
}
