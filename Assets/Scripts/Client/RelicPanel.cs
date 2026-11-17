using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 퍼즐 3 화면(씬에 놓인 UI): 유물 보관실. 역할마다 다른 면.
    ///  조작자 — 선반의 유물을 한 번에 하나씩 들어 받침대에 올린다 / 통역 — 시대 이름 번역표 / 관측자 — 자외선등(복제품) / 내비게이터 — 받침대 배치도.
    /// 규칙 판정은 RelicPuzzle(순수 규칙).
    /// </summary>
    public sealed class RelicPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool Cleared { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; Cleared = false; }

        [Header("공통")]
        public CanvasGroup group;
        public RectTransform content;
        public PixelText title, roleText, hint, status;
        public Image flash;

        [Header("조작자 면")]
        public GameObject viewOperator;
        public RectTransform[] shelfRects = new RectTransform[5];
        public GameObject[] shelfItems = new GameObject[5];
        public PixelText[] shelfTags = new PixelText[5];
        public RectTransform[] pedRects = new RectTransform[4];
        public GameObject[] pedItems = new GameObject[4];
        public PixelText[] pedTags = new PixelText[4];
        public RectTransform cursor;
        public RectTransform heldBox;
        public PixelText heldTag;

        [Header("통역 면")]
        public GameObject viewInterpreter;
        public PixelText[] glossTl = new PixelText[4];
        public PixelText[] glossKo = new PixelText[4];

        [Header("관측자 면")]
        public GameObject viewObserver;
        public Image[] uvBoxes = new Image[5];
        public PixelText[] uvTexts = new PixelText[5];
        public PixelText[] uvSubs = new PixelText[5];

        [Header("내비게이터 면")]
        public GameObject viewNavigator;
        public PixelText[] navTexts = new PixelText[4];
        public PixelText[] navSubs = new PixelText[4];

        private static readonly Color Gold = new Color(0.949f, 0.773f, 0.447f);
        private RelicCase _owner;
        private RelicPuzzle _puzzle;
        private readonly int[] _placed = { -1, -1, -1, -1 };
        private int _held = -1, _row, _col;
        private float _shake, _flashA, _closeAt = -1f, _stunUntil;
        private Color _flashCol = Color.white;
        private int _openedFrame;
        private bool _solved;
        private int _sayVer = -1;
        private int _fails;

        private void Awake() { if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; } }

        public void Open(RelicCase owner, RelicPuzzle puzzle)
        {
            _owner = owner; _puzzle = puzzle;
            ResetAll(); _row = 0; _col = 0; _solved = false; _closeAt = -1f; _flashA = 0f; _stunUntil = 0f;
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            IsOpen = true; Story1BotOperator.Opened(Story1Net.PRelic); _openedFrame = Time.frameCount; _sayVer = -1; _fails = 0; Story1Net.ResetSent(Story1Net.PRelic);
            status.text = "";
            Refresh();
            Sfx.Play("ui_select", 0.8f);
        }

        /// <summary>조작자가 AI 일 때: 동료의 신호를 받아 AI 조작자가 푼다.</summary>
        private void BotSolve()
        {
            if (_solved) return;
            _solved = true;
            status.text = "AI 조작자가 신호대로 풀었어요!";
            status.color = new Color(0.45f, 0.95f, 0.6f);
            _flashCol = new Color(0.5f, 1f, 0.6f); _flashA = 0.5f;
            Sfx.Play("power_on", 0.9f);
            _closeAt = Time.unscaledTime + 1.5f;
            Story1Net.Send(Story1Net.Clear, Story1Net.PRelic);
        }

        private float _botNote;

        private void BotOperatorTick()
        {
            if (_solved) return;
            string waiting; int st = Story1BotOperator.Tick(Story1Net.PRelic, out waiting);
            if (st == 1 && Time.unscaledTime > _botNote) { _botNote = Time.unscaledTime + 1f; status.text = waiting.Length == 0 ? "AI 조작자가 신호대로 푸는 중이에요" : "AI 조작자가 신호를 기다려요: " + waiting + " 의 신호 카드가 필요해요 (C)"; status.color = new Color(0.95f, 0.85f, 0.55f); }
            else if (st == 2) BotSolve();
        }

        public void Close()
        {
            IsOpen = false;
            SoloBots.Hide();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            Sfx.Play("ui_back", 0.8f);
        }

        private void ResetAll() { for (int i = 0; i < 4; i++) _placed[i] = -1; _held = -1; }

        private bool OnShelf(int s) => s != _held && System.Array.IndexOf(_placed, s) < 0;

        private void Refresh()
        {
            int role = Story1Role.Current;
            bool op = role == (int)GameRole.Operator, tr = role == (int)GameRole.Interpreter, ob = role == (int)GameRole.Observer, nv = role == (int)GameRole.Navigator;
            viewOperator.SetActive(op); viewInterpreter.SetActive(tr); viewObserver.SetActive(ob); viewNavigator.SetActive(nv);
            roleText.text = "역할 · " + Roles.Name(role) + (Story1Role.Solo ? "    [Tab] 역할 바꾸기 (혼자 테스트)" : "");
            roleText.color = Roles.Color(role);

            if (op)
            {
                title.text = "수장고 · 유물과 받침대" + Story1Net.RoundLabel(Story1Net.PRelic);
                hint.text = "← → ↑ ↓  고르기     Enter  들기 / 올리기     하나씩만 들어요";
                for (int s = 0; s < 5; s++)
                {
                    shelfItems[s].SetActive(OnShelf(s));
                    shelfTags[s].text = RelicPuzzle.EraTl[_puzzle.eraOf[s]];
                }
                for (int p = 0; p < 4; p++)
                {
                    int s = _placed[p];
                    pedItems[p].SetActive(s >= 0);
                    if (s >= 0) pedTags[p].text = RelicPuzzle.EraTl[_puzzle.eraOf[s]];
                }
                heldBox.anchoredPosition = new Vector2(625f, -135f);
                heldBox.gameObject.SetActive(_held >= 0);
                if (_held >= 0) heldTag.text = RelicPuzzle.EraTl[_puzzle.eraOf[_held]];
                var target = _row == 0 ? shelfRects[Mathf.Clamp(_col, 0, 4)] : pedRects[Mathf.Clamp(_col, 0, 3)];
                cursor.anchoredPosition = target.anchoredPosition;
                cursor.sizeDelta = target.sizeDelta + new Vector2(24f, 24f);
                cursor.gameObject.SetActive(!_solved);
            }
            if (tr)
            {
                title.text = "유물 이름표 번역 (필리핀어)";
                for (int i = 0; i < 4; i++) { glossTl[i].text = RelicPuzzle.EraTl[i]; glossKo[i].text = "→ " + (i + 1) + "번째로 오래됨 · " + RelicPuzzle.EraKo[i]; }
                hint.text = "이 글은 당신만 읽을 수 있어요. 팀원에게 이름표의 시대를 알려 주세요.";
            }
            if (ob)
            {
                title.text = "자외선등 아래의 유물";
                for (int s = 0; s < 5; s++)
                {
                    bool f = s == _puzzle.fake;
                    uvBoxes[s].color = f ? new Color(0.55f, 0.25f, 0.85f) : new Color(0.14f, 0.17f, 0.22f);
                    uvTexts[s].text = (s + 1) + "번 칸";
                    uvSubs[s].text = f ? "복제품" : "진품";
                    uvSubs[s].color = f ? new Color(0.95f, 0.85f, 1f) : new Color(0.6f, 0.65f, 0.7f);
                    uvTexts[s].color = f ? new Color(0.95f, 0.85f, 1f) : new Color(0.6f, 0.65f, 0.7f);
                }
                hint.text = "복제품은 자외선에서 빛나요. 어느 칸인지 팀원에게 알려 주세요.";
            }
            if (nv)
            {
                title.text = "받침대 배치도";
                for (int p = 0; p < 4; p++) { navTexts[p].text = (p + 1) + "번 받침대"; navSubs[p].text = "오래된 순 " + (_puzzle.pedestalEra[p] + 1) + "번"; }
                hint.text = "받침대마다 올릴 유물 순서예요 (1번째가 가장 오래됨)";
            }
            if (hint != null) { hint.maxWidth = 1420f; hint.text += "\nAI 말뜻  1 → 2 1번 칸은 오래된 순서 2번째 · N 아니 N번 칸 복제품"; }
            SoloBots.Sync(BotLines.Relic(_puzzle));
            if (op && !_solved) { int n = 0; foreach (int s in _placed) if (s >= 0) n++; Story1Net.Tell(Story1Net.PRelic, "조작자: 받침대에 유물 " + n + " / 4개"); }
        }

        private void Update()
        {
            if (content != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 30f);
                content.anchoredPosition = _shake > 0.01f ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _shake + new Vector2(0f, 60f) : new Vector2(0f, 60f);
            }
            if (flash != null)
            {
                _flashA = Mathf.MoveTowards(_flashA, 0f, Time.unscaledDeltaTime * 2.2f);
                var c = _flashCol; c.a = _flashA; flash.color = c;
            }
            if (!IsOpen) return;
            Story1Net.ShowSay(Story1Net.PRelic, status, ref _sayVer);
            if (Story1BotOperator.Active) BotOperatorTick();
            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) { Close(); _owner.OnSolved(); return; }
            if (_solved || Time.frameCount == _openedFrame) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) { Close(); return; }   // 웹에서는 Esc 가 전체화면을 풀어 버려서 X·Q 도 쓴다
            if (kb.tabKey.wasPressedThisFrame && Story1Role.Solo) { Story1Role.Cycle(); Sfx.Play("ui_hover", 0.8f); Refresh(); return; }
            if (Story1Role.Current != (int)GameRole.Operator || Time.unscaledTime < _stunUntil) return;

            int max = _row == 0 ? 4 : 3;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { _col = Mathf.Max(0, _col - 1); Move(); }
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { _col = Mathf.Min(max, _col + 1); Move(); }
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { _row = 0; _col = Mathf.Min(_col, 4); Move(); }
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { _row = 1; _col = Mathf.Min(_col, 3); Move(); }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Act();
        }

        private void Move() { Sfx.Play("ui_hover", 0.7f); Refresh(); }

        private void Act()
        {
            if (_row == 0)
            {
                // 선반: 들고 있으면 되돌려 놓고, 비었으면 이 칸의 유물을 든다
                if (_held >= 0) { _held = -1; Sfx.Play("ui_back", 0.7f); }
                else if (OnShelf(_col)) { _held = _col; Sfx.Play("ui_select", 0.7f); }
            }
            else
            {
                int cur = _placed[_col];
                if (_held >= 0 && cur < 0) { _placed[_col] = _held; _held = -1; Sfx.Play("ui_select", 0.8f); }
                else if (_held < 0 && cur >= 0) { _held = cur; _placed[_col] = -1; Sfx.Play("ui_back", 0.7f); }   // 올려 둔 걸 다시 든다
                else if (_held >= 0 && cur >= 0) { status.text = "받침대가 이미 차 있어요."; status.color = new Color(0.95f, 0.8f, 0.45f); Sfx.Play("ui_back", 0.6f); }
                if (_held < 0 && RelicPuzzle.IsFull(_placed)) { Submit(); return; }
            }
            if (status.text == "받침대가 이미 차 있어요." && _held < 0) status.text = "";
            Refresh();
        }

        private void Submit()
        {
            if (_puzzle.Check(_placed))
            {
                _solved = true;
                status.text = "열쇠 보관함이 열렸다! 코드 조각을 얻었다.";
                status.color = new Color(0.45f, 0.95f, 0.6f);
                _flashCol = new Color(1f, 0.9f, 0.5f); _flashA = 0.6f;
                Sfx.Play("power_on", 0.9f);
                _closeAt = Time.unscaledTime + 1.6f;
                Story1Net.Send(Story1Net.Clear, Story1Net.PRelic);
            }
            else
            {
                status.text = "유물이 튕겨 나왔다! 시대와 진품을 다시 확인하자.";
                status.color = new Color(1f, 0.5f, 0.45f);
                _fails++;
                if (_fails >= System.Math.Max(1, Story1Rules.HintAfter(Story1Net.Difficulty) - 1)) { status.text = "힌트: 복제품은 " + (_puzzle.fake + 1) + "번 칸이다"; status.color = new Color(0.95f, 0.85f, 0.45f); }
                _flashCol = new Color(1f, 0.35f, 0.3f); _flashA = 0.6f; _shake = 12f;
                _stunUntil = Time.unscaledTime + 2.0f;
                Sfx.Play("fuse_pop", 1f);
                ResetAll();
                Refresh();
                Story1Net.Tell(Story1Net.PRelic, "조작자: 유물이 튕겨 나왔다!");
                return;
            }
            Refresh();
        }

        internal static void ResetState() { Cleared = false; IsOpen = false; }
        internal static void MarkCleared() { Cleared = true; }
    }
}
