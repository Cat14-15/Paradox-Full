using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 퍼즐 4 화면(씬에 놓인 UI): 물길 돌리기. 역할마다 다른 면.
    ///  조작자 — 밸브 4개(필리핀어 이름표)를 직접 돌린다 / 통역 — 서수 번역표 / 내비게이터 — 파이프 지도(열려야 할 번호) / 관측자 — 밸브 연동.
    /// 수위는 모두가 본다. 시간이 갈수록, 밸브를 돌릴수록, 틀릴수록 오른다.
    /// </summary>
    public sealed class WaterPanel : MonoBehaviour
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
        public RectTransform waterFill;
        public PixelText waterLabel;
        public const float BarHeight = 560f;

        [Header("조작자 면")]
        public GameObject viewOperator;
        public Image[] valveBodies = new Image[4];
        public Image[] valveHandles = new Image[4];
        public PixelText[] valveTags = new PixelText[4];
        public PixelText[] valveStates = new PixelText[4];
        public RectTransform[] valveRects = new RectTransform[4];

        [Header("통역 면")]
        public GameObject viewInterpreter;
        public PixelText[] glossTl = new PixelText[4];
        public PixelText[] glossKo = new PixelText[4];

        [Header("내비게이터 면")]
        public GameObject viewNavigator;
        public Image[] mapNodes = new Image[4];
        public PixelText[] mapLabels = new PixelText[4];

        [Header("관측자 면")]
        public GameObject viewObserver;
        public PixelText[] linkLines = new PixelText[4];

        private static readonly Color ClosedCol = new Color(0.5f, 0.2f, 0.2f), OpenCol = new Color(0.3f, 0.75f, 0.45f);
        private WaterValves _owner;
        private WaterPuzzle _puzzle;
        private readonly bool[] _state = new bool[4];
        private float _water, _shake, _flashA, _closeAt = -1f;
        private Color _flashCol = Color.white;
        private int _openedFrame;
        private bool _solved;
        private int _sayVer = -1;
        private int _fails;
        private float _pumpReadyAt, _seenPump;
        private float _tellAt;

        private void Awake() { if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; } }

        public void Open(WaterValves owner, WaterPuzzle puzzle)
        {
            _owner = owner; _puzzle = puzzle;
            ResetValves(); _water = 0f; _solved = false; _closeAt = -1f; _flashA = 0f;
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            IsOpen = true; _openedFrame = Time.frameCount; _sayVer = -1; _fails = 0; _pumpReadyAt = 0f; _seenPump = Story1Net.PumpUntil; Story1Net.ResetSent(Story1Net.PWater);
            status.text = "";
            Refresh();
            Sfx.Play("ui_select", 0.8f);
        }

        public void Close()
        {
            IsOpen = false;
            SoloBots.Hide();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            Sfx.Play("ui_back", 0.8f);
        }

        private void ResetValves() { for (int i = 0; i < 4; i++) _state[i] = false; }

        private void Refresh()
        {
            int role = Story1Role.Current;
            bool op = role == (int)GameRole.Operator, tr = role == (int)GameRole.Interpreter, ob = role == (int)GameRole.Observer, nv = role == (int)GameRole.Navigator;
            viewOperator.SetActive(op); viewInterpreter.SetActive(tr); viewObserver.SetActive(ob); viewNavigator.SetActive(nv);
            roleText.text = "역할 · " + Roles.Name(role) + (Story1Role.Solo ? "    [Tab] 역할 바꾸기 (혼자 테스트)" : "");
            roleText.color = Roles.Color(role);

            if (op)
            {
                title.text = "관리실 · 배수 밸브";
                hint.text = "1~4  밸브 돌리기 (왼쪽부터)     Enter  수문 확인     밸브는 서로 연동돼 있어요. 돌릴 때마다 물이 찹니다.";
                for (int i = 0; i < 4; i++)
                {
                    valveTags[i].text = WaterPuzzle.TagTl[_puzzle.tagOf[i]];
                    valveStates[i].text = (i + 1) + "  " + (_state[i] ? "열림" : "닫힘");
                    valveStates[i].color = _state[i] ? OpenCol : new Color(0.85f, 0.55f, 0.5f);
                    valveHandles[i].color = _state[i] ? OpenCol : ClosedCol;
                    valveHandles[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, _state[i] ? 0f : 90f);
                }
            }
            if (tr)
            {
                title.text = "밸브 이름표 번역 (필리핀어)";
                for (int i = 0; i < 4; i++) { glossTl[i].text = WaterPuzzle.TagTl[i]; glossKo[i].text = "→ " + WaterPuzzle.TagKo(i); }
                hint.text = "이 글은 당신만 읽을 수 있어요. 밸브의 이름표가 몇 번인지 팀원에게 알려 주세요.";
            }
            if (nv)
            {
                title.text = "파이프 지도";
                var nums = _puzzle.TargetNumbers();
                for (int i = 0; i < 4; i++)
                {
                    bool need = System.Array.IndexOf(nums, i + 1) >= 0;
                    mapNodes[i].color = need ? OpenCol : new Color(0.3f, 0.3f, 0.35f);
                    mapLabels[i].text = (i + 1) + "번  " + (need ? "열림" : "닫힘");
                }
                hint.text = "이 번호의 밸브만 열려 있어야 해요.     Space  배수 펌프 (4초 동안 수위 정지, 12초마다)";
            }
            if (ob)
            {
                title.text = "밸브 연동 (배관 점검)";
                for (int i = 0; i < 4; i++) linkLines[i].text = (i + 1) + "번째를 돌리면 → " + (_puzzle.link[i] + 1) + "번째도 같이 돈다";
                hint.text = "연동을 모르면 엉뚱한 밸브가 열려요. 팀원에게 알려 주세요.";
            }
            if (hint != null) hint.text += "     Q  닫기";
            SoloBots.Sync(BotLines.Water(_puzzle));
        }

        private void Update()
        {
            if (content != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 30f);
                content.anchoredPosition = _shake > 0.01f ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _shake : Vector2.zero;
            }
            if (flash != null)
            {
                _flashA = Mathf.MoveTowards(_flashA, 0f, Time.unscaledDeltaTime * 2.2f);
                var c = _flashCol; c.a = _flashA; flash.color = c;
            }
            if (!IsOpen) return;

            if (!_solved && Story1Net.Now >= Story1Net.PumpUntil) _water = Mathf.Min(WaterPuzzle.MaxWater, _water + Time.unscaledDeltaTime * WaterPuzzle.RisePerSecond);
            if (!_solved && Time.unscaledTime >= _tellAt) { _tellAt = Time.unscaledTime + 1.5f; Story1Net.Tell(Story1Net.PWater, "수위 " + Mathf.RoundToInt(_water / WaterPuzzle.MaxWater * 100f) + "% · 열린 밸브 " + OpenCount() + "개"); }
            Story1Net.ShowSay(Story1Net.PWater, status, ref _sayVer);
            UpdateBar();
            if (!_solved && _water >= WaterPuzzle.MaxWater) { Flood(); return; }

            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) { Close(); _owner.OnSolved(); return; }
            if (_solved || Time.frameCount == _openedFrame) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) { Close(); return; }   // 웹에서는 Esc 가 전체화면을 풀어 버려서 Q 도 쓴다
            if (kb.tabKey.wasPressedThisFrame && Story1Role.Solo) { Story1Role.Cycle(); Sfx.Play("ui_hover", 0.8f); Refresh(); return; }
            if (Story1Net.PumpUntil != _seenPump)
            {
                _seenPump = Story1Net.PumpUntil;
                status.text = "펌프가 돈다! 수위가 잠깐 멈춘다."; status.color = new Color(0.6f, 0.85f, 1f);
                Sfx.Play("ui_select", 0.8f);
            }
            if (Story1Role.Current == (int)GameRole.Navigator && Story1Net.Now >= _pumpReadyAt && kb.spaceKey.wasPressedThisFrame)
            {
                _pumpReadyAt = Story1Net.Now + 12f;
                Story1Net.Send(Story1Net.Pump);
            }
            if (Story1Role.Current != (int)GameRole.Operator) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Press(0);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Press(1);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Press(2);
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Press(3);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Confirm();

            var ms = Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame)
            {
                Vector2 p = ms.position.ReadValue();
                for (int i = 0; i < 4; i++) if (RectTransformUtility.RectangleContainsScreenPoint(valveRects[i], p, null)) { Press(i); return; }
            }
        }

        private int OpenCount() { int n = 0; foreach (bool b in _state) if (b) n++; return n; }

        private void UpdateBar()
        {
            float f = _water / WaterPuzzle.MaxWater;
            waterFill.sizeDelta = new Vector2(waterFill.sizeDelta.x, BarHeight * f);
            waterLabel.text = "수위 " + Mathf.RoundToInt(f * 100f) + "%";
            waterLabel.color = f > 0.75f ? new Color(1f, 0.5f, 0.45f) : new Color(0.7f, 0.85f, 1f);
        }

        private void Press(int p)
        {
            _puzzle.Toggle(_state, p);
            _water = Mathf.Min(WaterPuzzle.MaxWater, _water + WaterPuzzle.PressCost);
            Sfx.Play("ui_select", 0.8f);
            status.text = "";
            Refresh();
        }

        private void Confirm()
        {
            if (_puzzle.Check(_state))
            {
                _solved = true;
                status.text = "물이 배수구로 빠진다!";
                status.color = new Color(0.45f, 0.95f, 0.6f);
                _flashCol = new Color(0.5f, 0.8f, 1f); _flashA = 0.5f;
                Sfx.Play("power_on", 0.8f);
                _closeAt = Time.unscaledTime + 1.5f;
                Story1Net.Send(Story1Net.Clear, Story1Net.PWater);
            }
            else
            {
                _water = Mathf.Min(WaterPuzzle.MaxWater, _water + WaterPuzzle.WrongCost);
                _fails++;
                status.text = "수문이 막혔다! 물이 더 찼다.";
                status.color = new Color(1f, 0.5f, 0.45f);
                if (_fails >= 3) { int hv = -1; for (int i = 0; i < 4; i++) if (_puzzle.solution[i]) { hv = i; break; } if (hv >= 0) { status.text = "힌트: " + (hv + 1) + "번째 밸브를 돌려 보자"; status.color = new Color(0.95f, 0.85f, 0.45f); } }
                _flashCol = new Color(1f, 0.35f, 0.3f); _flashA = 0.5f; _shake = 10f;
                Sfx.Play("fuse_pop", 0.8f);
            }
            Refresh();
        }

        private void Flood()
        {
            status.text = "방이 물에 잠겼다! 밸브를 처음 상태로 되돌린다.";
            status.color = new Color(1f, 0.5f, 0.45f);
            _flashCol = new Color(0.3f, 0.5f, 1f); _flashA = 0.7f; _shake = 16f;
            Sfx.Play("fuse_pop", 1f);
            ResetValves(); _water = WaterPuzzle.MaxWater * 0.15f;
            Refresh();
        }

        internal static void ResetState() { Cleared = false; IsOpen = false; }
        internal static void MarkCleared() { Cleared = true; }
    }
}
