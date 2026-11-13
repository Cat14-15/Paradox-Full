using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 이름 설정(PDF s05). 커튼 배경 + 카드 4장(국기색 라벨, 픽셀 초상, 이름, 역할명).
    /// 내 카드만 이름을 편집(InputField, 앰버 테두리), 다른 카드는 실시간 이름 표시.
    /// [확인] = 이름 전송 후 Ready 1, [준비 취소] = Ready 0. [뒤로] 는 호스트만(Back). (PDF 의 CONFIRM/BACK — 아이 눈높이로 한국어) 전원 Ready 면 서버가 다음으로 넘긴다.
    /// </summary>
    public class NameSetupScreen : ScreenBase
    {
        private RectTransform _cards;
        private readonly Text[] _nameTexts = new Text[4];
        private readonly Text[] _readyTexts = new Text[4];
        private readonly RectTransform[] _cardInners = new RectTransform[4];
        private InputField _name;
        private Button _confirm, _back;
        private Text _waiting;
        private int _builtFor = -2;
        private bool _nameLocked;
        private string _lastSent;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            ReadyScreen.DrawCurtain(Root);

            // QA P-A5: 커튼 금색 띠(위에서 60~68px)와 겹치지 않게 제목·부제를 아래로
            UI.Label(Root, "이름을 정해 주세요", 30, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(800f, 44f), TextAnchor.MiddleCenter);
            UI.Label(Root, "국적과 역할은 캠프에서 이미 배정되었습니다", 14, Skin.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(800f, 22f), TextAnchor.MiddleCenter);

            _cards = UI.Fixed(Root, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1320f, 460f));
            UI.HStack(_cards, 0, 30, false, TextAnchor.MiddleCenter);

            _back = Skin.Outline(Root, "뒤로", () => Session.Send("Back"), 17, 52, 120);
            var brt = (RectTransform)_back.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f); brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(190f, 60f); brt.sizeDelta = new Vector2(120f, 52f);

            _confirm = Skin.Accent(Root, "확인", Confirm, 18, 52, 150);
            var crt = (RectTransform)_confirm.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f); crt.pivot = new Vector2(1f, 0f);
            crt.anchoredPosition = new Vector2(-190f, 60f); crt.sizeDelta = new Vector2(150f, 52f);

            _waiting = UI.Label(Root, "", 14, Skin.Dim, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(700f, 24f), TextAnchor.MiddleCenter);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 122f), true);
            OnState();
        }

        private void Confirm()
        {
            var me = Session.Me;
            if (me == null) return;
            if (me.ready) { Session.Send("Ready", 0); return; }
            SendName();
            Session.Send("Ready", 1);
        }

        private void SendName()
        {
            if (_name == null) return;
            string s = (_name.text ?? "").Trim();
            if (string.IsNullOrEmpty(s)) s = Roles.DefaultName(Session.LocalSlot);
            Progress.myName = s;
            _lastSent = s;
            Session.Send("Name", 0, 0, s);
        }

        private void BuildCards()
        {
            UI.Clear(_cards);
            Unlock();
            _name = null;
            int mine = Session.LocalSlot;
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                var c = Roles.Color(slot);
                bool isMe = slot == mine;
                var card = Skin.BorderedCell(_cards, "Card" + slot, 300, 440, isMe ? Skin.Amber : Skin.Border, Skin.PanelSolid, isMe ? 3f : 2f);
                _cardInners[slot] = card;
                UI.VStack(card, 18, 6, false, TextAnchor.UpperCenter);

                string nation = Roles.Nation(slot);
                if (nation.EndsWith("인")) nation = nation.Substring(0, nation.Length - 1);
                Skin.Swatch(card, c, nation, 14, c, 22f);

                // 스프라이트 슬롯
                var slotBox = UI.Rect(card, "Slot");
                UI.Bg(slotBox, Skin.Slot, false);
                UI.LE(slotBox, 190, 200, 0f);
                var portrait = UIPortrait.MakeForSlot(slotBox, slot, slot, 72f);
                portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
                portrait.anchoredPosition = new Vector2(0f, 4f);

                UI.Text(card, "이름", 11, Skin.Dim, TextAnchor.MiddleLeft, false, 14);
                if (isMe)
                {
                    string initial = string.IsNullOrEmpty(Progress.myName) ? "" : Progress.myName;
                    var me = Session.Me;
                    if (string.IsNullOrEmpty(initial) && me != null) initial = me.name;
                    _name = UI.Input(card, Roles.DefaultName(slot), initial, 20, 10);
                    var img = _name.GetComponent<Image>();
                    if (img != null) img.color = Skin.C("0A0F18");
                    UI.LE(_name, -1, 50);
                    _name.onEndEdit.AddListener(s => { SendName(); Unlock(); });
                    // 저장된 이름이 있고 서버 이름과 다르면 바로 알려 준다
                    if (me != null && !me.ready && !string.IsNullOrEmpty(Progress.myName) && Progress.myName != me.name) SendName();
                    _nameTexts[slot] = null;
                }
                else
                {
                    var box = UI.Rect(card, "NameBox");
                    UI.Bg(box, Skin.C("0A0F18"), false);
                    UI.LE(box, -1, 50);
                    var t = UI.Label(box, "", 20, Skin.Ink, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(260f, 50f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
                    _nameTexts[slot] = t;
                }
                var roleRow = UI.Rect(card, "RoleRow");
                UI.HStack(roleRow, 0, 6, true, TextAnchor.MiddleLeft);
                UI.LE(roleRow, -1, 20);
                UI.Text(roleRow, Roles.Name(slot), 12, Skin.Dim, TextAnchor.MiddleLeft, false);
                _readyTexts[slot] = UI.Text(roleRow, "", 12, Skin.Green, TextAnchor.MiddleRight, false);
            }
        }

        private void Lock() { if (!_nameLocked) { InteractionState.PushLock(); _nameLocked = true; } }
        private void Unlock() { if (_nameLocked) { InteractionState.PopLock(); _nameLocked = false; } }

        public override void Dispose()
        {
            Unlock();
            base.Dispose();
        }

        public override void Tick()
        {
            bool focused = _name != null && _name.isFocused;
            if (focused && !_nameLocked) Lock();
            else if (!focused && _nameLocked) Unlock();
        }

        public override void OnState()
        {
            var S = Session.State;
            int slot = Session.LocalSlot;
            var me = Session.Me;
            if (_builtFor != slot) { BuildCards(); _builtFor = slot; }

            int readyCount = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                bool ready = p.present && p.ready;
                if (ready) readyCount++;
                if (_nameTexts[i] != null)
                {
                    string nm = p.present ? (string.IsNullOrEmpty(p.name) ? Roles.DefaultName(i) : p.name) : "빈 자리";
                    _nameTexts[i].text = p.present ? nm : $"<color=#5A6478>{nm}</color>";
                }
                if (_readyTexts[i] != null) _readyTexts[i].text = ready ? "✓ 준비" : (p.present && p.isBot ? "봇" : "");
                if (_cardInners[i] != null && i != slot) Skin.SetBorder(_cardInners[i], ready ? Skin.Alpha(Roles.Color(i), 0.85f) : Skin.Border);
            }

            if (_name != null && me != null)
            {
                // 서버 이름 동기화: 입력 중이 아니고, 내가 보낸 이름이 반영됐거나 아직 아무것도 안 보냈을 때만
                if (!_name.isFocused)
                {
                    if (_lastSent != null && me.name == _lastSent) _lastSent = null;
                    if (_lastSent == null && !string.IsNullOrEmpty(me.name) && _name.text != me.name) _name.text = me.name;
                }
                _name.interactable = !me.ready;
            }
            if (_confirm != null && me != null)
            {
                var t = UI.ButtonLabel(_confirm);
                if (t != null) t.text = me.ready ? "준비 취소" : "확인";
                UI.SetButtonColor(_confirm, me.ready ? Skin.Border : Skin.Amber);
                if (t != null) t.color = me.ready ? Skin.Ink : Skin.DarkText;
            }
            if (_back != null) _back.gameObject.SetActive(Session.IsHost);
            if (_waiting != null)
            {
                if (me != null && me.ready) _waiting.text = $"준비 완료 — 다른 친구들을 기다리는 중 ({readyCount}/4)";
                else _waiting.text = readyCount > 0 ? $"{readyCount}/4 준비됨 — 이름을 정하고 [확인]을 누르세요" : "이름을 정하고 [확인]을 누르세요";
            }
        }
    }
}
