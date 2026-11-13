using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>캐릭터 선택 + 이름 입력. 전원이 준비 완료하면 서버가 프롤로그로 넘긴다.</summary>
    public class CharacterSelectScreen : ScreenBase
    {
        private RectTransform _grid;
        private RectTransform _side;
        private RectTransform _status;
        private InputField _name;
        private Button _ready;
        private Text _myTitle;
        private bool _nameLocked;
        private bool _autoCharSent;
        private int _builtFor = -2;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, new Color(0.08f, 0.09f, 0.13f, 1f));
            UI.Label(Root, "캐릭터 선택 + 이름 입력", 32, UI.Gold, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(900f, 46f), TextAnchor.MiddleCenter);

            _grid = UI.Fixed(Root, "Grid", new Vector2(0f, 0.5f), new Vector2(90f, 40f), new Vector2(760f, 520f), new Vector2(0f, 0.5f));
            UI.Grid(_grid, new Vector2(240f, 165f), new Vector2(12f, 12f), 3);

            _side = UI.Panel(Root, "Side", UI.PanelMid, new Vector2(1f, 0.5f), new Vector2(-90f, 40f), new Vector2(560f, 520f), new Vector2(1f, 0.5f));
            UI.VStack(_side, 22, 12, false, TextAnchor.UpperLeft);

            _status = UI.Fixed(Root, "Status", new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1300f, 90f));
            UI.HStack(_status, 0, 12, true, TextAnchor.MiddleCenter);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 126f), true);
            BuildSide();
            OnState();
        }

        private void BuildSide()
        {
            UI.Clear(_side);
            int slot = Session.LocalSlot;
            var me = Session.Me;
            _myTitle = UI.Text(_side, "", 22, UI.Ink, TextAnchor.MiddleLeft, true, 34);
            UI.Text(_side, Roles.Job(slot), 14, UI.InkDim, TextAnchor.UpperLeft, true, 44);

            UI.Text(_side, "이름 (10자까지)", 15, UI.InkDim, TextAnchor.MiddleLeft, false, 22);
            _name = UI.Input(_side, "이름을 입력하세요", me != null ? me.name : "", 18, 10);
            UI.LE(_name, -1, 46);
            _name.onEndEdit.AddListener(s => { Session.Send("Name", 0, 0, s); Unlock(); });

            UI.Text(_side, "왼쪽에서 캐릭터를 클릭해 고르세요. 준비 완료 후에는 바꿀 수 없습니다.", 14, UI.InkDim, TextAnchor.UpperLeft, true, 40);

            _ready = UI.Button(_side, "준비 완료", UI.BtnGood, () =>
            {
                var m = Session.Me;
                if (m == null) return;
                if (!m.ready && _name != null) Session.Send("Name", 0, 0, _name.text);
                Session.Send("Ready", m.ready ? 0 : 1);
            }, 20, 54);
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
            // 이름 입력 중에는 단축키(1~4 등)가 먹지 않도록 잠근다
            bool focused = _name != null && _name.isFocused;
            if (focused && !_nameLocked) Lock();
            else if (!focused && _nameLocked) Unlock();
        }

        public override void OnState()
        {
            var S = Session.State;
            int slot = Session.LocalSlot;
            var me = Session.Me;
            if (_builtFor != slot) { BuildSide(); _builtFor = slot; }

            // 캐릭터 그리드 — 역할 전용 캐릭터가 있으면 고르지 않고 그 캐릭터를 보여준다
            bool fixedArt = GameApp.Instance != null && GameApp.Instance.HasRoleArt(slot);
            UI.Clear(_grid);
            if (fixedArt)
            {
                var holder = UI.Fixed(Root, "FixedArt", new Vector2(0f, 0.5f), new Vector2(90f, 40f), new Vector2(760f, 520f), new Vector2(0f, 0.5f));
                holder.SetParent(_grid, false);
                holder.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // 그리드 칸 크기에 눌리지 않게
                holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one; holder.offsetMin = Vector2.zero; holder.offsetMax = Vector2.zero;
                UI.Bg(holder, new Color(0.13f, 0.15f, 0.20f, 1f), false);
                var port = UIPortrait.MakeForSlot(holder, slot, -1, 110f);
                port.anchorMin = port.anchorMax = new Vector2(0.5f, 0.58f);
                port.anchoredPosition = Vector2.zero;
                UI.Label(holder, $"<color=#{Roles.Hex(slot)}><b>{Roles.Name(slot)}</b></color> ({Roles.Nation(slot)})의 전용 캐릭터", 22, UI.Ink,
                    new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(700f, 34f), TextAnchor.MiddleCenter);
                UI.Label(holder, "이 역할은 직접 그린 캐릭터로 정해져 있어요. 이름만 입력하고 준비 완료!", 15, UI.InkDim,
                    new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(700f, 26f), TextAnchor.MiddleCenter);
                if (me != null && me.character < 0 && !me.ready && !_autoCharSent) { _autoCharSent = true; Session.Send("Char", 0); }
            }
            else
            {
                int count = GameApp.Instance != null ? Mathf.Max(1, GameApp.Instance.CharacterCount) : 9;
                for (int i = 0; i < count; i++) BuildCharCard(i, S, me);
            }

            if (_myTitle != null)
                _myTitle.text = $"내 역할: <color=#{Roles.Hex(slot)}><b>{Roles.Name(slot)}</b></color> ({Roles.Nation(slot)})";
            if (_ready != null && me != null)
            {
                UI.ButtonLabel(_ready).text = me.ready ? "준비 취소" : (me.character < 0 ? "캐릭터를 골라주세요" : "준비 완료 ✓");
                UI.SetButtonColor(_ready, me.ready ? UI.BtnWarn : UI.BtnGood);
                _ready.interactable = me.ready || me.character >= 0;
            }
            if (_name != null && me != null && !_name.isFocused && _name.text != me.name) _name.text = me.name;
            if (_name != null && me != null) _name.interactable = !me.ready;

            // 하단: 4명 상태
            UI.Clear(_status);
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                var cell = UI.Rect(_status, "P" + i);
                UI.Bg(cell, new Color(0.12f, 0.13f, 0.18f, 1f), false);
                UI.LE(cell, 280, 80, 1f);
                UI.HStack(cell, 10, 10, false, TextAnchor.MiddleLeft);
                var port = UIPortrait.MakeForSlot(cell, i, p.present ? p.character : -1, 22f);
                UI.LE(port, 52, 58, 0f);
                string ready = p.present ? (p.ready ? "<color=#7FE39B>준비 완료</color>" : "<color=#E0C080>고르는 중…</color>") : "빈 자리";
                bool fixedI = GameApp.Instance != null && GameApp.Instance.HasRoleArt(i);
                string charName = fixedI ? "전용 캐릭터" : GameApp.CharacterName(p.present ? p.character : -1);
                var info = UI.Text(cell, $"<color=#{Roles.Hex(i)}><b>{Roles.Name(i)}</b></color>  {Session.NameOf(i)}\n{charName} · {ready}",
                    14, UI.Ink, TextAnchor.MiddleLeft, true);
                UI.LE(info, -1, -1, 1f);
            }
        }

        private void BuildCharCard(int i, SessionState S, PlayerInfo me)
        {
            var card = UI.Rect(_grid, "Char" + i);
            var bg = card.gameObject.AddComponent<Image>();
            bool mine = me != null && me.character == i;
            int takenBy = -1;
            for (int s = 0; s < 4; s++) if (S.players[s].present && S.players[s].character == i && s != Session.LocalSlot) takenBy = s;
            bg.color = mine ? new Color(0.25f, 0.45f, 0.35f, 1f) : (takenBy >= 0 ? new Color(0.16f, 0.16f, 0.20f, 1f) : new Color(0.15f, 0.17f, 0.23f, 1f));

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            int idx = i;
            btn.onClick.AddListener(() => { if (Session.Me != null && !Session.Me.ready) Session.Send("Char", idx); });
            btn.interactable = me != null && !me.ready;

            var port = UIPortrait.Make(card, i, 42f);
            port.anchorMin = port.anchorMax = new Vector2(0.3f, 0.5f);
            port.anchoredPosition = new Vector2(0f, 4f);

            string taken = takenBy >= 0 ? $"\n<size=12><color=#{Roles.Hex(takenBy)}>{Session.NameOf(takenBy)} 선택</color></size>" : "";
            UI.Label(card, $"<b>{GameApp.CharacterName(i)}</b>{taken}", 17, UI.Ink, new Vector2(0.72f, 0.5f), Vector2.zero, new Vector2(120f, 80f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f));
            if (mine) UI.Label(card, "★ 내 선택", 13, UI.Gold, new Vector2(1f, 1f), new Vector2(-8f, -6f), new Vector2(90f, 20f), TextAnchor.MiddleRight);
        }
    }
}
