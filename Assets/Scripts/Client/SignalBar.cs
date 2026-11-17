using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Client
{
    /// <summary>
    /// 신호 카드 띠(씬에 놓인 UI). 이 게임에는 채팅이 없다 — 말이 안 통하는 팀은 숫자·방향·색·예/아니오 카드로만 뜻을 주고받는다.
    /// 사람 동료가 있을 때만 보이고, 누르면 모두에게 "누가 어떤 카드를 냈다"가 전달된다(오른쪽 기록).
    /// </summary>
    public sealed class SignalBar : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; }

        public CanvasGroup group;
        public RectTransform[] cards = new RectTransform[Story1Net.CardCount];
        public PixelText[] labels = new PixelText[Story1Net.CardCount];
        public UnityEngine.UI.Image[] backs = new UnityEngine.UI.Image[Story1Net.CardCount];
        public CanvasGroup chatGroup;                       // 오른쪽 채팅창(신호 기록)
        public PixelText[] feedRows = new PixelText[8];
        public PixelText chatTitle, chatEmpty;

        private static readonly Color[] Tint =
        {
            Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white,
            new Color(0.92f, 0.27f, 0.27f), new Color(0.27f, 0.52f, 0.96f), new Color(0.98f, 0.80f, 0.22f), new Color(0.31f, 0.82f, 0.42f),
            new Color(0.45f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.45f), new Color(0.95f, 0.85f, 0.55f)
        };

        private float _nextSend, _hoverFlash;
        private int _lastFeed = -1, _scroll, _lastTotal;
        private int _flashCard = -1;
        private bool _expanded;
        private float _flashUntil;

        private void Awake()
        {
            transform.SetAsLastSibling();
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            for (int i = 0; i < labels.Length; i++) { labels[i].text = Story1Net.CardLabel[i]; labels[i].color = Tint[i]; }
            foreach (var r in feedRows) r.text = "";
        }

        /// <summary>신호 휠을 열거나 닫는다(C 키 또는 오른쪽 단추). 다른 화면(도움말·지도·나가기)이 떠 있을 때는 열지 않는다.</summary>
        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (Story1Flow.Ending || Story1Tutorial.IsOpen || Story1Map.IsOpen || Story1Pause.IsOpen || Story1RoomInfo.IsOpen) return;
            IsOpen = true;
            Paradox.Core.Sfx.Play("ui_select", 0.8f);
        }

        /// <summary>기록에 보이는 말한 사람: "이름(역할)". 이름이 기본값이거나 없으면 역할만.</summary>
        private static string SpeakerName(int slot)
        {
            string role = Roles.Name(slot);
            var ps = Session.State != null ? Session.State.players : null;
            string nm = ps != null && slot >= 0 && slot < ps.Length && ps[slot] != null ? ps[slot].name : null;
            if (string.IsNullOrEmpty(nm)) nm = Roles.DefaultName(slot);
            return nm + "(" + role + ")";
        }

        private void Close() { if (IsOpen) { IsOpen = false; Paradox.Core.Sfx.Play("ui_back", 0.6f); } }

        private void Update()
        {
            var kb = Keyboard.current;
            if (Story1Flow.Ending) IsOpen = false;
            else if (kb != null)
            {
                if (IsOpen && (kb.cKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) Close();
                else if (!IsOpen && kb.cKey.wasPressedThisFrame) Toggle();
            }
            group.alpha = Mathf.MoveTowards(group.alpha, IsOpen ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            group.blocksRaycasts = IsOpen; group.interactable = IsOpen;
            // 기록 창: 메시지가 있을 때만, 휠이 열려 있는 동안은 숨김. 마우스 휠로 지난 기록을 본다.
            int total = Story1Net.Feed.Count;
            bool puzzle = FusePanel.IsOpen || GalleryPanel.IsOpen || WaterPanel.IsOpen || FinalPanel.IsOpen || RelicPanel.IsOpen;
            // 기본은 간단히(3줄), 기록 창을 클릭하면 길게(8줄) ↔ 간단히 토글
            var click = Mouse.current;
            if (chatGroup != null && chatGroup.alpha > 0.5f && !puzzle && click != null && click.leftButton.wasPressedThisFrame
                && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)chatGroup.transform, click.position.ReadValue(), null))
            { _expanded = !_expanded; _scroll = 0; Paradox.Core.Sfx.Play("ui_select", 0.5f); }
            int vis = puzzle ? 2 : (_expanded ? feedRows.Length : 3);   // 퍼즐 화면 아래 안내 줄을 가리지 않게 퍼즐 중에는 2줄만
            if (chatGroup != null) chatGroup.alpha = Story1Flow.Ending || IsOpen || Story1Map.IsOpen || Story1Pause.IsOpen || Story1RoomInfo.IsOpen || Story1Tutorial.IsOpen || total == 0 ? 0f : 1f;
            if (total != _lastTotal) { _scroll = 0; _lastTotal = total; }
            var wheel = Mouse.current;
            if (wheel != null && total > vis)
            {
                float sy = wheel.scroll.ReadValue().y;
                if (sy > 0.1f) _scroll++; else if (sy < -0.1f) _scroll--;
                _scroll = Mathf.Clamp(_scroll, 0, total - vis);
            }
            bool show = IsOpen;

            var ms = Mouse.current;
            if (show && ms != null && Time.unscaledTime >= _nextSend)
            {
                Vector2 p = ms.position.ReadValue();
                for (int i = 0; i < cards.Length; i++)
                {
                    bool over = RectTransformUtility.RectangleContainsScreenPoint(cards[i], p, null);
                    var c = backs[i].color; c.a = over ? 0.95f : (i == _flashCard && Time.unscaledTime < _flashUntil ? 1f : 0.6f); backs[i].color = c;
                    if (over && ms.leftButton.wasPressedThisFrame)
                    {
                        _nextSend = Time.unscaledTime + 0.6f;
                        _flashCard = i; _flashUntil = Time.unscaledTime + 0.25f;
                        Story1Net.Send(Story1Net.Signal, i);
                        Paradox.Core.Sfx.Play("ui_select", 0.6f);
                        Close();
                        break;
                    }
                }
            }

            // 채팅창: 최근 신호를 아래가 가장 최신이 되게 쌓아 보여 준다(오래된 것은 흐리게)
            int rows = feedRows.Length, cnt = Story1Net.Feed.Count;
            if (chatEmpty != null) chatEmpty.text = "";
            if (chatTitle != null) chatTitle.text = puzzle ? "" : (_expanded ? "기록  ·  클릭하면 줄이기  ·  휠로 위아래" : "기록  ·  클릭하면 길게 보기");
            // 보이는 줄 수에 맞춰 창 높이를 줄인다(아래 모서리 고정, 사방 여백 17). 퍼즐 중에는 제목 없이 넓게
            int shown = Mathf.Clamp(cnt, 1, vis);
            float head = puzzle ? 0f : 48f;
            float h = 17f + head + shown * 40f - 4f + 17f;
            float w = puzzle ? 1100f : 680f;
            if (chatGroup != null)
            {
                var cr = (RectTransform)chatGroup.transform;
                if (Mathf.Abs(cr.sizeDelta.y - h) > 0.5f || Mathf.Abs(cr.sizeDelta.x - w) > 0.5f) cr.sizeDelta = new Vector2(w, h);
                if (chatTitle != null) { chatTitle.gameObject.SetActive(!puzzle); chatTitle.rectTransform.anchoredPosition = new Vector2(0f, h / 2f - 17f - 18f); }
            }
            for (int r = 0; r < rows; r++)
            {
                feedRows[r].gameObject.SetActive(r >= rows - shown);
                feedRows[r].rectTransform.sizeDelta = new Vector2(w - 34f, feedRows[r].rectTransform.sizeDelta.y);
                feedRows[r].rectTransform.anchoredPosition = new Vector2(0f, h / 2f - 17f - head - 18f - (r - (rows - shown)) * 40f);
                int idx = cnt - rows - _scroll + r;
                if (idx < 0) { feedRows[r].text = ""; continue; }
                var e = Story1Net.Feed[idx];
                feedRows[r].text = SpeakerName(e.slot) + "   " + (e.card < 0 ? e.text : Story1Net.CardLabel[Mathf.Clamp(e.card, 0, Story1Net.CardCount - 1)]);
                var col = Roles.Color(e.slot);
                col.a = Time.time - e.t > 25f ? 0.55f : 1f;
                feedRows[r].color = col;
            }
            if (_lastFeed != Story1Net.FeedVersion) { _lastFeed = Story1Net.FeedVersion; Paradox.Core.Sfx.Play("ui_hover", 0.7f); }
        }
    }
}
