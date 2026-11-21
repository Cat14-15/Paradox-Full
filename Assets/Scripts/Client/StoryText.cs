using System.Collections.Generic;
using System.Text.RegularExpressions;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Client
{
    /// <summary>
    /// 이야기 대사 공통 손질(프롤로그 · 캠프 · 박물관 이야기).
    ///  · 이름: 로비에서 정한 이름으로 바꾼다(서하린 = 0번 자리, 이안 · 린 · 카이도 같은 방식).
    ///  · 조사: 이름이 바뀌어도 이/가 · 은/는 · 을/를 · 과/와 · 아/야 가 맞게. 부를 때는 성을 뺀다("서하린아" → "하린아").
    ///  · 줄이기: 시작 전 이야기가 길다는 의견 → 장면에 저장된 대사 중 덜 중요한 줄을 실행할 때 뺀다(효과음·장면 전환은 다음 줄로 옮긴다).
    ///  · 건너뛰기: 마우스로 누를 수 있는 "건너뛰기 X" 버튼 + 연타로 여러 장면이 한꺼번에 넘어가지 않게 하는 쿨다운.
    /// </summary>
    public static class StoryText
    {
        // ---------------------------------------------------------------- 이름
        /// <summary>자리(slot)의 지금 이름. 로비에서 사람이 정한 이름이 있으면 그것, 아니면 기본 이름.</summary>
        public static string NameOf(int slot)
        {
            string def = Roles.DefaultName(slot);
            if (!Roles.Valid(slot) || !Session.Connected) return def;
            var ps = Session.State?.players;
            if (ps == null || slot >= ps.Length) return def;
            var p = ps[slot];
            if (p == null || !p.present || p.isBot || string.IsNullOrWhiteSpace(p.name)) return def;
            return p.name.Trim();
        }

        /// <summary>낱말 끝에 받침이 있는가(한글은 받침, 숫자는 읽는 소리, 영문은 자음으로 끝나면).</summary>
        public static bool HasBatchim(string w)
        {
            if (string.IsNullOrEmpty(w)) return false;
            char c = w[w.Length - 1];
            if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 != 0;
            if (c >= '0' && c <= '9') return "013678".IndexOf(c) >= 0;   // 영 일 삼 육 칠 팔
            char l = char.ToLowerInvariant(c);
            if (l >= 'a' && l <= 'z') return "aeiouyw".IndexOf(l) < 0;
            return false;
        }

        /// <summary>부를 때 쓰는 이름: 한글 세 글자면 성을 뺀다(서하린 → 하린).</summary>
        public static string CallName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Length == 3)
            {
                foreach (char c in name) if (c < 0xAC00 || c > 0xD7A3) return name;
                return name.Substring(1);
            }
            return name;
        }

        /// <summary>이름 + 조사(받침에 맞게). particle 은 원래 대사에 붙어 있던 조사.</summary>
        public static string WithJosa(string name, string particle)
        {
            if (string.IsNullOrEmpty(particle)) return name;
            bool b = HasBatchim(name);
            switch (particle)
            {
                case "아": case "야": { string n = CallName(name); return n + (HasBatchim(n) ? "아" : "야"); }
                case "이": case "가": return name + (b ? "이" : "가");
                case "은": case "는": return name + (b ? "은" : "는");
                case "을": case "를": return name + (b ? "을" : "를");
                case "과": case "와": return name + (b ? "과" : "와");
                case "이야": return name + (b ? "이야" : "야");
                case "이랑": case "랑": return name + (b ? "이랑" : "랑");
                case "이는": return name + (b ? "이는" : "는");
                default: return name + particle;
            }
        }

        private static readonly string[] Particles = { "에게는", "에게", "한테", "이야", "이랑", "이는", "처럼", "아", "야", "이", "가", "은", "는", "을", "를", "과", "와", "의", "도", "만", "랑", "께" };
        private static Regex _rx;
        private static Regex Rx
        {
            get
            {
                if (_rx != null) return _rx;
                var names = new List<string>(Roles.DefaultNames);
                names.Sort((a, b) => b.Length.CompareTo(a.Length));   // 긴 이름부터(서하린 안의 "린"이 먼저 잡히지 않게)
                string n = string.Join("|", names.ConvertAll(Regex.Escape));
                string p = string.Join("|", Particles);
                // 이름 앞은 한글이 아니어야 하고, 조사 뒤도 한글이 아니어야 한다(낱말 속 "린"·"카이로" 같은 것은 건드리지 않는다)
                _rx = new Regex("(?<![가-힣])(" + n + ")(" + p + ")?(?![가-힣])");
                return _rx;
            }
        }

        /// <summary>대사 한 줄의 이름과 조사를 지금 이름에 맞춘다. 기본 이름 그대로여도 부르는 말("서하린아")은 "하린아"로 고친다.</summary>
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Rx.Replace(text, m =>
            {
                int slot = Roles.SlotOfDefaultName(m.Groups[1].Value);
                string name = slot >= 0 ? NameOf(slot) : m.Groups[1].Value;
                return WithJosa(name, m.Groups[2].Success ? m.Groups[2].Value : "");
            });
            // 외국 친구가 부르는 로마자 이름
            string hero = NameOf(0);
            if (hero != Roles.DefaultName(0)) text = text.Replace("Seoharin", hero);
            return text;
        }

        /// <summary>말하는 사람 이름표(기본 이름이면 지금 이름으로).</summary>
        public static string Speaker(string who)
        {
            if (string.IsNullOrEmpty(who)) return who;
            int slot = Roles.SlotOfDefaultName(who);
            return slot >= 0 ? NameOf(slot) : who;
        }

        // ---------------------------------------------------------------- 줄이기
        // 줄의 앞부분으로 찾는다(장면 빌더 문구가 조금 바뀌어도 앞부분이 같으면 빠진다)
        private static readonly string[] CutPrologue =
        {
            "오, 진짜? 그럼 베트남어도",
            "아… 집에서는 조금 쓰는데",
            "서하린은 문제라고 느끼지 않고",
            "야야, 우리 조 누구야?",
            "오, 지호 우리 조야?",
            "어… 잘 부탁해.",
            "어, 어. 잘 부탁해.",
            "맞네, 서하린아",
            "발표는 내가 하는 게 낫지.",
            "서하린아, 끝나고 PC방",
            "당연하지! 먼저 가 있어",
            "어, 선생님. 무슨 일이세요?",
            "그래. 선생님들 사이에서",
            "그래, 준비 잘하고.",
            "선생님이 돌아가고, 신호등이",
        };

        private static readonly string[] CutCamp =
        {
            "치이익~ 버스 문이 열린다",
            "내가 대표로 뽑힌 거니까",
            "…무슨 말이지? 영어는 아닌데",
            "이안이 고개를 갸웃한다",
            "서-하-린. 서하린!",
            "맞아, 서하린! 너는?",
            "카이가 손을 번쩍 들어 하이파이브",
            "손짓이랑 표정으로는 얼추",
            "이안은 멈칫하다가 웃으며",
            "정신없다… 그래도 다들",
            "이안? 왜 그래? 하늘에",
            "…Ayos lang. Halika na.",
            "이안이 손짓으로 본관 쪽을",
            "오늘부터 진짜 시작이구나",
            "모두 모였죠? 만나서 반갑습니다",
            "첫 목적지가 박물관이래.",
            "박물관 안은 복잡하니까",
            "…Museum?",
            "이안이 알겠다는 듯 앞장서",
            "길은 쟤가 아는 것 같으니까",
            "우와, 크다… 박물관이",
            "카이는 조각상 앞에서 사진",
            "다들 표정이 심각해…",
            "린이 입구 쪽을 가리킨다",
            "세 사람이 번갈아 문을 밀어",
            "완전한 어둠. 바람 소리와",
            "어? 나… 아무것도 안 보이는데.",
            "뭐라고 하는 거지? 표정이",
            "내가 이끌어야 하는데… 왜 안",
            "네 사람이 서로를 바라본다",
            "[ 미션 2 ]",
        };

        private const string JihoBridge = "그때의 서하린은 아직 몰랐다. 말이 통하지 않는 곳에 혼자 남는 기분을. 지호가 매일 교실에서 조용히 견디던, 바로 그 기분을.";

        private static bool IsCut(string text, string[] cuts)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in cuts) if (text.StartsWith(c, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>장면에 저장된 대사에서 뺄 줄을 빼고, 빠진 줄의 효과음·배경 소리·음악·어둠·장면 전환은 다음 남는 줄로 옮긴다.</summary>
        public static PrologueLine[] Trim(PrologueLine[] lines, bool camp)
        {
            if (lines == null) return null;
            var cuts = camp ? CutCamp : CutPrologue;
            var keep = new List<PrologueLine>();
            PrologueLine carry = null;
            foreach (var src in lines)
            {
                if (src == null) continue;
                bool cut = string.IsNullOrEmpty(src.chapter) && IsCut(src.text, cuts);
                if (cut)
                {
                    if (carry == null) carry = new PrologueLine { dark = -1f, bg = -1 };
                    if (!string.IsNullOrEmpty(src.sfx)) { carry.sfx = src.sfx; carry.sfxVol = src.sfxVol; }
                    if (!string.IsNullOrEmpty(src.ambience)) { carry.ambience = src.ambience; carry.ambVol = src.ambVol; }
                    if (!string.IsNullOrEmpty(src.bgm)) carry.bgm = src.bgm;
                    if (src.dark >= 0f) carry.dark = src.dark;
                    continue;
                }
                if (carry != null)
                {
                    if (string.IsNullOrEmpty(src.sfx) && !string.IsNullOrEmpty(carry.sfx)) { src.sfx = carry.sfx; src.sfxVol = carry.sfxVol; }
                    if (string.IsNullOrEmpty(src.ambience) && !string.IsNullOrEmpty(carry.ambience)) { src.ambience = carry.ambience; src.ambVol = carry.ambVol; }
                    if (string.IsNullOrEmpty(src.bgm) && !string.IsNullOrEmpty(carry.bgm)) src.bgm = carry.bgm;
                    if (src.dark < 0f && carry.dark >= 0f) src.dark = carry.dark;
                    carry = null;
                }
                keep.Add(src);
            }
            // 프롤로그 끝: 왜 지호 이야기로 시작했는지 한 줄로 이어 준다(엔딩의 교실 장면과 짝)
            if (!camp && keep.Count > 0 && keep[keep.Count - 1].text != JihoBridge)
                keep.Add(new PrologueLine { speaker = "", text = JihoBridge, kind = 1, portrait = -1, focus = -1, chatter = 0f });
            return keep.ToArray();
        }

        // ---------------------------------------------------------------- 대화창 배치(프롤로그 · 캠프 · 박물관 공통)
        /// <summary>이름표 가운데 y · 대사 첫 줄 위 y(대화창 가운데 -365, 높이 330 기준). 말하는 사람·줄 수와 상관없이 고정.</summary>
        public const float NameY = -246f, BodyTop = -286f;

        /// <summary>
        /// 이 초상이 "내 캐릭터"인가(왼쪽에 놓는다). 초상 번호 0~3 = 서하린 · 이안 · 린 · 카이 자리.
        /// 프롤로그(한국 학교)는 주인공 서하린이 왼쪽, 캠프 · 박물관은 내가 맡은 역할이 왼쪽.
        /// </summary>
        public static bool IsMine(int portrait, bool roleScene)
        {
            if (!roleScene) return portrait == 0;
            int me = Story1Role.Current;
            if (!Roles.Valid(me)) me = 0;
            return portrait == me;
        }

        /// <summary>캔버스 폭(캔버스 단위).</summary>
        public static float CanvasWidth(RectTransform anyChild)
        {
            if (anyChild == null) return 1920f;
            var cv = anyChild.GetComponentInParent<Canvas>();
            if (cv == null) return 1920f;
            var root = cv.rootCanvas.GetComponent<RectTransform>();
            return root != null && root.rect.width > 1f ? root.rect.width : 1920f;
        }

        /// <summary>화면에 꽉 찬 묶음(대화창 등)을 폭 needW 가 화면 안에 들어오게 줄인다(넓은 화면은 그대로).</summary>
        public static void FitWidth(RectTransform root, float needW)
        {
            if (root == null) return;
            if (root.pivot != new Vector2(0.5f, 0f)) root.pivot = new Vector2(0.5f, 0f);   // 아래 가운데 기준으로 줄인다(대화창이 바닥에 붙어 있게)
            float s =Mathf.Clamp((CanvasWidth(root) - 32f) / needW, 0.4f, 1f);
            if (Mathf.Abs(root.localScale.x - s) > 0.001f) root.localScale = new Vector3(s, s, 1f);
        }

        // ---------------------------------------------------------------- 건너뛰기
        private static float _lastSkip = -99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _lastSkip = -99f; _rx = null; }

        /// <summary>건너뛰기 버튼 글자.</summary>
        public static string SkipLabel => "건너뛰기  X";

        /// <summary>
        /// 이번 프레임에 건너뛰기를 눌렀는가(X · Esc 키, 또는 버튼을 마우스로).
        /// shownFor: 지금 이야기가 화면에 보인 지 몇 초. 연타 방지 — 이야기가 뜨고 0.8초 안, 또는 방금 건너뛴 뒤 1.2초 안에는 무시한다.
        /// </summary>
        /// <summary>이번 프레임에 화면을 눌렀는가(마우스 왼쪽 또는 새 터치) · 누른 자리.</summary>
        public static bool Tap(out Vector2 pos)
        {
            pos = new Vector2(-9999f, -9999f);
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null) foreach (var t in ts.touches) if (t.press.wasPressedThisFrame) { pos = t.position.ReadValue(); return true; }
            var ms = Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame) { pos = ms.position.ReadValue(); return true; }
            return false;
        }

        public static bool Tap() => Tap(out _);

        private static bool Inside(RectTransform r, Vector2 p)
        {
            if (r == null || !r.gameObject.activeInHierarchy) return false;
            var cv = r.GetComponentInParent<Canvas>();
            Camera cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(r, p, cam);
        }

        public static bool SkipPressed(RectTransform button, float shownFor)
        {
            var kb = Keyboard.current;
            bool key = kb != null && (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
            bool click = Tap(out var tp) && Inside(button, tp);   // 마우스·터치 공통
            if (!key && !click) return false;
            if (shownFor < 0.8f || Time.unscaledTime - _lastSkip < 1.2f) return false;
            _lastSkip = Time.unscaledTime;
            return true;
        }

        /// <summary>마우스가 버튼 위에 있는가(클릭이 "다음 대사"로도 처리되지 않게 확인용).</summary>
        public static bool Over(RectTransform r)
        {
            if (Tap(out var tp) && Inside(r, tp)) return true;   // 터치로 버튼을 누른 프레임
            var ms = Mouse.current;
            if (r == null || ms == null || !r.gameObject.activeInHierarchy) return false;
            var cv = r.GetComponentInParent<Canvas>();
            Camera cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(r, ms.position.ReadValue(), cam);
        }

        /// <summary>건너뛰기 알약(배경 이미지 + 글자)을 버튼처럼: 크기·글자를 맞추고, 마우스를 올리면 밝아진다.</summary>
        public static void StyleSkip(RectTransform pill, PixelText label, UnityEngine.UI.Image bg)
        {
            if (pill == null) return;
            pill.sizeDelta = new Vector2(300f, 64f);
            if (label != null)
            {
                label.text = SkipLabel;
                label.scale = 3;
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
                label.maxWidth = 284f;
            }
            if (bg != null) bg.raycastTarget = false;
        }

        /// <summary>마우스가 올라가 있으면 알약을 밝게.</summary>
        public static void HoverSkip(RectTransform pill, UnityEngine.UI.Image bg)
        {
            if (bg == null) return;
            bool on = Over(pill);
            bg.color = on ? new Color(0.16f, 0.2f, 0.26f, 0.92f) : new Color(0.04f, 0.05f, 0.08f, 0.72f);
        }
    }
}
