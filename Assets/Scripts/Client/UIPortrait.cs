using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// UI 초상. 플레이어 4역할은 픽셀 시트 앞모습(MakeForSlot), NPC는 코드로 그린 실루엣(Npc).
    /// 종이인형(Make)은 픽셀 시트가 없을 때의 폴백으로만 남겨 둔다.
    /// </summary>
    public static class UIPortrait
    {
        // Jovial 종이인형 파츠의 로컬 오프셋(월드 유닛, PPU 100) — 폴백 전용
        private static readonly Vector2 HeadOff = new Vector2(0f, 0.631f);
        private static readonly Vector2 LeftFootOff = new Vector2(-0.071f, -0.381f);
        private static readonly Vector2 RightFootOff = new Vector2(0.36f, -0.381f);

        /// <summary>[폴백] 종이인형 초상. size = 1유닛당 픽셀. 반환된 RectTransform은 (2.2 x 2.4)유닛 크기.</summary>
        public static RectTransform Make(Transform parent, int character, float size, bool flip = false, string name = "Portrait")
        {
            var root = UI.Rect(parent, name);
            root.sizeDelta = new Vector2(2.2f * size, 2.4f * size);
            var app = GameApp.Instance;
            if (app == null || character < 0 || app.CharacterPrefab(character) == null)
            {
                var ph = UI.Rect(root, "Placeholder");
                ph.anchorMin = ph.anchorMax = new Vector2(0.5f, 0.5f);
                ph.sizeDelta = new Vector2(1.2f * size, 1.6f * size);
                var img = ph.gameObject.AddComponent<Image>();
                img.sprite = SpriteLib.Disc();
                img.color = new Color(0.4f, 0.42f, 0.5f);
                img.raycastTarget = false;
                return root;
            }

            float dir = flip ? -1f : 1f;
            Part(root, app.PartSprite(character, "Shadow"), new Vector2(0.155f * dir, -0.539f), size, dir, "Shadow");
            Part(root, app.PartSprite(character, "Left_Foot"), new Vector2(LeftFootOff.x * dir, LeftFootOff.y), size, dir, "Left_Foot");
            Part(root, app.PartSprite(character, "Right_Foot"), new Vector2(RightFootOff.x * dir, RightFootOff.y), size, dir, "Right_Foot");
            Part(root, app.PartSprite(character, "Weapon"), new Vector2(0.655f * dir, 0.072f), size, dir, "Weapon");
            Part(root, app.PartSprite(character, "Body"), Vector2.zero, size, dir, "Body");
            Part(root, app.PartSprite(character, "Head"), new Vector2(HeadOff.x * dir, HeadOff.y), size, dir, "Head");
            return root;
        }

        private static void Part(RectTransform root, Sprite sprite, Vector2 offsetUnits, float size, float dir, string name)
        {
            if (sprite == null) return;
            var rt = UI.Rect(root, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.45f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float ppu = sprite.pixelsPerUnit > 0 ? sprite.pixelsPerUnit : 100f;
            rt.sizeDelta = new Vector2(sprite.rect.width / ppu * size, sprite.rect.height / ppu * size);
            rt.anchoredPosition = offsetUnits * size;
            rt.localScale = new Vector3(dir, 1f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }

        /// <summary>
        /// 역할 기준 초상: 그 역할의 픽셀 캐릭터 앞모습(도트 선명하게). 시트가 없을 때만 종이인형 폴백.
        /// 반환 크기 (2.2 x 2.4) x size.
        /// </summary>
        public static RectTransform MakeForSlot(Transform parent, int slot, int character, float size, bool flip = false, string name = "Portrait")
        {
            var app = GameApp.Instance;
            var sprite = app != null && app.HasRoleArt(slot) ? PixelSprites.Portrait(slot) : null;
            if (sprite == null) return Make(parent, character, size, flip, name);

            var root = UI.Rect(parent, name);
            root.sizeDelta = new Vector2(2.2f * size, 2.4f * size);
            var rt = UI.Rect(root, "Pixel");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // 32x64 픽셀 캐릭터를 2.3유닛 높이 칸에 맞춘다(폭은 절반)
            float h = 2.3f * size, w = h * 0.5f;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(0f, -0.05f * size);
            rt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return root;
        }

        /// <summary>역할 색 테두리가 있는 작은 카드(로비용).</summary>
        public static RectTransform Card(Transform parent, int slot, int character, float size)
        {
            var card = UI.Rect(parent, "PortraitCard");
            card.sizeDelta = new Vector2(2.4f * size, 2.6f * size);
            var bg = card.gameObject.AddComponent<Image>();
            var c = Roles.Color(slot);
            bg.color = new Color(c.r, c.g, c.b, 0.18f);
            bg.raycastTarget = false;
            var p = MakeForSlot(card, slot, character, size);
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f);
            p.anchoredPosition = Vector2.zero;
            return card;
        }

        /// <summary>NPC 실루엣 색(id별로 고정).</summary>
        public static Color NpcColor(int id)
        {
            switch (id % 6)
            {
                case 0: return new Color(0.36f, 0.58f, 0.62f);   // 청록(지호)
                case 1: return new Color(0.66f, 0.46f, 0.52f);   // 분홍 갈색(친구 1)
                case 2: return new Color(0.52f, 0.58f, 0.40f);   // 올리브(친구 2)
                case 3: return new Color(0.42f, 0.48f, 0.66f);   // 슬레이트(담임 선생님)
                case 4: return new Color(0.70f, 0.56f, 0.34f);   // 카키(캠프 스태프)
                default: return new Color(0.56f, 0.44f, 0.62f);  // 자주
            }
        }

        /// <summary>
        /// NPC(10+) 초상 — 코드로 그린 실루엣: 머리 원 + 어깨 + 이름 첫 글자. 반환 크기 (2.2 x 2.4) x size.
        /// 어깨 타원의 아랫부분은 RectMask2D 로 잘라 상반신처럼 보이게 한다.
        /// </summary>
        public static RectTransform Npc(Transform parent, int id, float size, string name = "Portrait")
        {
            var root = UI.Rect(parent, name);
            root.sizeDelta = new Vector2(2.2f * size, 2.4f * size);
            root.gameObject.AddComponent<RectMask2D>();
            var c = NpcColor(id);
            var dark = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f);
            var light = new Color(Mathf.Min(1f, c.r * 1.25f + 0.05f), Mathf.Min(1f, c.g * 1.25f + 0.05f), Mathf.Min(1f, c.b * 1.25f + 0.05f));

            // 어깨(넓은 타원, 아래쪽은 잘림)
            var body = UI.Rect(root, "Shoulders");
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(1.9f * size, 1.5f * size);
            body.anchoredPosition = new Vector2(0f, 0.30f * size);
            var bi = body.gameObject.AddComponent<Image>();
            bi.sprite = SpriteLib.Disc(0.05f); bi.color = dark; bi.raycastTarget = false;

            // 목
            var neck = UI.Rect(root, "Neck");
            neck.anchorMin = neck.anchorMax = new Vector2(0.5f, 0f);
            neck.pivot = new Vector2(0.5f, 0f);
            neck.sizeDelta = new Vector2(0.34f * size, 0.45f * size);
            neck.anchoredPosition = new Vector2(0f, 0.95f * size);
            var ni = neck.gameObject.AddComponent<Image>();
            ni.color = c; ni.raycastTarget = false;

            // 머리 원
            var head = UI.Rect(root, "Head");
            head.anchorMin = head.anchorMax = new Vector2(0.5f, 0f);
            head.pivot = new Vector2(0.5f, 0.5f);
            head.sizeDelta = new Vector2(0.95f * size, 1.0f * size);
            head.anchoredPosition = new Vector2(0f, 1.72f * size);
            var hi = head.gameObject.AddComponent<Image>();
            hi.sprite = SpriteLib.Disc(0.05f); hi.color = c; hi.raycastTarget = false;
            // 머리카락 느낌(윗부분 밝은 호)
            var hair = UI.Rect(head, "Hair");
            hair.anchorMin = new Vector2(0.08f, 0.55f); hair.anchorMax = new Vector2(0.92f, 1.0f);
            hair.offsetMin = Vector2.zero; hair.offsetMax = Vector2.zero;
            var hri = hair.gameObject.AddComponent<Image>();
            hri.sprite = SpriteLib.Disc(0.05f); hri.color = new Color(light.r, light.g, light.b, 0.35f); hri.raycastTarget = false;

            // 이름 첫 글자(몸통 가운데)
            string nm = Game.Npc.Name(id);
            string initial = string.IsNullOrEmpty(nm) ? "?" : nm.Substring(0, 1);
            var label = UI.Label(root, initial, Mathf.RoundToInt(0.55f * size), new Color(1f, 1f, 1f, 0.92f),
                new Vector2(0.5f, 0f), new Vector2(0f, 0.16f * size), new Vector2(1.2f * size, 0.7f * size), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
            label.fontStyle = FontStyle.Bold;
            return root;
        }
    }
}
