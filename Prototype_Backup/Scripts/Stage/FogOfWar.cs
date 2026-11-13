using UnityEngine;

namespace Paradox.Stage
{
    /// <summary>
    /// 시야 제한(안개). 플레이어 주변만 보이고 나머지는 어둡게 가린다.
    /// 큰 맵에서 혼자서는 전체를 볼 수 없게 만들어, 내비게이터의 안내가 필요해진다.
    /// 플레이어를 따라다니는 큰 반투명 스프라이트에 가운데만 뚫린 텍스처를 쓴다.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        [Tooltip("훤히 보이는 반경(월드 유닛)")]
        [SerializeField] private float visionRadius = 5f;

        private Transform _player;
        private Transform _fog;

        private const float SpriteWorld = 64f; // 스프라이트가 덮는 월드 크기(화면보다 큼)

        private void Start()
        {
            var p = Object.FindFirstObjectByType<Player>();
            if (p != null) _player = p.transform;
            Build();
        }

        private void Build()
        {
            var go = new GameObject("FogSprite");
            _fog = go.transform;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeFogSprite();
            sr.sortingOrder = 950; // 월드 위, UI(오버레이) 아래
            if (_player != null) _fog.position = new Vector3(_player.position.x, _player.position.y, 0f);
        }

        private void LateUpdate()
        {
            if (_player == null || _fog == null) return;
            _fog.position = new Vector3(_player.position.x, _player.position.y, 0f);
        }

        private Sprite MakeFogSprite()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            float worldPerPixel = SpriteWorld / size; // 유닛/픽셀
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float worldR = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) * worldPerPixel;
                    // 시야 안: 투명(0) → 시야 밖: 검정(1)
                    float a = Mathf.Clamp01(Mathf.InverseLerp(visionRadius, visionRadius + 2.0f, worldR));
                    px[y * size + x] = new Color(0.02f, 0.03f, 0.05f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            float ppu = size / SpriteWorld;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
