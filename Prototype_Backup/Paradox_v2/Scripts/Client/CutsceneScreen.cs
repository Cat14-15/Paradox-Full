using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 공용 컷씬(프롤로그 · 스테이지 도입 · 클리어 · 엔딩). 호스트가 진행하고 모두 같은 줄을 본다.
    /// 언어 장벽 대사는 해당 역할에게 물음표가 지글거리는 ???로 보인다.
    /// </summary>
    public class CutsceneScreen : ScreenBase
    {
        private Cutscene _cut;
        private RectTransform _bg;
        private Image _dark;
        private Text _title, _tag, _speaker, _body, _progress, _hint;
        private RectTransform _stage;   // 초상 영역
        private Button _next, _skip;
        private RectTransform _banner;

        private int _lineIndex = -1;
        private int _lineSlot = -2;
        private string _fullText = "";
        private bool _barrier;
        private float _typeT;
        private float _jiggleT;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            _cut = StoryData.Get(Session.State.cutsceneId);

            _bg = UI.Stretch(Root, "Bg");
            var top = _cut != null ? _cut.top : new Color(0.16f, 0.18f, 0.24f);
            var bottom = _cut != null ? _cut.bottom : new Color(0.07f, 0.08f, 0.11f);
            const int bands = 8;
            for (int i = 0; i < bands; i++)
            {
                var band = UI.Rect(_bg, "Band");
                band.anchorMin = new Vector2(0f, 1f - (i + 1) / (float)bands);
                band.anchorMax = new Vector2(1f, 1f - i / (float)bands);
                band.offsetMin = Vector2.zero; band.offsetMax = new Vector2(0f, 1f);
                UI.Bg(band, Color.Lerp(top, bottom, i / (float)(bands - 1)), false);
            }
            var darkRt = UI.Stretch(Root, "Dark");
            _dark = UI.Bg(darkRt, new Color(0f, 0f, 0f, 0f), false);

            _title = UI.Label(Root, _cut != null ? _cut.title : "", 22, new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(900f, 34f), TextAnchor.MiddleLeft);
            _progress = UI.Label(Root, "", 15, new Color(1f, 1f, 1f, 0.6f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(300f, 30f), TextAnchor.MiddleRight);

            _stage = UI.Fixed(Root, "Stage", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1300f, 420f));

            if (Session.State.phase == Phase.StageIntro) BuildStageBanner();

            var box = UI.Panel(Root, "TextBox", new Color(0.05f, 0.06f, 0.09f, 0.88f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1240f, 210f));
            _tag = UI.Label(box, "", 14, UI.Gold, new Vector2(0f, 1f), new Vector2(26f, -14f), new Vector2(500f, 22f), TextAnchor.MiddleLeft);
            _speaker = UI.Label(box, "", 20, UI.Gold, new Vector2(0f, 1f), new Vector2(26f, -40f), new Vector2(800f, 30f), TextAnchor.MiddleLeft);
            _body = UI.Label(box, "", 21, UI.Ink, new Vector2(0f, 1f), new Vector2(26f, -76f), new Vector2(1000f, 120f), TextAnchor.UpperLeft);
            _body.lineSpacing = 1.15f;

            var btnRow = UI.Fixed(box, "Buttons", new Vector2(1f, 0f), new Vector2(-20f, 18f), new Vector2(360f, 48f), new Vector2(1f, 0f));
            UI.HStack(btnRow, 0, 10, true, TextAnchor.MiddleRight);
            _skip = UI.Button(btnRow, "건너뛰기", UI.BtnColor, () => Session.Send("Skip"), 15, 40, 110);
            _next = UI.Button(btnRow, "다음 ▶  (Enter)", UI.BtnAccent, () => Session.Send("Next"), 17, 44, 200);
            _hint = UI.Label(box, "", 15, UI.InkDim, new Vector2(1f, 0f), new Vector2(-24f, 26f), new Vector2(420f, 30f), TextAnchor.MiddleRight);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 1f), new Vector2(0f, -6f), false);
            OnState();
        }

        private void BuildStageBanner()
        {
            var def = StageDefs.Get(Session.State.stage);
            _banner = UI.Panel(Root, "Banner", new Color(0.05f, 0.06f, 0.09f, 0.82f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 108f));
            UI.VStack(_banner, 14, 4, false, TextAnchor.UpperCenter);
            UI.Text(_banner, $"<b>{def.title.ToUpper()}</b>  ·  {def.country} ({def.code})  ·  {def.disaster}", 26, UI.Gold, TextAnchor.MiddleCenter, false, 36);
            string time = def.timeLimit > 0 ? $"제한시간 {def.timeLimit:0}초" : "시간 압박 없음";
            UI.Text(_banner, $"핵심 메커니즘: {def.mechanic}   ·   이번 현지인: <color=#{Roles.Hex(def.localSlot)}><b>{Roles.Full(def.localSlot)}</b></color>   ·   {time}", 15, UI.Ink, TextAnchor.MiddleCenter, false, 24);
            _bannerRole = UI.Text(_banner, "", 14, UI.InkDim, TextAnchor.MiddleCenter, false, 22);
            RefreshBannerRole();
        }

        private Text _bannerRole;

        private void RefreshBannerRole()
        {
            if (_bannerRole == null) return;
            var def = StageDefs.Get(Session.State.stage);
            int me = Session.LocalSlot;
            string mine = me == def.localSlot ? "당신만 표지판을 읽을 수 있습니다!" : "표지판이 ???로 보입니다 — 현지인의 전달에 의지하세요";
            _bannerRole.text = $"내 역할 {Roles.ColoredName(me)}: {mine}";
        }

        public override void OnState()
        {
            var S = Session.State;
            if (_cut == null || _cut.id != S.cutsceneId) _cut = StoryData.Get(S.cutsceneId);
            if (_cut == null) return;
            int idx = Mathf.Clamp(S.cutsceneIndex, 0, _cut.Count - 1);
            bool host = Session.IsHost;
            if (_next != null) _next.gameObject.SetActive(host);
            if (_skip != null) _skip.gameObject.SetActive(host);
            if (_hint != null) _hint.text = host ? "" : "호스트가 진행합니다…";
            if (_progress != null) _progress.text = $"{idx + 1} / {_cut.Count}";
            RefreshBannerRole();
            // 같은 줄이라도 내 시점(슬롯)이 바뀌면 언어 장벽 표시를 다시 계산한다
            if (idx == _lineIndex && Session.LocalSlot == _lineSlot) return;
            bool sameLine = idx == _lineIndex;
            _lineIndex = idx;
            _lineSlot = Session.LocalSlot;
            ShowLine(_cut.lines[idx], sameLine);
        }

        private void ShowLine(StoryLine line, bool keepTyping = false)
        {
            int me = Session.LocalSlot;
            _barrier = me >= 0 && (line.barrierMask & (1 << me)) != 0;
            _fullText = line.text;
            if (!keepTyping) _typeT = 0f;

            if (_tag != null) _tag.text = string.IsNullOrEmpty(line.sceneTag) ? "" : $"[ {line.sceneTag} ]";
            if (_speaker != null)
            {
                string sp = line.speaker;
                if (line.system) sp = "안내";
                else if (line.portrait >= 0 && line.portrait < 4)
                {
                    var p = Session.State.players[line.portrait];
                    sp = $"{line.speaker} <size=15><color=#{Roles.Hex(line.portrait)}>({p.name} · {Roles.Name(line.portrait)})</color></size>";
                }
                _speaker.text = sp;
                _speaker.color = line.system ? new Color(0.6f, 0.9f, 1f) : (string.IsNullOrEmpty(line.speaker) ? UI.InkDim : UI.Gold);
            }
            if (_body != null)
            {
                _body.fontStyle = line.system ? FontStyle.Normal : (string.IsNullOrEmpty(line.speaker) ? FontStyle.Italic : FontStyle.Normal);
                _body.color = line.system ? new Color(0.85f, 0.95f, 1f) : UI.Ink;
            }
            if (_dark != null) _dark.color = new Color(0f, 0f, 0f, line.dark ? 0.72f : 0f);

            UI.Clear(_stage);
            if (line.portrait >= 0)
            {
                int character = PortraitCharacter(line.portrait);
                bool right = line.portrait >= 10;
                var port = line.portrait < 4
                    ? UIPortrait.MakeForSlot(_stage, line.portrait, character, 130f, right)
                    : UIPortrait.Make(_stage, character, 130f, right);
                port.anchorMin = port.anchorMax = new Vector2(right ? 0.72f : 0.28f, 0.45f);
                port.anchoredPosition = Vector2.zero;
                string name = line.portrait < 4 ? Session.NameOf(line.portrait) : Npc.Name(line.portrait);
                UI.Label(_stage, name, 18, UI.Ink, new Vector2(right ? 0.72f : 0.28f, 0.45f), new Vector2(0f, -190f), new Vector2(300f, 30f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            }
            else if (line.system)
            {
                var icon = UI.Label(_stage, "협동 규칙", 40, new Color(0.6f, 0.9f, 1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 60f), TextAnchor.MiddleCenter);
            }
        }

        private static int PortraitCharacter(int portrait)
        {
            var S = Session.State;
            if (portrait >= 0 && portrait < 4)
            {
                var p = S.players[portrait];
                return p.character >= 0 ? p.character : portrait;
            }
            // NPC: 플레이어가 고르지 않은 캐릭터 중에서 결정적으로 배정
            int count = GameApp.Instance != null ? Mathf.Max(1, GameApp.Instance.CharacterCount) : 9;
            var used = new bool[count];
            foreach (var p in S.players) if (p.present && p.character >= 0 && p.character < count) used[p.character] = true;
            int k = portrait - 10;
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                if (used[i]) continue;
                if (found == k) return i;
                found++;
            }
            return (portrait * 3) % count;
        }

        public override void Tick()
        {
            _typeT += Time.deltaTime;
            _jiggleT += Time.deltaTime;
            if (_body != null)
            {
                if (_barrier)
                {
                    if (_jiggleT > 0.12f)
                    {
                        _jiggleT = 0f;
                        var sb = new System.Text.StringBuilder();
                        int words = Mathf.Clamp(_fullText.Length / 6, 3, 12);
                        for (int i = 0; i < words; i++)
                        {
                            int n = Random.Range(2, 5);
                            sb.Append('?', n).Append(' ');
                        }
                        _body.text = $"<color=#D08890>{sb}</color>\n<size=15><color=#8A7A80>(알아들을 수 없는 말이다)</color></size>";
                    }
                }
                else
                {
                    int chars = Mathf.Min(_fullText.Length, (int)(_typeT * 45f));
                    _body.text = _fullText.Substring(0, chars);
                }
            }

            if (Session.IsHost && !InteractionState.InputLocked)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                {
                    if (!_barrier && _fullText.Length > (int)(_typeT * 45f)) _typeT = 999f; // 먼저 전부 보여주기
                    else Session.Send("Next");
                }
            }
        }
    }
}
