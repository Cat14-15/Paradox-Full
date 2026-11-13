using UnityEngine;

namespace Paradox.Language
{
    /// <summary>
    /// 오버월드에 2.5D 감성의 바닥과 소품(덤불)을 런타임에 생성한다.
    /// 스프라이트를 코드로 만들기 때문에 별도 애셋/배치가 필요 없다.
    ///
    /// - 바닥: 잔디 타일을 넓게 깔아 걸어다닐 평면을 만든다.
    /// - 덤불: Y축 깊이 정렬(YDepthSort)이 붙어, 플레이어가 사이를 지나가면
    ///   자연스럽게 앞/뒤로 가려진다.
    /// </summary>
    public class SceneDressing : MonoBehaviour
    {
        [SerializeField] private float groundSize = 60f;

        // 덤불 위치(플레이어 0,0 ~ 퍼즐지점 3,0 주변에 흩뿌리되 길목은 비움)
        // 장식 덤불은 혼란을 줘서 제거함(바닥만 유지). 필요하면 좌표를 다시 넣으면 됨.
        private static readonly Vector2[] BushSpots = new Vector2[0];

        private Texture2D _grassTex;
        private Texture2D _bushTex;

        private void Awake()
        {
            BuildGround();
            foreach (var spot in BushSpots) BuildBush(spot);
        }

        private void BuildGround()
        {
            _grassTex = MakeGrassTile(128);
            var sprite = Sprite.Create(_grassTex, new Rect(0, 0, 128, 128),
                new Vector2(0.5f, 0.5f), 128f, 0, SpriteMeshType.FullRect);

            var go = new GameObject("Ground");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(1.5f, 0f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(groundSize, groundSize);
            sr.sortingOrder = -1000; // 항상 가장 뒤
        }

        private void BuildBush(Vector2 pos)
        {
            if (_bushTex == null) _bushTex = MakeBushTex(72);

            var sprite = Sprite.Create(_bushTex, new Rect(0, 0, _bushTex.width, _bushTex.height),
                new Vector2(0.5f, 0.12f), 64f, 0, SpriteMeshType.FullRect); // 피벗을 아래쪽에

            var go = new GameObject("Bush");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;

            go.AddComponent<YDepthSort>(); // 깊이 정렬(SortingGroup 자동 추가)
        }

        // ---------- 텍스처 생성 ----------

        private static Texture2D MakeGrassTile(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point; // 또렷한 픽셀(도착존·위험존과 톤 통일)

            var cfg = Paradox.Stage.StageConfig.Current;
            Color baseA = cfg != null ? cfg.GroundColor : new Color(0.32f, 0.47f, 0.26f);
            Color baseB = new Color(baseA.r * 0.88f, baseA.g * 0.88f, baseA.b * 0.88f, 1f);
            Color seam = new Color(baseA.r * 0.72f, baseA.g * 0.72f, baseA.b * 0.72f, 1f);

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 16px 블록 체커로 은은한 결
                    bool checker = ((x / 16) + (y / 16)) % 2 == 0;
                    Color c = checker ? baseA : baseB;
                    // 타일 가장자리에 옅은 이음선
                    if (x == 0 || y == 0) c = seam;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeBushTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = size * 0.5f;
            float cy = size * 0.52f;
            float r = size * 0.42f;

            Color leaf = new Color(0.30f, 0.55f, 0.25f);
            Color leafDark = new Color(0.20f, 0.40f, 0.18f);
            Color clear = new Color(0, 0, 0, 0);

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - cx) / r;
                    float dy = (y - cy) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    Color c;
                    if (d > 1f)
                    {
                        c = clear;
                    }
                    else
                    {
                        // 아래로 갈수록 어둡게(입체감), 가장자리는 부드럽게 페이드
                        float shade = Mathf.Lerp(1f, 0.72f, (float)y / size);
                        c = Color.Lerp(leafDark, leaf, shade);
                        c.a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.82f, 1f, d));
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
