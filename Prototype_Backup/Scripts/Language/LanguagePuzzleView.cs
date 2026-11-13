using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Language
{
    /// <summary>
    /// 언어 장벽 협동 퍼즐 UI를 "열고 닫을 수 있는" 재사용 컴포넌트(순수 C# 클래스).
    /// 오버월드에서 PuzzleStation이 이걸 생성해 Open()/Close() 한다.
    /// (독립 테스트 씬에서는 CoopPrototype이 즉시 Open() 한다)
    ///
    /// 규칙은 프로토타입과 동일:
    ///  - A만 비밀 코드를 봄 → 팔레트로 '말하기'
    ///  - B가 뜻 맞히기 → 서로 배움(사전 성장)
    ///  - 코드 순서대로 자물쇠 입력 → 문 열림 → OnSolved 발생
    /// </summary>
    public class LanguagePuzzleView
    {
        private const string LangA = "solari";
        private const string LangB = "verda";

        private readonly string[] _palette = { "DOOR", "KEY", "LEVER", "BUTTON", "BRIDGE" };
        private readonly string[] _code = { "KEY", "DOOR", "LEVER" };

        private readonly LanguageDatabase _db;
        private readonly LanguageService _service;
        private readonly PlayerPhrasebook _pbA;
        private readonly PlayerPhrasebook _pbB;
        private readonly GameMessage _codeMessage;
        private readonly Font _font;

        private string _spokenConcept;
        private readonly List<string> _lockBuffer = new List<string>();
        private bool _solved;

        private GameObject _root;
        private Text _aCodeText, _bSpokenText, _bDictText, _bCodeText, _lockSlotsText, _statusText, _modeText, _exitLabel;

        /// <summary>문이 열려 퍼즐을 풀었을 때 1회 발생.</summary>
        public System.Action OnSolved;

        public bool IsOpen => _root != null && _root.activeSelf;
        public bool Solved => _solved;

        private static readonly Color PanelA = new Color(0.20f, 0.17f, 0.09f, 0.98f);
        private static readonly Color PanelB = new Color(0.10f, 0.18f, 0.14f, 0.98f);
        private static readonly Color BtnColor = new Color(0.24f, 0.26f, 0.32f);
        private static readonly Color BtnAccent = new Color(0.30f, 0.40f, 0.55f);
        private static readonly Color TextMain = new Color(0.95f, 0.95f, 0.97f);
        private static readonly Color TextDim = new Color(0.72f, 0.74f, 0.80f);

        public LanguagePuzzleView(RectTransform parent, LanguageDatabase db)
        {
            _font = UIFontHelper.Load();
            _db = db;
            _service = new LanguageService(db);
            _pbA = new PlayerPhrasebook(LangA);
            _pbB = new PlayerPhrasebook(LangB);

            _codeMessage = new GameMessage(LangA);
            foreach (var c in _code) _codeMessage.Add(c);

            Build(parent);
            _root.SetActive(false);
            Refresh();
        }

        public void Open()
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            InteractionState.PushLock();
        }

        public void Close()
        {
            _root.SetActive(false);
            InteractionState.PopLock();
        }

        // ---------- UI 생성 ----------

        private void Build(RectTransform parent)
        {
            _root = CreateRect("LanguagePuzzle", parent).gameObject;
            var rootRt = (RectTransform)_root.transform;
            Stretch(rootRt);
            var backdrop = _root.AddComponent<Image>();
            backdrop.color = new Color(0.04f, 0.05f, 0.07f, 0.75f); // 뒤 세계를 살짝 가리는 반투명 배경

            var frame = CreateRect("Frame", rootRt);
            Stretch(frame);
            frame.offsetMin = new Vector2(48, 40);
            frame.offsetMax = new Vector2(-48, -40);
            var frameV = frame.gameObject.AddComponent<VerticalLayoutGroup>();
            frameV.spacing = 10;
            frameV.childControlWidth = true; frameV.childControlHeight = true;
            frameV.childForceExpandWidth = true; frameV.childForceExpandHeight = true;

            // 상단 바: 제목 + 나가기
            var topBar = CreateRect("TopBar", frame);
            var topH = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topH.spacing = 10; topH.childControlWidth = true; topH.childControlHeight = true;
            topH.childForceExpandWidth = false; topH.childForceExpandHeight = true;
            SetFlexHeight(topBar.gameObject, 0f, 46f);

            var title = CreateText(topBar, "언어 장벽 협동 퍼즐 — 함께 자물쇠를 열어라", 22, TextMain, TextAnchor.MiddleLeft);
            SetFlexible(title.gameObject, 1f, 0f);
            var exitBtn = CreateButton(topBar, "나가기", BtnColor, Close);
            _exitLabel = exitBtn.GetComponentInChildren<Text>();
            var exitLe = exitBtn.GetComponent<LayoutElement>();
            exitLe.minWidth = 120; exitLe.flexibleWidth = 0f;

            var middle = CreateRect("Middle", frame);
            var midH = middle.gameObject.AddComponent<HorizontalLayoutGroup>();
            midH.spacing = 12; midH.childControlWidth = true; midH.childControlHeight = true;
            midH.childForceExpandWidth = true; midH.childForceExpandHeight = true;
            SetFlexible(middle.gameObject, 1f, 1f);

            BuildPanelA(middle);
            BuildPanelB(middle);

            var statusBar = CreatePanel(frame, "StatusBar", new Color(0.08f, 0.09f, 0.12f, 0.98f));
            SetFlexHeight(statusBar.gameObject, 0f, 52f);
            _statusText = CreateText(statusBar, "다가온 두 사람은 말이 통하지 않는다. 함께 방법을 찾아보자.", 17, TextMain, TextAnchor.MiddleCenter);
        }

        private void BuildPanelA(RectTransform parent)
        {
            var panel = CreatePanel(parent, "PanelA", PanelA);
            SetFlexible(panel.gameObject, 1f, 1f);

            CreateText(panel, "◤ 플레이어 A · Solari어", 22, _db.GetLanguage(LangA).tint, TextAnchor.MiddleLeft);
            CreateText(panel, "당신만 비밀 코드를 볼 수 있어요. 단어를 눌러 B에게 '말'하세요.", 15, TextDim, TextAnchor.MiddleLeft);

            CreateText(panel, "[ 비밀 코드 · 이 순서로! ]", 17, TextMain, TextAnchor.MiddleLeft);
            _aCodeText = CreateText(panel, "", 20, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleLeft);

            CreateText(panel, "[ 말하기 팔레트 ]", 17, TextMain, TextAnchor.MiddleLeft);
            var grid = CreateRect("PaletteGrid", panel);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(150, 54); g.spacing = new Vector2(8, 8);
            SetFlexHeight(grid.gameObject, 1f, 130f);

            foreach (var conceptId in _palette)
            {
                string c = conceptId;
                string label = _db.GetGlyph(LangA, c) + "\n(" + _db.GetConcept(c).meaning + ")";
                CreateButton(grid, label, BtnColor, () => SpeakWord(c));
            }
        }

        private void BuildPanelB(RectTransform parent)
        {
            var panel = CreatePanel(parent, "PanelB", PanelB);
            SetFlexible(panel.gameObject, 1f, 1f);

            CreateText(panel, "플레이어 B · Verda어 ◥", 22, _db.GetLanguage(LangB).tint, TextAnchor.MiddleRight);

            CreateText(panel, "[ A가 지금 하는 말 ]", 17, TextMain, TextAnchor.MiddleLeft);
            _bSpokenText = CreateText(panel, "...", 24, new Color(0.9f, 0.7f, 0.7f), TextAnchor.MiddleCenter);
            SetFlexHeight(_bSpokenText.gameObject, 0f, 46f);

            _modeText = CreateText(panel, "", 15, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleLeft);

            CreateText(panel, "[ 뜻 / 자물쇠 버튼 ]", 17, TextMain, TextAnchor.MiddleLeft);
            var grid = CreateRect("MeaningGrid", panel);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(150, 54); g.spacing = new Vector2(8, 8);
            SetFlexHeight(grid.gameObject, 0f, 130f);

            foreach (var conceptId in _palette)
            {
                string c = conceptId;
                string label = _db.GetGlyph(LangB, c) + "\n(" + _db.GetConcept(c).meaning + ")";
                CreateButton(grid, label, BtnColor, () => MeaningButton(c));
            }

            CreateText(panel, "[ 내 사전 (배운 단어) ]", 17, TextMain, TextAnchor.MiddleLeft);
            _bDictText = CreateText(panel, "(아직 없음)", 15, TextDim, TextAnchor.UpperLeft);
            SetFlexHeight(_bDictText.gameObject, 0f, 52f);

            CreateText(panel, "[ 자물쇠 — A의 코드가 이렇게 보임 ]", 17, TextMain, TextAnchor.MiddleLeft);
            _bCodeText = CreateText(panel, "", 20, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleLeft);
            _lockSlotsText = CreateText(panel, "", 22, TextMain, TextAnchor.MiddleLeft);

            var lockRow = CreateRect("LockRow", panel);
            var lr = lockRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            lr.spacing = 8; lr.childControlWidth = true; lr.childControlHeight = true;
            lr.childForceExpandWidth = false; lr.childForceExpandHeight = false;
            SetFlexHeight(lockRow.gameObject, 0f, 54f);
            CreateButton(lockRow, "확인", BtnAccent, ConfirmLock);
            CreateButton(lockRow, "지우기", BtnColor, ClearLock);
        }

        // ---------- 로직 ----------

        private void SpeakWord(string concept)
        {
            if (_solved) return;
            _spokenConcept = concept;
            SetStatus("A가 무언가 말했어요. B는 그 뜻을 뜻 버튼으로 맞혀보세요!");
            Refresh();
        }

        private void MeaningButton(string concept)
        {
            if (_solved) return;

            if (_spokenConcept != null)
            {
                if (concept == _spokenConcept)
                {
                    _pbB.Learn(LangA, _spokenConcept);
                    _pbA.Learn(LangB, _spokenConcept);
                    string m = _db.GetConcept(_spokenConcept).meaning;
                    SetStatus($"통했다! B가 '{m}' 를 배웠어요. (사전이 늘었습니다)");
                    _spokenConcept = null;
                }
                else
                {
                    SetStatus("아직 아니에요. 힘을 합쳐 다시 시도해보세요.");
                }
            }
            else
            {
                if (_lockBuffer.Count < _code.Length)
                {
                    _lockBuffer.Add(concept);
                    SetStatus($"자물쇠에 입력 중... ({_lockBuffer.Count}/{_code.Length})");
                }
            }
            Refresh();
        }

        private void ConfirmLock()
        {
            if (_solved) return;
            if (_lockBuffer.Count != _code.Length)
            {
                SetStatus("코드를 3개 모두 입력한 뒤 확인하세요.");
                return;
            }

            bool match = true;
            for (int i = 0; i < _code.Length; i++)
                if (_lockBuffer[i] != _code[i]) { match = false; break; }

            if (match)
            {
                _solved = true;
                SetStatus("문이 열렸다! 말이 안 통해도, 함께 배워서 해냈습니다.");
                if (_exitLabel != null) _exitLabel.text = "완료! 나가기";
                OnSolved?.Invoke();
            }
            else
            {
                SetStatus("자물쇠가 꿈쩍 않아요. A의 코드 순서를 다시 읽어보세요.");
                _lockBuffer.Clear();
            }
            Refresh();
        }

        private void ClearLock()
        {
            _lockBuffer.Clear();
            SetStatus("자물쇠 입력을 지웠어요.");
            Refresh();
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        private void Refresh()
        {
            _aCodeText.text = _service.RenderToString(_codeMessage, _pbA);

            if (_spokenConcept == null) _bSpokenText.text = "...";
            else _bSpokenText.text = _service.RenderToString(new GameMessage(LangA).Add(_spokenConcept), _pbB);

            _modeText.text = _spokenConcept != null
                ? "▶ 지금은 [배우기 모드] — A가 말한 뜻을 고르세요."
                : "▶ 지금은 [자물쇠 모드] — 코드 순서대로 버튼을 누르세요.";

            _bDictText.text = BuildDictText();
            _bCodeText.text = _service.RenderToString(_codeMessage, _pbB);
            _lockSlotsText.text = BuildLockSlots();
        }

        private string BuildDictText()
        {
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var concept in _palette)
            {
                if (_pbB.Understands(LangA, concept, _db))
                {
                    if (shown > 0) sb.Append("   ");
                    sb.Append(_db.GetGlyph(LangA, concept)).Append(" = ").Append(_db.GetConcept(concept).meaning);
                    shown++;
                }
            }
            return shown == 0 ? "(아직 없음)" : sb.ToString();
        }

        private string BuildLockSlots()
        {
            var sb = new System.Text.StringBuilder("입력: ");
            for (int i = 0; i < _code.Length; i++)
            {
                if (i > 0) sb.Append(" - ");
                if (i < _lockBuffer.Count) sb.Append("[").Append(_db.GetConcept(_lockBuffer[i]).meaning).Append("]");
                else sb.Append("[ _ ]");
            }
            return sb.ToString();
        }

        // ---------- UI 헬퍼 ----------

        private RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private RectTransform CreatePanel(RectTransform parent, string name, Color bg)
        {
            var rt = CreateRect(name, parent);
            rt.gameObject.AddComponent<Image>().color = bg;
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 12, 12);
            v.spacing = 8;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return rt;
        }

        private Text CreateText(RectTransform parent, string content, int size, Color color, TextAnchor anchor)
        {
            var rt = CreateRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font; t.text = content; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            rt.gameObject.AddComponent<LayoutElement>().minHeight = size + 8;
            return t;
        }

        private Button CreateButton(RectTransform parent, string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateRect("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(bg.r + 0.1f, bg.g + 0.1f, bg.b + 0.12f);
            colors.pressedColor = new Color(bg.r * 0.8f, bg.g * 0.8f, bg.b * 0.8f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txtRt = CreateRect("Label", rt);
            Stretch(txtRt);
            var t = txtRt.gameObject.AddComponent<Text>();
            t.font = _font; t.text = label; t.fontSize = 16; t.color = TextMain;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 48; le.minWidth = 120;
            return btn;
        }

        private static void SetFlexible(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.flexibleWidth = w; le.flexibleHeight = h;
        }

        private static void SetFlexHeight(GameObject go, float flexible, float min)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.flexibleHeight = flexible; le.minHeight = min;
        }
    }
}
