using System;
using System.Collections;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// Lobby.unity 전용: "팀 모으기" — ① 방(만들기·코드로 참가) → ② 캐릭터 선택(+닉네임) → 시작하면 Paradox 씬으로 이어진다.
    /// 모든 패널·카드·버튼·글씨는 씬에 직접 배치돼 있고, 이 컴포넌트는 입력 → NetHub(Photon Fusion)/Session 명령 → 표시 갱신만 맡는다.
    /// 온라인 규칙(슬롯·봇·시작)은 서버 권한자(GameLogic)가 판정하고, 여기는 그 상태(Session.State)를 그릴 뿐이다.
    /// </summary>
    public class LobbyScene : MonoBehaviour
    {
        [Serializable]
        public class Btn
        {
            public RectTransform rect;
            public Image border, fill;
            public PixelText label;
        }

        [Serializable]
        public class Card
        {
            public Btn btn;
            public Image portrait;
            public PixelText code, tag, name, role, owner;
        }

        [Header("패널")]
        public CanvasGroup black, roomPanel, charPanel, busy;
        public PixelText busyText, busySub;

        [Header("① 방")]
        public Btn[] roomCards;                 // 0 방 만들기, 1 방 참가하기
        public Btn solo;                        // 에디터 전용 혼자 테스트
        public Btn backBtn;                     // 방 화면의 "뒤로" (Esc 를 쓰지 않아도 나갈 수 있게)
        public Image[] codeBorders;
        public PixelText[] codeChars;
        public PixelText roomStatus;

        [Header("② 캐릭터 선택")]
        public Card[] cards;
        public PixelText charTitle, charCodeLine, charStatus;
        public Btn nickBtn, leaveBtn, startBtn, botsBtn, diffBtn, stageBtn;
        public PixelText nickValue;

        public Sprite[] portraits;

        private enum Mode { Room, Connecting, Character, Leaving }
        private Mode _mode = Mode.Room;
        private int _roomSel = 1;               // 0 만들기 1 참가 2 혼자 (처음엔 참가에 포커스)
        private string _code = "";
        private int _cardSel;
        private bool _nickFocus;
        private string _nick = "";
        private string _ime = "";        // 한글 조합 중인 글자(아직 확정 전)
        private bool _imeOn;

        private void OnIme(UnityEngine.InputSystem.LowLevel.IMECompositionString s)
        {
            if (Web) return;
            _ime = s.ToString();
            if (_ime.Length > 0) { _imeSeen = true; _hgOn = false; _hg.Reset(); }   // OS 입력기가 살아 있으면 자체 조합기는 끈다
        }

        // 한글 자체 조합기(OS 입력기가 유니티에 전달되지 않을 때의 대체 경로) — 오른쪽 Alt/Ctrl 로 한/영 전환
        private readonly HangulComposer _hg = new HangulComposer();
        private bool _hgOn, _imeSeen;

        private static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;

        private static readonly string[] TypeIds = { "ui_type", "ui_type2", "ui_type3" };
        private int _typeN;
        private void TypeSfx(float v) { _typeN = (_typeN + UnityEngine.Random.Range(1, 3)) % 3; Paradox.Core.Sfx.Play(TypeIds[_typeN], v); }

        private void AddNick(string s)
        {
            foreach (char ch in s) if (PixelFontData.Has(ch) && _nick.Length < MaxNick) _nick += ch;
        }

        private string NickComposing => _hgOn ? _hg.Current : _ime;

        private void FlushHangul() { if (_hg.Active) { AddNick(_hg.Current); _hg.Reset(); } }
        private bool _suppressDisc;
        private float _bsTimer;
        private float _msgUntil;
        private static readonly Color Mint = new Color(0.376f, 0.984f, 0.796f);
        private static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);
        private static readonly Color Warn = new Color(1f, 0.55f, 0.5f);
        private static readonly Color Dim = new Color(0.52f, 0.58f, 0.62f);
        private static readonly Color Ink = new Color(0.9f, 0.95f, 0.96f);
        private static readonly Color Bd = new Color(0.30f, 0.33f, 0.38f);
        private const int MaxNick = 8;
        private const int CodeLen = 6;

        // ================================================================= 생명주기

        private void Awake()
        {
            Application.runInBackground = true;
            if (NetHub.Instance == null) new GameObject("NetHub").AddComponent<NetHub>();
        }

        private void OnEnable()
        {
            var kb = Keyboard.current;
            if (kb != null) { kb.onTextInput += OnText; kb.onIMECompositionChange += OnIme; }
            Session.StateChanged += OnState;
        }

        private void OnDisable()
        {
            var kb = Keyboard.current;
            if (kb != null) { kb.onTextInput -= OnText; kb.onIMECompositionChange -= OnIme; kb.SetIMEEnabled(false); }
            Session.StateChanged -= OnState;
            var hub = NetHub.Instance;
            if (hub != null) { hub.StatusChanged -= OnStatus; hub.Disconnected -= OnDisconnected; }
        }

        private void Start()
        {
            _nick = PlayerPrefs.GetString("stranger_nick", "");
            // 지난번 방 코드를 참가 칸에 미리 채워 둔다(원래 있던 방으로 바로 돌아가기 · Enter 만 누르면 된다)
            if (_code.Length == 0) { string last = PlayerPrefs.GetString(LastRoomKey, ""); if (NetHub.TryParseCode(last, out _, out _)) _code = last; }
            if (Web) _hgOn = true;
            Bgm.Preload("prologue");   // 게임을 시작할 때 곡을 읽느라 멈추지 않도록 미리
            var hub = NetHub.Instance;
            hub.StatusChanged += OnStatus;
            hub.Disconnected += OnDisconnected;

            solo.rect.gameObject.SetActive(true);       // 혼자 하기: AI 친구 3명과 함께(정식 빌드에서도)
            botsBtn.rect.gameObject.SetActive(false);
            black.alpha = 1f;
            roomPanel.alpha = 1f; charPanel.alpha = 0f; busy.alpha = 0f;
            SetPanelRaycast(roomPanel, true); SetPanelRaycast(charPanel, false);
            roomStatus.text = "";
            charStatus.text = "";
            var copy = charCodeLine.gameObject.AddComponent<PointerRelay>();
            copy.owner = this;
            copy.group = 10;
            // 윗줄 오른쪽: 난이도 + 스피드런을 나란히(오른쪽 위 끝은 설정 단추 자리 · x 724 부터)
            if (diffBtn != null && diffBtn.rect != null)
            {
                diffBtn.rect.anchoredPosition = new Vector2(326f, 440f); diffBtn.rect.sizeDelta = new Vector2(300f, diffBtn.rect.sizeDelta.y);
                if (diffBtn.label != null) diffBtn.label.rectTransform.sizeDelta = new Vector2(280f, diffBtn.label.rectTransform.sizeDelta.y);
            }
            speedBtn = CloneButton(diffBtn, "SpeedrunButton", 11, new Vector2(598f, 440f), 228f);

            // 이미 접속해 있으면(예: 게임 중 돌아옴) 바로 캐릭터 선택으로
            if (hub.IsRunning && Session.Connected) EnterCharacter();
            StartCoroutine(FadeBlack(0f, 0.5f));
            RefreshRoom();
        }

        private static void SetPanelRaycast(CanvasGroup g, bool on) { g.blocksRaycasts = on; g.interactable = on; }

        private IEnumerator FadeBlack(float to, float seconds)
        {
            float from = black.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime) { black.alpha = Mathf.Lerp(from, to, t / seconds); yield return null; }
            black.alpha = to;
        }

        // ================================================================= 포인터

        public void PointerEnter(int group, int id)
        {
            if (_mode == Mode.Room && (group == 0 || group == 1)) _roomSel = group == 0 ? id : 2;
            else if (_mode == Mode.Character && group == 2) _cardSel = id;
        }

        public void PointerClick(int group, int id)
        {
            if (_mode == Mode.Room || _mode == Mode.Character) Paradox.Core.Sfx.Play(group == 4 || group == 7 ? "ui_back" : "ui_select");
            switch (_mode)
            {
                case Mode.Room:
                    if (group == 0) { _roomSel = id; if (id == 0) CreateRoom(); else FocusJoin(); }
                    else if (group == 1) { _roomSel = 2; SoloTest(); }
                    else if (group == 7) BackToTitle();
                    break;
                case Mode.Character:
                    if (_nickFocus && group != 3) CommitNick();   // 마우스로 다른 곳을 누르면 입력 중이던 마지막 글자까지 확정
                    if (group == 2) { _cardSel = id; PickCard(id); }
                    else if (group == 3) { _nickFocus = true; }
                    else if (group == 4) LeaveRoom();
                    else if (group == 5) TryStart();
                    else if (group == 10) CopyCode();
                    else if (group == 8) CycleDifficulty();
                    else if (group == 9) CycleStage();
                    else if (group == 11) ToggleSpeedrun();
                    break;
            }
        }

        // ================================================================= 입력

        private void OnText(char c)
        {
            if (SettingsPanel.IsOpen) return;
            if (_mode == Mode.Room)
            {
                // 방 코드는 UpdateRoomInput 에서 키보드 키로 직접 읽는다(한글 입력기가 켜져 있어도 영문·숫자로 들어가게)
            }
            else if (_mode == Mode.Character && _nickFocus)
            {
                if (Web && c >= 128) return;   // 웹: 브라우저 입력기 대신 자체 한글 조합기만 쓴다(중복·깨짐 방지)
                if (Web && !_hgOn && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return;   // 웹 영문·숫자는 아래 UpdateCharInput 에서 키로 직접 읽는다(맥 한글 입력기가 켜져 있어도 입력되게)
                if (c >= 128) { _imeSeen = true; _hgOn = false; _hg.Reset(); }
                if (_hgOn)
                {
                    if (c < 128 && char.IsLetter(c)) return;   // 영문 글자는 자체 조합기가 키로 직접 처리
                    FlushHangul();
                }
                if (c >= ' ' && c != 127 && PixelFontData.Has(c) && _nick.Length < MaxNick) { _nick += c; TypeSfx(1f); }
            }
        }

        private Mode _lastMode = (Mode)(-1);
        private int _lastRoomSel = -1, _lastCardSel = -1;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && !SettingsPanel.IsOpen)
            {
                // 방 화면에서는 A/D 가 방 코드 글자라서 이동키로 쓰지 않는다(화살표·Tab 만).
                bool letterNav = !_nickFocus && _mode != Mode.Room;
                bool left = kb.leftArrowKey.wasPressedThisFrame || (letterNav && kb.aKey.wasPressedThisFrame);
                bool right = kb.rightArrowKey.wasPressedThisFrame || (letterNav && kb.dKey.wasPressedThisFrame);
                bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                bool esc = kb.escapeKey.wasPressedThisFrame;
                bool back = kb.backspaceKey.wasPressedThisFrame;
                if (kb.backspaceKey.isPressed) { _bsTimer += Time.deltaTime; if (_bsTimer > 0.45f) { back = true; _bsTimer = 0.38f; } } else _bsTimer = 0f;

                if (_mode == Mode.Room || (_mode == Mode.Character && !_nickFocus))
                {
                    if (enter) Paradox.Core.Sfx.Play("ui_select");
                    if (esc) Paradox.Core.Sfx.Play("ui_back");
                }
                if (_mode == Mode.Room) UpdateRoomInput(kb, left, right, enter, esc, back);
                else if (_mode == Mode.Connecting) { if (esc) CancelConnect(); }
                else if (_mode == Mode.Character) UpdateCharInput(kb, left, right, enter, esc, back);
            }

            // 닉네임 입력 중에만 IME(한글 조합)를 켠다
            bool wantIme = _mode == Mode.Character && _nickFocus;
            if (wantIme && !_imeOn && !_imeSeen) Say("한글 ↔ 영문 전환: 오른쪽 Alt · Caps Lock · F2", Dim);
            if (kb != null && wantIme != _imeOn)
            {
                _imeOn = wantIme; kb.SetIMEEnabled(wantIme);
                if (!wantIme) _ime = "";
            }

            // 선택이 바뀌면 살짝 '틱'
            if (_mode != _lastMode) { _lastMode = _mode; _lastRoomSel = _roomSel; _lastCardSel = _cardSel; }
            else if (_mode == Mode.Room && _roomSel != _lastRoomSel) { _lastRoomSel = _roomSel; Paradox.Core.Sfx.Play("ui_hover", 0.7f); }
            else if (_mode == Mode.Character && _cardSel != _lastCardSel) { _lastCardSel = _cardSel; Paradox.Core.Sfx.Play("ui_hover", 0.7f); }

            // 패널 크로스페이드 — 연결 중에는 방 화면도 사라지고 완전히 어두운 배경에 글자만 남긴다
            bool room = _mode == Mode.Room;
            roomPanel.alpha = Mathf.MoveTowards(roomPanel.alpha, room ? 1f : 0f, Time.deltaTime * 4f);
            charPanel.alpha = Mathf.MoveTowards(charPanel.alpha, _mode == Mode.Character || _mode == Mode.Leaving ? 1f : 0f, Time.deltaTime * 4f);
            SetPanelRaycast(roomPanel, _mode == Mode.Room);
            SetPanelRaycast(charPanel, _mode == Mode.Character);
            bool showBusy = _mode == Mode.Connecting || _mode == Mode.Leaving || (_mode == Mode.Character && Session.State.phase == Phase.Ready);
            busy.alpha = Mathf.MoveTowards(busy.alpha, showBusy ? 1f : 0f, Time.deltaTime * 6f);
            busy.blocksRaycasts = showBusy;

            if (_msgUntil > 0f && Time.time > _msgUntil) { _msgUntil = 0f; if (_mode == Mode.Room) roomStatus.text = ""; else charStatus.text = ""; }

            if (_mode == Mode.Connecting && Session.Connected && Session.State.phase == Phase.Lobby && Session.MySlot >= 0) EnterCharacter();

            if (_mode == Mode.Room) RefreshRoom(); else if (_mode == Mode.Character) RefreshChar();
        }

        private void UpdateRoomInput(Keyboard kb, bool left, bool right, bool enter, bool esc, bool back)
        {
            if (!kb.ctrlKey.isPressed && !kb.leftCommandKey.isPressed && !kb.rightCommandKey.isPressed && !kb.altKey.isPressed && _code.Length < CodeLen)
            {
                for (int k = 0; k < 26 && _code.Length < CodeLen; k++)
                    if (kb[(Key)((int)Key.A + k)].wasPressedThisFrame) AddCodeChar((char)('A' + k));
                for (int n = 0; n < 10 && _code.Length < CodeLen; n++)
                {
                    var dk = n == 0 ? kb.digit0Key : kb[(Key)((int)Key.Digit1 + n - 1)];
                    if (dk.wasPressedThisFrame || kb[(Key)((int)Key.Numpad0 + n)].wasPressedThisFrame) AddCodeChar((char)('0' + n));
                }
            }
            if (left || right)
            {
                int max = Application.isEditor ? 2 : 1;
                _roomSel = Mathf.Clamp(_roomSel + (right ? -1 : 1), 0, max);   // 참가(1)가 왼쪽, 만들기(0)가 오른쪽
            }
            if (kb.tabKey.wasPressedThisFrame) _roomSel = (_roomSel + 1) % (Application.isEditor ? 3 : 2);
            if (back && _code.Length > 0) { _code = _code.Substring(0, _code.Length - 1); _roomSel = 1; _lastRoomSel = 1; TypeSfx(0.8f); }
            if ((kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed) && kb.vKey.wasPressedThisFrame)
            {
                string clip = NetHub.Normalize(GUIUtility.systemCopyBuffer);
                string f = "";
                foreach (char c in clip) if (char.IsLetterOrDigit(c) && c < 128 && f.Length < CodeLen) f += c;
                if (f.Length > 0) { _code = f; _roomSel = 1; _lastRoomSel = 1; }
            }
            if (enter)
            {
                if (_roomSel == 0) CreateRoom();
                else if (_roomSel == 1) FocusJoin();
                else SoloTest();
            }
            if (esc) BackToTitle();
        }

        private void AddCodeChar(char c) { _code += c; _roomSel = 1; _lastRoomSel = 1; TypeSfx(1f); }

        private void UpdateCharInput(Keyboard kb, bool left, bool right, bool enter, bool esc, bool back)
        {
            if (_nickFocus)
            {
                if (Web && !_hgOn && !kb.ctrlKey.isPressed && !kb.altKey.isPressed && !kb.leftCommandKey.isPressed && !kb.rightCommandKey.isPressed)
                {
                    bool sh = kb.shiftKey.isPressed;
                    for (int k = 0; k < 26; k++)
                        if (kb[(Key)((int)Key.A + k)].wasPressedThisFrame) { char ch = (char)((sh ? 'A' : 'a') + k); if (PixelFontData.Has(ch) && _nick.Length < MaxNick) { _nick += ch; TypeSfx(1f); } }
                    for (int k = 0; k < 10; k++)
                        if (kb[(Key)((int)Key.Digit0 + k)].wasPressedThisFrame && !sh) { char ch = (char)('0' + k); if (_nick.Length < MaxNick) { _nick += ch; TypeSfx(1f); } }
                }
                if (!_imeSeen && (kb.rightAltKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame || kb.rightCommandKey.wasPressedThisFrame || kb.capsLockKey.wasPressedThisFrame || kb.f2Key.wasPressedThisFrame))
                {
                    FlushHangul(); _hgOn = !_hgOn;
                    Say(_hgOn ? "한글 입력  (오른쪽 Alt 로 영문 전환)" : "영문 입력", Mint);
                }
                if (_hgOn && !kb.ctrlKey.isPressed && !kb.altKey.isPressed)
                {
                    bool shift = kb.shiftKey.isPressed;
                    for (int k = 0; k < 26; k++)
                    {
                        var key = kb[(Key)((int)Key.A + k)];
                        if (!key.wasPressedThisFrame) continue;
                        char j = HangulComposer.JamoOf((char)('a' + k), shift);
                        if (j == '\0') continue;
                        string done = _hg.Key(j);
                        AddNick(done);
                        TypeSfx(1f);
                    }
                }
                if (back)
                {
                    if (_hgOn && _hg.Back()) TypeSfx(0.8f);
                    else if (_ime.Length == 0 && _nick.Length > 0) { _nick = _nick.Substring(0, _nick.Length - 1); TypeSfx(0.8f); }
                }
                if (enter || esc || kb.tabKey.wasPressedThisFrame) CommitNick();
                return;
            }
            if (left) _cardSel = (_cardSel + 3) % 4;
            if (right) _cardSel = (_cardSel + 1) % 4;
            if (kb.tabKey.wasPressedThisFrame) _nickFocus = true;
            if (enter) PickCard(_cardSel);
            if (esc) LeaveRoom();
            if ((kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed) && kb.cKey.wasPressedThisFrame) CopyCode();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void StrangerCopyCode(string code, string receiver);
#endif
        private void CopyCode()
        {
            var hub = NetHub.Instance;
            if (hub == null || hub.IsSolo || string.IsNullOrEmpty(hub.RoomCode)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            StrangerCopyCode(hub.RoomCode, gameObject.name);
#else
            GUIUtility.systemCopyBuffer = hub.RoomCode;
            CopyCodeResult("ok");
#endif
        }

        [UnityEngine.Scripting.Preserve]
        public void CopyCodeResult(string result)
        {
            if (_mode == Mode.Character) Say(result == "ok" ? "방 코드를 복사했어요" : "복사 권한을 확인해 주세요", result == "ok" ? Mint : Warn);
        }

        // ================================================================= ① 방

        private void FocusJoin()
        {
            if (!NetHub.TryParseCode(_code, out _, out _)) { Say(_code.Length == 0 ? "방 코드를 입력하세요" : "방 코드는 6자예요", Warn); return; }
            StartConnect(() => NetHub.Instance.StartClient(_code), "방에 들어가는 중…");
        }

        private void CreateRoom() { StartConnect(() => NetHub.Instance.StartHost(false), "방을 만드는 중…"); }
        private void SoloTest() { StartConnect(() => NetHub.Instance.StartHost(true), "AI 친구들을 부르는 중…"); }

        private void StartConnect(Func<bool> start, string msg)
        {
            var hub = NetHub.Instance;
            if (hub.IsRunning) return;
            busyText.text = msg;
            busySub.text = "";
            if (!start()) { Say(hub.Status, Warn); return; }
            _mode = Mode.Connecting;
        }

        private void CancelConnect()
        {
            _suppressDisc = true;
            NetHub.Instance.Shutdown();
            _suppressDisc = false;
            _mode = Mode.Room;
            Say("취소했어요", Dim);
        }

        private void OnStatus(string s)
        {
            if (_mode == Mode.Connecting && busySub != null) busySub.text = s ?? "";
        }

        private void OnDisconnected()
        {
            if (_suppressDisc || _mode == Mode.Leaving) return;
            string msg = NetHub.Instance != null ? NetHub.Instance.Status : "";
            if (string.IsNullOrEmpty(msg)) msg = "서버와 연결이 끊어졌어요. 다시 들어와 주세요";
            _mode = Mode.Room;
            _nickFocus = false;
            // 실패 이유를 오래 보여 준다(같은 버튼을 다시 누르면 재시도). 긴 문장은 글씨를 줄여 칸 안에
            if (roomStatus != null && roomStatus.maxWidth <= 0f) roomStatus.maxWidth = Mathf.Max(600f, roomStatus.rectTransform.rect.width);
            Say(msg, Warn, 10f);
        }

        private void Say(string msg, Color c, float seconds = 3f)
        {
            var t = _mode == Mode.Character ? charStatus : roomStatus;
            t.text = msg; t.color = c;
            _msgUntil = Time.time + seconds;
        }

        private void RefreshRoom()
        {
            // 방 카드 / 혼자 테스트 버튼
            Style(roomCards[0], _roomSel == 0, Mint, true);
            Style(roomCards[1], _roomSel == 1, Mint, true);
            Style(solo, _roomSel == 2, Amber, true);
            Style(backBtn, false, Warn, true);
            bool joinFocus = _roomSel == 1;
            for (int i = 0; i < CodeLen; i++)
            {
                bool has = i < _code.Length;
                codeChars[i].text = has ? _code[i].ToString() : "";
                bool cursor = joinFocus && i == _code.Length && (int)(Time.time * 2f) % 2 == 0;
                codeBorders[i].color = has ? Mint : (cursor ? Ink : Bd);
            }
        }

        // ================================================================= ② 캐릭터 선택

        private void EnterCharacter()
        {
            _mode = Mode.Character;
            _cardSel = Mathf.Max(0, Session.MySlot);
            _nickFocus = false;
            if (_nick.Length > 0) Session.Send("Name", 0, 0, _nick);
            var hub = NetHub.Instance;
            charCodeLine.text = hub.IsSolo ? "혼자 하기 · AI 친구 3명" : CodeLabel(hub.RoomCode);
            if (!hub.IsSolo) RememberRoom(hub.RoomCode);
            charCodeLine.raycastTarget = !hub.IsSolo;
            charCodeLine.color = hub.IsSolo ? Amber : Mint;
            hub.RoomCodeChanged -= OnCode;
            hub.RoomCodeChanged += OnCode;
            charStatus.text = "";
        }

        private const string LastRoomKey = "stranger_last_room";

        private static void RememberRoom(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            try { PlayerPrefs.SetString(LastRoomKey, code); PlayerPrefs.Save(); } catch { }
        }

        private void OnCode(string c)
        {
            var hub = NetHub.Instance;
            if (hub != null && !hub.IsSolo) RememberRoom(c);
            if (_mode == Mode.Character && hub != null && !hub.IsSolo) charCodeLine.text = CodeLabel(c);
        }

        private static string CodeLabel(string code) => string.IsNullOrEmpty(code) ? "방 코드 생성 중" : "방 코드   " + code + "   ·   클릭해서 복사";

        private void OnState()
        {
            if (_mode != Mode.Character && _mode != Mode.Connecting) return;
            var S = Session.State;
            if (_mode == Mode.Character)
            {
                if ((int)S.phase >= (int)Phase.StageIntro) StartCoroutine(HandOff());
            }
        }

        private IEnumerator HandOff()
        {
            if (_mode == Mode.Leaving) yield break;
            _mode = Mode.Leaving;
            busyText.text = "시작합니다";
            busySub.text = "";
            yield return FadeBlack(1f, 0.6f);
            // 타이틀 "이어하기"로 온 혼자 하기: 이미 본 프롤로그·캠프를 건너뛰고 박물관으로 바로(온라인은 다 같이 보므로 그대로)
            var hub = NetHub.Instance;
            bool skip = (SkipStory && hub != null && hub.IsSolo) || Story1Net.Speedrun;   // 스피드런: 모두 같은 설정이라 다 같이 박물관으로 바로
            SkipStory = false;
            SceneManager.LoadSceneAsync(skip ? "MainStory1" : "Prologue");   // 프롤로그를 보고 난 뒤 캠프 → 박물관으로 넘어간다
        }

        /// <summary>타이틀 "이어하기": 혼자 하기라면 프롤로그·캠프 이야기를 건너뛴다.</summary>
        public static bool SkipStory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { SkipStory = false; }

        private void PickCard(int i)
        {
            var S = Session.State;
            if (i == Session.MySlot) { TryStart(); return; }
            var p = S.players[i];
            if (p.present && !p.isBot) { Say("다른 친구가 고른 캐릭터예요", Warn); return; }
            Session.Send("Slot", i);
        }

        private void TryStart()
        {
            if (Session.IsHost && Session.State.HumansReady) Session.Send("Launch");   // 빈 자리는 서버가 AI 로 채운다
            else ToggleReady();
        }

        private void ToggleReady()
        {
            int me = Session.MySlot;
            if (me >= 0 && (Session.State.phase == Phase.Lobby || Session.State.phase == Phase.NameSetup))
                Session.Send("Ready", Session.State.players[me].ready ? 0 : 1);
        }

        private void BackToTitle() { IntroSequence.SkipToMenu = true; StartCoroutine(LeaveScene("Intro")); }

        // 스테이지: 방을 만든 뒤 방장이 고른다. 지금은 Stage 1 만 열려 있다.
        private static readonly string[] StageNames = { "침수된 박물관", "TOP SECRET", "TOP SECRET", "TOP SECRET" };
        private static readonly bool[] StageOpen = { true, false, false, false };
        private int _stage;

        private void CycleStage()
        {
            if (!Session.IsHost) { Paradox.Core.Sfx.Play("ui_back", 0.6f); return; }
            int n = (_stage + 1) % StageNames.Length;
            if (!StageOpen[n]) { Paradox.Core.Sfx.Play("ui_back", 0.6f); Say("Stage " + (n + 1) + " 은 아직 잠겨 있어요", Warn); return; }
            _stage = n;
        }

        // 난이도: 방장만 바꾼다(서버가 한 번 더 확인). 다른 사람은 방장이 고른 값을 본다.
        private void CycleDifficulty()
        {
            if (!Session.IsHost) { Paradox.Core.Sfx.Play("ui_back", 0.6f); return; }
            int d = (Story1Net.Difficulty + 1) % Story1Rules.Count;
            Story1Net.SetDifficulty(d);
            Say(Story1Rules.Names[d] + " · " + Story1Rules.Blurbs[d], d == Story1Rules.Easy ? Mint : Amber);
        }

        // 스피드런: 이야기를 모두 건너뛰고 구간 기록을 잰다(랭킹도 따로). 방장만 바꾼다.
        private Btn speedBtn;

        private void ToggleSpeedrun()
        {
            if (!Session.IsHost) { Paradox.Core.Sfx.Play("ui_back", 0.6f); return; }
            bool on = !Story1Net.Speedrun;
            Story1Net.SetSpeedrun(on);
            Say(on ? "스피드런 · 이야기를 건너뛰고 시간만 잰다 · 랭킹 따로" : "스토리 모드 · 이야기와 함께", on ? Amber : Mint);
        }

        /// <summary>씬에 있는 단추를 복제해 새 단추를 만든다(씬 빌더를 다시 돌리지 않아도 되게).</summary>
        private Btn CloneButton(Btn src, string name, int group, Vector2 pos, float width)
        {
            if (src == null || src.rect == null) return null;
            var go = Instantiate(src.rect.gameObject, src.rect.parent);
            go.name = name;
            var r = (RectTransform)go.transform;
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(width, r.sizeDelta.y);
            var relay = go.GetComponent<PointerRelay>();
            if (relay != null) { relay.owner = this; relay.group = group; relay.id = 0; }
            var b = new Btn { rect = r, fill = go.GetComponent<Image>() };
            var br = go.transform.Find("Border"); if (br != null) b.border = br.GetComponent<Image>();
            var lb = go.transform.Find("Label"); if (lb != null) { b.label = lb.GetComponent<PixelText>(); ((RectTransform)lb).sizeDelta = new Vector2(width - 20f, ((RectTransform)lb).sizeDelta.y); }
            return b;
        }

        private void CommitNick()
        {
            _nickFocus = false;
            FlushHangul(); _hgOn = Web;
            _nick = _nick.Trim();
            PlayerPrefs.SetString("stranger_nick", _nick);
            if (Session.Connected) Session.Send("Name", 0, 0, _nick);
        }

        private void LeaveRoom()
        {
            if (_mode == Mode.Leaving) return;
            var hub = NetHub.Instance;
            hub.RoomCodeChanged -= OnCode;
            _suppressDisc = true;
            hub.Shutdown();
            _suppressDisc = false;
            _mode = Mode.Room;
            _nickFocus = false;
        }

        private IEnumerator LeaveScene(string scene)
        {
            _mode = Mode.Leaving;
            busyText.text = "";
            yield return FadeBlack(1f, 0.45f);
            SceneManager.LoadSceneAsync(scene);
        }

        private void RefreshChar()
        {
            var S = Session.State;
            int me = Session.MySlot;
            int present = S.PresentCount;
            int ready = 0;
            for (int i = 0; i < 4; i++)
            {
                var c = cards[i];
                var p = S.players[i];
                Color rc = Roles.Color(i);
                bool mine = i == me;
                bool taken = p.present && !p.isBot && !mine;
                bool focus = _cardSel == i;
                Style(c.btn, focus, rc, true, mine);
                if (p.present && p.ready) ready++;
                c.tag.text = !p.present ? "" : p.ready ? "준비 완료" : "입장";
                c.tag.rectTransform.sizeDelta = new Vector2(220f, 36f);
                c.tag.rectTransform.anchoredPosition = new Vector2(18f, 234f);
                c.tag.color = p.ready ? Mint : Amber;
                c.name.text = Roles.DefaultName(i);
                c.name.color = mine || p.present ? Ink : Dim;
                c.role.text = Roles.Name(i);
                c.role.color = rc;
                string who = p.present ? (p.isBot ? "AI 친구" : p.name) : "빈 자리";
                if (mine) who = (string.IsNullOrEmpty(p.name) ? Roles.DefaultName(i) : p.name);
                c.owner.text = (mine ? "★ " : "") + (p.present && i == S.hostSlot ? "방장 " : "") + who;
                c.owner.color = mine ? Amber : (p.present ? Ink : Dim);
                // 초상: 비어 있으면 실루엣, 가져간 캐릭터는 밝게, 선택 중이면 살짝 크게
                c.portrait.color = p.present ? Color.white : new Color(0.16f, 0.18f, 0.22f, 1f);
                float ps = Mathf.Lerp(c.portrait.rectTransform.localScale.x, focus ? 1.06f : 1f, Mathf.Clamp01(Time.deltaTime * 12f));
                c.portrait.rectTransform.localScale = new Vector3(ps, ps, 1f);
                _ = taken;
            }
            charTitle.scale = 2;
            charTitle.text = "입장 " + present + "/4   ·   준비 " + ready + "/4";

            // 닉네임
            bool blink = (int)(Time.time * 2f) % 2 == 0;
            string shown = _nick.Length > 0 ? _nick : (_nickFocus ? "" : (me >= 0 ? Roles.DefaultName(me) : ""));
            string comp = "";
            foreach (char ch in NickComposing) if (PixelFontData.Has(ch) && (_nick.Length + comp.Length) < MaxNick) comp += ch;
            nickValue.text = shown + (_nickFocus ? comp : "") + (_nickFocus && blink ? "_" : "");
            nickValue.color = _nick.Length > 0 || _nickFocus ? Ink : Dim;
            Style(nickBtn, _nickFocus, Mint, true);
            // 닉네임 칸: 입력 중이면 테두리가 민트↔흰색으로 또렷하게 반짝이고,
            // 아직 이름을 안 정했다면 호박색으로 천천히 숨 쉬듯 깜빡여 눈에 띄게 한다
            if (nickBtn != null && nickBtn.border != null)
            {
                if (_nickFocus) nickBtn.border.color = Color.Lerp(Mint, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 9f));
                else if (_nick.Length == 0) { var a = Amber; a.a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f)); nickBtn.border.color = a; }
            }

            // 버튼
            Style(leaveBtn, false, Warn, true);
            bool host = Session.IsHost;
            bool can = me >= 0 && (S.phase == Phase.Lobby || S.phase == Phase.NameSetup);
            Style(startBtn, can, Mint, can);
            startBtn.label.text = host && S.HumansReady ? (present < 4 ? "AI와 시작" : "게임 시작") : can && S.players[me].ready ? "준비 취소" : "준비하기";
            if (host && S.HumansReady && present < 4 && _msgUntil <= 0f && charStatus.text.Length == 0)
            { charStatus.text = "빈 자리 " + (4 - present) + "개는 AI 친구가 맡아요"; charStatus.color = Dim; }
            else if (!(host && S.HumansReady && present < 4) && _msgUntil <= 0f && charStatus.text.StartsWith("빈 자리")) charStatus.text = "";
            startBtn.label.scale = 3;
            startBtn.label.color = can ? Mint : Dim;
            stageBtn.label.text = "Stage " + (_stage + 1) + "  " + StageNames[_stage];
            stageBtn.label.color = host ? Mint : Dim;
            Style(stageBtn, false, host ? Mint : Dim, true);
            int dl = Story1Net.Difficulty;
            diffBtn.label.text = "난이도  " + Story1Rules.Names[dl];
            diffBtn.label.color = host ? Amber : Dim;
            Style(diffBtn, false, host ? Amber : Dim, true);
            if (speedBtn != null && speedBtn.label != null)
            {
                bool sp = Story1Net.Speedrun;
                speedBtn.label.text = sp ? "스피드런 ON" : "스피드런 OFF";
                speedBtn.label.color = !host ? Dim : sp ? Amber : Mint;
                Style(speedBtn, sp, !host ? Dim : sp ? Amber : Mint, true);
            }
        }

        // ================================================================= 공통 스타일

        private void Style(Btn b, bool focus, Color accent, bool enabled, bool mine = false)
        {
            if (b == null || b.rect == null) return;
            float k = Mathf.Clamp01(Time.deltaTime * 14f);
            float target = focus && enabled ? 1.04f : 1f;
            float s = Mathf.Lerp(b.rect.localScale.x, target, k);
            b.rect.localScale = new Vector3(s, s, 1f);
            Color bc = focus && enabled ? accent : (mine ? accent * 0.85f : Color.Lerp(Bd, accent, enabled ? 0.28f : 0f));
            bc.a = 1f;
            if (focus && enabled) bc.a = 0.78f + 0.22f * Mathf.Sin(Time.time * 5f);
            b.border.color = bc;
            Color bg = new Color(0.055f, 0.065f, 0.085f, 0.94f);
            if (focus && enabled) bg = Color.Lerp(bg, accent, 0.10f);
            else if (mine) bg = Color.Lerp(bg, accent, 0.07f);
            bg.a = 0.94f;
            b.fill.color = bg;
        }
    }
}
