using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>UI에 종이인형 캐릭터 초상을 그린다(프리팹 파츠 스프라이트를 Image로 재구성).</summary>
    public static class UIPortrait
    {
        // Jovial 종이인형 파츠의 로컬 오프셋(월드 유닛, PPU 100)
        private static readonly Vector2 HeadOff = new Vector2(0f, 0.631f);
        private static readonly Vector2 LeftFootOff = new Vector2(-0.071f, -0.381f);
        private static readonly Vector2 RightFootOff = new Vector2(0.36f, -0.381f);

        /// <summary>size = 1유닛당 픽셀. 반환된 RectTransform은 (2.2 x 2.4)유닛 크기.</summary>
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
        /// 역할 기준 초상: 그 역할에 전용 픽셀 캐릭터가 있으면 그것(앞모습)을, 없으면 고른 종이인형 캐릭터를 그린다.
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
            // 32x64 픽셀 캐릭터를 종이인형과 비슷한 높이로(2.3유닛 높이 칸에 맞춤)
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

        /// <summary>역할 색 테두리가 있는 작은 카드(로비·선택 화면용).</summary>
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
    }
}
