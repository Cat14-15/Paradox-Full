using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 내비게이터 전용 전체 지도. M 키로 열고 닫는다(X·Q·Esc 도 닫힌다).
    /// 방마다 이름과 그 방의 퍼즐을 보여 주고, 지금 있는 방과 동료들이 있는 방을 표시한다. 칸과 글자는 씬에 놓여 있다(Story1FlowBuilder).
    /// </summary>
    public sealed class Story1Map : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; }

        public Story1Scene scene;
        public CanvasGroup group;
        public CanvasGroup badge;                // 내비게이터일 때만 보이는 작은 "M 지도"
        public RectTransform[] tiles;            // scene.rooms 와 같은 순서
        public Image[] tileBack;
        public RectTransform[] dots;             // 역할 순서(조작자·통역·내비게이터·관측자)
        public Image[] dotImg;
        public PixelText youLabel;

        private static bool Allowed => Story1Role.Current == (int)GameRole.Navigator;

        private void Awake() { Close(); }

        private void Close()
        {
            IsOpen = false;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
        }

        private void Open()
        {
            IsOpen = true;
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            Sfx.Play("ui_select", 0.8f);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (badge != null)
            {
                bool showBadge = Allowed && !IsOpen && !Story1Scene.InputLocked && !Story1Flow.Ending;
                badge.alpha = showBadge ? 1f : 0f;
            }
            if (kb == null) return;
            if (IsOpen)
            {
                if (kb.mKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                { Close(); Sfx.Play("ui_back", 0.8f); return; }
                Refresh();
                return;
            }
            if (kb.mKey.wasPressedThisFrame && Allowed && !Story1Scene.InputLocked && !scene.Busy) { Open(); Refresh(); }
        }

        private int RoomIndexAt(Vector2 p)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < scene.rooms.Length; i++)
            {
                float d = ((Vector2)scene.rooms[i].transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private void Refresh()
        {
            int mine = scene.CurrentRoom != null ? System.Array.IndexOf(scene.rooms, scene.CurrentRoom) : -1;
            int me = Story1Role.Current;
            var count = new int[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                tileBack[i].color = i == mine ? new Color(0.36f, 0.27f, 0.12f, 0.95f) : new Color(0.12f, 0.15f, 0.2f, 0.95f);

            for (int r = 0; r < dots.Length; r++)
            {
                int room = mine;   // 봇은 주인공을 따라다닌다
                if (r != me && Story1Net.IsRemoteHuman(r) && Story1Net.RemoteFresh(r)) room = RoomIndexAt(Story1Net.RemotePos[r]);
                if (room < 0 || room >= tiles.Length) { dots[r].gameObject.SetActive(false); continue; }
                dots[r].gameObject.SetActive(true);
                var t = tiles[room];
                float step = 30f;
                int k = count[room]++;
                dots[r].SetParent(t, false);
                dots[r].anchoredPosition = new Vector2(t.rect.width * 0.5f - 24f - k * step, -t.rect.height * 0.5f + 22f);
                dotImg[r].color = Roles.Color(r);
            }
        }
    }
}
