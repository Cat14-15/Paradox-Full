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
        private int _lastFeed = -1;
        private int _flashCard = -1;
        private float _flashUntil;

        private void Awake()
        {
            transform.SetAsLastSibling();
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            for (int i = 0; i < labels.Length; i++) { labels[i].text = Story1Net.CardLabel[i]; labels[i].color = Tint[i]; }
            foreach (var r in feedRows) r.text = "";
        }

        private void Update()
        {
            bool show = Story1Net.HasHumanPeers && !Story1Flow.Ending;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            if (chatGroup != null) chatGroup.alpha = group.alpha;
            if (!show) return;

            var ms = Mouse.current;
            if (ms != null && Time.unscaledTime >= _nextSend)
            {
                Vector2 p = ms.position.ReadValue();
                for (int i = 0; i < cards.Length; i++)
                {
                    bool over = RectTransformUtility.RectangleContainsScreenPoint(cards[i], p, null);
                    var c = backs[i].color; c.a = over ? 0.95f : (i == _flashCard && Time.unscaledTime < _flashUntil ? 1f : 0.6f); backs[i].color = c;
                    if (over && ms.leftButton.wasPressedThisFrame)
                    {
                        _nextSend = Time.unscaledTime + 0.3f;
                        _flashCard = i; _flashUntil = Time.unscaledTime + 0.25f;
                        Story1Net.Send(Story1Net.Signal, i);
                        Paradox.Core.Sfx.Play("ui_select", 0.6f);
                    }
                }
            }

            // 채팅창: 최근 신호를 아래가 가장 최신이 되게 쌓아 보여 준다(오래된 것은 흐리게)
            int rows = feedRows.Length, cnt = Story1Net.Feed.Count;
            if (chatEmpty != null) chatEmpty.text = cnt == 0 ? "아래 카드를 눌러 신호를 보내요" : "";
            for (int r = 0; r < rows; r++)
            {
                int idx = cnt - rows + r;
                if (idx < 0) { feedRows[r].text = ""; continue; }
                var e = Story1Net.Feed[idx];
                feedRows[r].text = Roles.Name(e.slot) + "   " + Story1Net.CardLabel[Mathf.Clamp(e.card, 0, Story1Net.CardCount - 1)];
                var col = Roles.Color(e.slot);
                col.a = Time.time - e.t > 25f ? 0.55f : 1f;
                feedRows[r].color = col;
            }
            if (_lastFeed != Story1Net.FeedVersion) { _lastFeed = Story1Net.FeedVersion; Paradox.Core.Sfx.Play("ui_hover", 0.7f); }
        }
    }
}
