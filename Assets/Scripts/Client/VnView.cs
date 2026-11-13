using System;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 공용 비주얼노벨 렌더러(명세 3-1). 로컬 프롤로그와 네트워크 컷씬이 같이 쓴다.
    /// 구성: 배경(Backgrounds) → 어둠 → 초상(화자는 좌측 슬롯 — 플레이어·NPC 모두, PDF s07/s08; portraitRight 가 있을 때만 우측 슬롯) → 하단 대사창(이름표 앰버 탭 · 지문 · 타자 효과 · ▼ 다음) → 우상단 SKIP ▶▶.
    /// 진행 권한(CanAdvance)이 있을 때만 Enter/Space/클릭/▼/SKIP 이 OnNext/OnSkip 을 부른다. 판정은 호출자가 한다.
    /// 언어 장벽 줄(barrierMask 에 내 슬롯 비트)은 이름표 "???" + 0.12초마다 새로 만드는 ??? 덩어리.
    /// 정전 줄(blackout)은 1.1초 완전 암전(자막 없음) 뒤에 그 줄의 배경이 드러나고, 같은 순간 흔들림 + sfx(QA D-A2). 줄마다 sfx 문자열이 있으면 Sfx.Play.
    /// </summary>
    public class VnView
    {
        public const float CharsPerSecond = 45f;

        public RectTransform Root { get; private set; }
        public Action OnNext;
        public Action OnSkip;
        public bool CanAdvance;

        private RectTransform _bgLayer;
        private Image _dark;
        private RectTransform _portraitLeft, _portraitRight, _teamRow;
        private RectTransform _box;
        private RectTransform _nameTab;
        private Image _nameTabBg;
        private Text _nameText, _body, _tag, _next, _hint, _fadeTag, _direction;
        private Button _skip, _nextBtn;
        private RectTransform _badge, _tagBg, _iconSlot;
        private Image _fade;

        private Cutscene _cut;
        private int _index = -1;
        private int _viewSlot = -2;
        private string _bgKey = "";
        private string _lastTag;
        private string _fullText = "";
        private bool _barrier;
        private float _typeT, _jiggleT, _blinkT, _fadeT;
        private float _holdT;            // 정전 완전 암전 남은 시간(QA D-A2)
        private string _pendingBg;       // 암전이 끝나면 그릴 배경 키
        private bool _teamRowOn, _teamRowSticky;

        public const float BlackoutHold = 1.1f;

        public bool Typing => !_barrier && _fullText.Length > (int)(_typeT * CharsPerSecond);
        public int Index => _index;
        public bool AtEnd => _cut != null && _index >= _cut.Count - 1;

        public void Build(RectTransform parent)
        {
            Root = UI.Stretch(parent, "VnView");
            UI.Bg(Root, Skin.Bg, false);

            _bgLayer = UI.Stretch(Root, "Background");

            // 어둠 레이어(dark 연출) + 하단 그라데이션(글자 가독성)
            var darkRt = UI.Stretch(Root, "Dark");
            _dark = UI.Bg(darkRt, new Color(0f, 0f, 0f, 0f), false);
            for (int i = 0; i < 5; i++)
            {
                var band = UI.Rect(Root, "Shade");
                band.anchorMin = new Vector2(0f, 0.06f * i); band.anchorMax = new Vector2(1f, 0.06f * (i + 1) + 0.002f);
                band.offsetMin = Vector2.zero; band.offsetMax = Vector2.zero;
                UI.Bg(band, new Color(0.02f, 0.03f, 0.05f, 0.55f - i * 0.11f), false);
            }

            // 화면 아무 곳이나 클릭 → 다음. 레이캐스트에 기대지 않고 Tick 에서 마우스 버튼을 직접 읽는다
            // (대사창·초상·페이드 등 어떤 이미지가 위에 있어도 클릭이 먹도록). SKIP/다음 버튼 위의 클릭은 그 버튼이 처리한다.

            // 초상 자리(좌: 화자 — 플레이어·NPC 모두(P-A11), 우: portraitRight 가 있을 때만) — 대사창 옆, 아래쪽 정렬
            _portraitLeft = UI.Fixed(Root, "PortraitL", new Vector2(0f, 0f), new Vector2(52f, 44f), new Vector2(210f, 250f), new Vector2(0f, 0f));
            _portraitRight = UI.Fixed(Root, "PortraitR", new Vector2(1f, 0f), new Vector2(-52f, 44f), new Vector2(210f, 250f), new Vector2(1f, 0f));
            // 오브젝트 아이콘 오버레이(벽보 등) — 우측 초상 위쪽
            _iconSlot = UI.Fixed(Root, "IconSlot", new Vector2(1f, 0f), new Vector2(-96f, 320f), new Vector2(120f, 120f), new Vector2(1f, 0f));
            _iconSlot.gameObject.SetActive(false);
            _teamRow = UI.Fixed(Root, "TeamRow", new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1000f, 260f));
            UI.HStack(_teamRow, 0, 40, false, TextAnchor.MiddleCenter);
            _teamRow.gameObject.SetActive(false);

            // 대사창(슬레이트 테두리, 어두운 속)
            _box = Skin.Bordered(Root, "TextBox", new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(1060f, 160f), Skin.Border, new Color(0.03f, 0.05f, 0.08f, 0.94f), 3f, null, false);
            _body = UI.Label(_box, "", 22, Skin.Ink, new Vector2(0f, 1f), new Vector2(34f, -30f), new Vector2(940f, 110f), TextAnchor.UpperLeft);
            _body.lineSpacing = 1.2f;

            // 이름표: 대사창 위쪽 테두리에 걸치는 앰버 탭
            _nameTab = UI.Fixed(_box, "NameTab", new Vector2(0f, 1f), new Vector2(28f, 16f), new Vector2(96f, 36f), new Vector2(0f, 0.5f));
            _nameTabBg = UI.Bg(_nameTab, Skin.Amber, false);
            _nameText = UI.Label(_nameTab, "", 17, Skin.DarkText, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 36f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            _nameText.fontStyle = FontStyle.Bold;

            // 지문(QA D-A10): 이름표 아래 작은 회색 글씨 — 있을 때만 보이고 본문을 조금 내린다
            _direction = UI.Label(_box, "", 14, Skin.Dim, new Vector2(0f, 1f), new Vector2(34f, -10f), new Vector2(940f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            _direction.fontStyle = FontStyle.Italic;
            _direction.gameObject.SetActive(false);

            // ▼ 다음(우하단, 깜빡임) — 진행 권한이 있으면 버튼
            var nextRt = UI.Fixed(_box, "Next", new Vector2(1f, 0f), new Vector2(-14f, 10f), new Vector2(96f, 34f), new Vector2(1f, 0f));
            var nimg = nextRt.gameObject.AddComponent<Image>();
            nimg.color = new Color(0f, 0f, 0f, 0f);
            _nextBtn = nextRt.gameObject.AddComponent<Button>();
            _nextBtn.targetGraphic = nimg;
            _nextBtn.transition = Selectable.Transition.None;
            _nextBtn.onClick.AddListener(Advance);
            _next = UI.Label(nextRt, "다음 ▼", 15, Skin.Amber, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 34f), TextAnchor.MiddleRight, new Vector2(0.5f, 0.5f));

            _hint = UI.Label(Root, "", 14, Skin.Dim, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(600f, 22f), TextAnchor.MiddleCenter);

            // 상단 장면 표기 [ 교실 점심시간 ] — 밝은 배경 위에서도 읽히도록 반투명 검정 띠 위에 앰버 글자
            _tagBg = UI.Fixed(Root, "TagBg", new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(360f, 28f));
            UI.Bg(_tagBg, new Color(0.024f, 0.031f, 0.051f, 0.6f), false);
            _tagBg.gameObject.SetActive(false);
            _tag = UI.Label(Root, "", 15, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(700f, 26f), TextAnchor.MiddleCenter);

            // 우상단 SKIP ▶▶
            _skip = Skin.Outline(Root, "SKIP  ▶▶", () => { if (CanAdvance) OnSkip?.Invoke(); }, 15, 40, 112);
            var srt = (RectTransform)_skip.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-40f, -40f);
            srt.sizeDelta = new Vector2(112f, 40f);
            UnityEngine.Object.Destroy(_skip.GetComponent<LayoutElement>());

            // 좌상단 시점 배지(네트워크 컷씬에서만 보임)
            _badge = UI.Fixed(Root, "Badge", new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(230f, 42f), new Vector2(0f, 1f));
            _badge.gameObject.SetActive(false);

            // 장면 전환 암전 + 큰 자막
            var fadeRt = UI.Stretch(Root, "Fade");
            _fade = UI.Bg(fadeRt, new Color(0f, 0f, 0f, 0f), false);
            _fadeTag = UI.Label(fadeRt, "", 30, Skin.Amber, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 50f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            _fadeTag.color = new Color(0, 0, 0, 0);
        }

        public void Dispose()
        {
            Sfx.Stop();
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
            Root = null;
        }

        /// <summary>
        /// 네 사람 픽셀 초상을 가운데 나란히 세운다. sticky = 컷씬 내내 유지(클리어 컷씬). 아니면 줄의 group 값에 따라 Show 가 켜고 끈다(캠프 첫 만남).
        /// </summary>
        public void SetTeamRow(bool on, bool sticky = false)
        {
            if (_teamRow == null) return;
            _teamRowSticky = on && sticky;
            if (on && _teamRowOn) return;
            _teamRowOn = on;
            _teamRow.gameObject.SetActive(on);
            if (!on) return;
            UI.Clear(_teamRow);
            for (int i = 0; i < 4; i++)
            {
                var cell = UI.Rect(_teamRow, "T" + i);
                UI.LE(cell, 180, 250, 0f);
                var v = UI.VStack(cell, 0, 4, false, TextAnchor.LowerCenter);
                var port = UIPortrait.MakeForSlot(cell, i, i, 80f);
                UI.LE(port, 176, 192, 0f);
                string nm = Session.Connected ? Session.NameOf(i) : Roles.DefaultName(i);
                UI.Text(cell, $"<color=#{Roles.Hex(i)}><b>{nm}</b></color>", 17, Skin.Ink, TextAnchor.MiddleCenter, false, 26);
            }
        }

        /// <summary>
        /// 컷씬의 index 줄을 보여준다. viewSlot = 내 시점 슬롯(-1 = 로컬 프롤로그). defaultBg = bg 가 비어 있을 때의 배경 키.
        /// 같은 줄이면 타자 진행을 유지하고 장벽 표시만 다시 계산한다.
        /// </summary>
        public void Show(Cutscene cut, int index, int viewSlot, string defaultBg)
        {
            if (cut == null || cut.Count == 0 || Root == null) return;
            index = Mathf.Clamp(index, 0, cut.Count - 1);
            bool sameLine = _cut == cut && _index == index;
            bool slotChanged = _viewSlot != viewSlot;
            if (sameLine && !slotChanged) return;
            _cut = cut; _index = index; _viewSlot = viewSlot;
            var line = cut.lines[index];

            // 팀 초상 줄(group): 줄마다 켜고 끈다. sticky(클리어 컷씬)면 그대로 둔다
            if (line.group) { if (!_teamRowOn) SetTeamRow(true); }
            else if (_teamRowOn && !_teamRowSticky) SetTeamRow(false);

            // 배경 키: 이 줄부터 거슬러 올라가 처음 나오는 bg, 없으면 기본값
            string bg = defaultBg ?? "";
            for (int i = index; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(cut.lines[i].bg)) { bg = cut.lines[i].bg; break; }
            }
            bool blackout = line.blackout && !sameLine;
            if (blackout)
            {
                // 정전(QA D-A2): 지금 배경을 둔 채 완전 암전 → 암전이 끝나면 새 배경이 드러난다(Tick)
                _holdT = BlackoutHold;
                _pendingBg = bg;
                _fadeT = 0f;
                if (_fadeTag != null) { _fadeTag.text = ""; _fadeTag.color = new Color(0f, 0f, 0f, 0f); }
                if (_fade != null) { _fade.color = new Color(0f, 0f, 0f, 1f); _fade.raycastTarget = true; }
                FeedbackFX.Shake(0.6f);
            }
            else
            {
                if (_holdT > 0f && !sameLine) { _holdT = 0f; _pendingBg = null; }   // 암전 도중 다음 줄로 넘어가면 바로 드러낸다
                if (_holdT <= 0f && (bg != _bgKey || _bgLayer.childCount == 0))
                {
                    _bgKey = bg;
                    UI.Clear(_bgLayer);
                    Backgrounds.Draw(_bgLayer, bg);
                }
            }
            if (_dark != null) _dark.color = new Color(0f, 0f, 0f, line.dark ? 0.72f : 0f);

            // 장면 전환(sceneTag 가 새로 나오면) 짧은 암전 + 자막 — 정전 줄은 태그 없이 암전만
            if (!sameLine && !blackout && !string.IsNullOrEmpty(line.sceneTag) && line.sceneTag != _lastTag)
            {
                _fadeT = 1.1f;
                if (_fadeTag != null) _fadeTag.text = line.sceneTag;
            }
            if (!string.IsNullOrEmpty(line.sceneTag)) _lastTag = line.sceneTag;
            if (_tag != null) _tag.text = string.IsNullOrEmpty(_lastTag) ? "" : $"[ {_lastTag} ]";
            if (_tagBg != null) _tagBg.gameObject.SetActive(!string.IsNullOrEmpty(_lastTag));

            // 효과음(QA D-A1): 줄에 들어설 때 한 번
            if (!sameLine && !string.IsNullOrEmpty(line.sfx)) Sfx.Play(line.sfx);

            // 언어 장벽
            _barrier = viewSlot >= 0 && (line.barrierMask & (1 << viewSlot)) != 0;
            _fullText = line.text ?? "";
            if (!sameLine) { _typeT = blackout ? -(BlackoutHold + 0.35f) : 0f; _jiggleT = 1f; }   // 정전 줄은 배경이 드러난 뒤 타자 시작

            // 이름표
            bool narration = string.IsNullOrEmpty(line.speaker) && !line.system;
            if (_nameTab != null)
            {
                _nameTab.gameObject.SetActive(!narration);
                string sp = SpeakerName(line);
                if (_barrier) sp = "???";
                if (_nameText != null) _nameText.text = sp;
                float w = Mathf.Max(72f, 26f + sp.Length * 17f * 0.95f);
                _nameTab.sizeDelta = new Vector2(w, 36f);
                if (_nameTabBg != null) _nameTabBg.color = _barrier ? new Color(0.62f, 0.62f, 0.60f) : (line.system ? new Color(0.55f, 0.80f, 0.95f) : Skin.Amber);
            }
            // 지문(QA D-A10): 장벽 줄에서는 숨긴다(알아들을 수 없는 말)
            bool hasDirection = !_barrier && !string.IsNullOrEmpty(line.direction);
            if (_direction != null)
            {
                _direction.gameObject.SetActive(hasDirection);
                _direction.text = hasDirection ? $"({line.direction})" : "";
            }
            if (_body != null)
            {
                _body.fontStyle = narration ? FontStyle.Italic : FontStyle.Normal;
                _body.color = narration ? new Color(0.86f, 0.88f, 0.92f) : (line.system ? new Color(0.85f, 0.95f, 1f) : Skin.Ink);
                _body.text = "";
                var brt = (RectTransform)_body.transform;
                brt.anchoredPosition = new Vector2(34f, hasDirection ? -46f : -30f);
                brt.sizeDelta = new Vector2(940f, hasDirection ? 94f : 110f);
            }

            // 초상(P-A11): 화자는 좌측 슬롯(플레이어·NPC 모두), 우측은 portraitRight 가 있을 때만
            UI.Clear(_portraitLeft); UI.Clear(_portraitRight);
            Portrait(_portraitLeft, line.portrait);
            Portrait(_portraitRight, line.portraitRight);

            // 오브젝트 아이콘 오버레이(D-A3: 벽보)
            if (_iconSlot != null)
            {
                UI.Clear(_iconSlot);
                var icon = string.IsNullOrEmpty(line.icon) ? null : SpriteLib.Icon(line.icon);
                _iconSlot.gameObject.SetActive(icon != null);
                if (icon != null)
                {
                    var box = Slot(_iconSlot);
                    var img = UI.Image(box, icon, Color.white, new Vector2(96f, 96f));
                    var irt = (RectTransform)img.transform;
                    irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                    irt.anchoredPosition = Vector2.zero;
                    UnityEngine.Object.Destroy(img.GetComponent<LayoutElement>());
                }
            }
            RefreshChrome();
        }

        /// <summary>초상 하나를 슬롯에 세운다(0~3 팀원 픽셀 초상, 10+ NPC 실루엣, 그 밖은 비움).</summary>
        private static void Portrait(RectTransform slot, int id)
        {
            if (slot == null) return;
            RectTransform p = null;
            if (id >= 0 && id < 4) p = UIPortrait.MakeForSlot(Slot(slot), id, id, 92f);
            else if (id >= 10) p = UIPortrait.Npc(Slot(slot), id, 92f);
            if (p == null) return;
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f);
            p.anchoredPosition = new Vector2(0f, 4f);
        }

        /// <summary>초상 뒤의 어두운 슬롯 상자(PDF의 초상 자리).</summary>
        private static RectTransform Slot(RectTransform parent)
        {
            var box = UI.Stretch(parent, "Slot");
            UI.Bg(box, new Color(0.04f, 0.06f, 0.10f, 0.85f), false);
            return box;
        }

        /// <summary>정전 암전이 진행 중인가(투어·테스트용).</summary>
        public bool BlackoutActive => _holdT > 0f;

        /// <summary>이름표 문구. 플레이어 초상 줄은 실제 이름(접속 전이면 내 저장 이름/기본 이름)으로 바꾼다.</summary>
        private string SpeakerName(StoryLine line)
        {
            if (line.system) return "안내";
            if (line.portrait >= 0 && line.portrait < 4)
            {
                string nm = PlayerName(line.portrait);
                bool inner = !string.IsNullOrEmpty(line.speaker) && line.speaker.Contains("속마음");
                return inner ? nm + " · 속마음" : nm;
            }
            return line.speaker ?? "";
        }

        public static string PlayerName(int slot)
        {
            if (Session.Connected && Roles.Valid(slot))
            {
                var p = Session.State.players[slot];
                if (p != null && p.present && !string.IsNullOrEmpty(p.name)) return p.name;
            }
            if (slot == 0 && !string.IsNullOrEmpty(Progress.myName)) return Progress.myName;
            return Roles.DefaultName(slot);
        }

        /// <summary>SKIP/다음/힌트/시점 배지 표시 갱신(권한·시점이 바뀔 때).</summary>
        public void RefreshChrome()
        {
            if (_skip != null) _skip.gameObject.SetActive(CanAdvance);
            if (_nextBtn != null) _nextBtn.interactable = CanAdvance;
            if (_hint != null) _hint.text = CanAdvance ? "" : "호스트가 진행합니다…";

            if (_badge != null)
            {
                bool show = _viewSlot >= 0 && Session.Connected;
                _badge.gameObject.SetActive(show);
                if (show)
                {
                    UI.Clear(_badge);
                    var c = Roles.Color(_viewSlot);
                    UI.Bg(_badge, c, false);
                    var inner = UI.Stretch(_badge, "Inner", 2, 2, 2, 2);
                    UI.Bg(inner, Skin.PanelSolid, false);
                    var row = UI.Stretch(inner, "Row", 12, 0, 8, 0);
                    UI.HStack(row, 0, 10, false, TextAnchor.MiddleLeft);
                    var sq = UI.Rect(row, "Square");
                    UI.Bg(sq, c, false);
                    UI.LE(sq, 18, 18, 0f, 0f, 18, 18);
                    string nation = Roles.Nation(_viewSlot);
                    if (nation.EndsWith("인")) nation = nation.Substring(0, nation.Length - 1);
                    var t = UI.Text(row, $"{PlayerName(_viewSlot)} · {nation} 시점", 15, c, TextAnchor.MiddleLeft, false);
                    UI.LE(t, -1, -1, 1f);
                }
            }
        }

        /// <summary>타자 중이면 전부 보여주고, 아니면 다음 줄로.</summary>
        public void Advance()
        {
            if (!CanAdvance) return;
            if (Typing) { _typeT = 999f; return; }
            OnNext?.Invoke();
        }

        public void CompleteTyping() { _typeT = 999f; }

        private static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

        /// <summary>포인터 아래 맨 위 UI 요소가 버튼(SKIP·다음 등)에 속하면 true — 그 클릭은 버튼이 처리하므로 화면 클릭 진행에서 제외.</summary>
        private static bool PointerOverButton(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            _hits.Clear();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = screenPos }, _hits);
            if (_hits.Count == 0) return false;
            var b = _hits[0].gameObject.GetComponentInParent<Button>();
            return b != null && b.interactable;
        }

        public void Tick()
        {
            if (Root == null) return;
            float dt = Time.deltaTime;
            _typeT += dt; _jiggleT += dt; _blinkT += dt;

            if (_body != null)
            {
                if (_barrier)
                {
                    if (_jiggleT > 0.12f)
                    {
                        _jiggleT = 0f;
                        var sb = new System.Text.StringBuilder();
                        int words = Mathf.Clamp(_fullText.Length / 5, 4, 14);
                        for (int i = 0; i < words; i++)
                        {
                            int n = UnityEngine.Random.Range(2, 6);
                            sb.Append('?', n);
                            if (UnityEngine.Random.value < 0.18f) sb.Append('.');
                            sb.Append(' ');
                        }
                        _body.text = $"<color=#C9CDD4>{sb}</color>\n<size=14><color=#8A96A8>(알아들을 수 없는 말이다)</color></size>";
                    }
                }
                else
                {
                    int chars = Mathf.Clamp((int)(_typeT * CharsPerSecond), 0, _fullText.Length);   // 정전 줄은 _typeT 가 음수에서 시작
                    string shown = _fullText.Substring(0, chars);
                    if (shown != _body.text) _body.text = shown;
                }
            }

            // 정전 완전 암전(QA D-A2): 끝나면 보류한 배경을 그리고 걷힘 구간(_fadeT 뒤 절반)으로 넘어간다
            if (_holdT > 0f)
            {
                _holdT -= dt;
                if (_fade != null) { _fade.color = new Color(0f, 0f, 0f, 1f); _fade.raycastTarget = true; }
                if (_holdT <= 0f)
                {
                    _holdT = 0f;
                    if (_pendingBg != null && _bgLayer != null)
                    {
                        _bgKey = _pendingBg;
                        UI.Clear(_bgLayer);
                        Backgrounds.Draw(_bgLayer, _pendingBg);
                    }
                    _pendingBg = null;
                    if (_fadeTag != null) _fadeTag.text = "";
                    _fadeT = 1.1f * 0.45f;   // k ≤ 0.45 → 바로 걷히기 시작
                }
            }

            // ▼ 깜빡임: 타자가 끝났을 때만
            if (_next != null)
            {
                bool done = !Typing;
                float a = done ? (0.55f + 0.45f * Mathf.Sin(_blinkT * 4f)) : 0.15f;
                _next.color = new Color(Skin.Amber.r, Skin.Amber.g, Skin.Amber.b, CanAdvance ? a : 0.25f);
                _next.text = done ? "다음 ▼" : "▼";
            }

            // 장면 전환 암전
            if (_fade != null && _holdT <= 0f)
            {
                if (_fadeT > 0f)
                {
                    _fadeT -= dt;
                    float k = Mathf.Clamp01(_fadeT / 1.1f);           // 1 → 0
                    float black = k > 0.45f ? 1f : k / 0.45f;          // 앞 절반은 완전 암전, 뒤 절반에서 걷힘
                    _fade.color = new Color(0f, 0f, 0f, black * 0.96f);
                    float tagA = Mathf.Clamp01(Mathf.Min((1f - k) * 4f, k * 2.2f));
                    _fadeTag.color = new Color(Skin.Amber.r, Skin.Amber.g, Skin.Amber.b, tagA);
                    _fade.raycastTarget = true;
                }
                else if (_fade.color.a > 0f || _fade.raycastTarget)
                {
                    // 마지막 페이드 프레임에서 알파가 정확히 0이 되면 알파 조건만으로는 이 분기에 못 들어와
                    // 투명한 페이드가 클릭을 영원히 가로챘다(SKIP·다음 버튼 먹통). raycastTarget 도 조건에 넣는다.
                    _fade.color = new Color(0f, 0f, 0f, 0f);
                    _fadeTag.color = new Color(0f, 0f, 0f, 0f);
                    _fade.raycastTarget = false;
                }
            }

            // 키보드(Enter/Space)·마우스 왼쪽 클릭 진행 — 다른 입력 잠금 중이면 무시
            if (CanAdvance && !InteractionState.InputLocked && _fadeT <= 0.3f && _holdT <= 0f)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    Advance();
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverButton(mouse.position.ReadValue()))
                    Advance();
            }
        }
    }
}
