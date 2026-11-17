using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 캐릭터 발밑 이름표. 내 캐릭터와 동료(봇·사람) 모두 이름이 보인다. 사람 동료는 로비에서 정한 닉네임, 봇은 기본 이름.
    /// 칸(글자·배경)은 씬에 놓여 있고(Story1FlowBuilder) 여기서는 위치와 글자만 갱신한다.
    /// </summary>
    public sealed class Story1NameTags : MonoBehaviour
    {
        public Story1Scene scene;
        public Story1Companions companions;
        public RectTransform canvasRect;
        public RectTransform[] tags = new RectTransform[4];
        public PixelText[] labels = new PixelText[4];

        private void LateUpdate()
        {
            if (scene == null || canvasRect == null || scene.cam == null) return;
            bool hideAll = Story1Scene.InputLocked || scene.Busy || Story1Flow.Ending;
            for (int r = 0; r < tags.Length; r++)
            {
                Vector2 pos = default; bool has = false;
                if (r == Story1Role.Current) { pos = scene.player.position; has = true; }
                else if (companions != null) has = companions.TryGetPos(r, out pos);
                bool show = has && !hideAll;
                if (tags[r].gameObject.activeSelf != show) tags[r].gameObject.SetActive(show);
                if (!show) continue;

                string name = Roles.DefaultNames[r];
                if (Story1Net.IsRemoteHuman(r))
                {
                    var p = Session.State.players[r];
                    if (p != null && !string.IsNullOrEmpty(p.name)) name = p.name;
                }
                else if (r == Story1Role.Current && Story1Net.Online)
                {
                    var p = Session.State.players[r];
                    if (p != null && !string.IsNullOrEmpty(p.name)) name = p.name;
                }
                string text = r == Story1Role.Current ? name + " (나)" : name;
                if (labels[r].text != text) labels[r].text = text;
                labels[r].color = Roles.Color(r);
                // 이름이 길어도 잘리지 않게 칸 폭을 글씨에 맞춘다
                var cs = labels[r].ContentSize; var sd = tags[r].sizeDelta; float tw = Mathf.Max(sd.x, cs.x + 36f);
                if (Mathf.Abs(sd.x - tw) > 0.5f && cs.x + 36f > sd.x) tags[r].sizeDelta = new Vector2(tw, sd.y);
                var lr = labels[r].rectTransform; if (lr.sizeDelta.x < cs.x + 4f && lr.anchorMin.x == lr.anchorMax.x) lr.sizeDelta = new Vector2(cs.x + 4f, lr.sizeDelta.y);

                var sp = RectTransformUtility.WorldToScreenPoint(scene.cam, new Vector3(pos.x, pos.y - 0.9f, 0f));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local)) tags[r].anchoredPosition = local;
            }
        }
    }
}
