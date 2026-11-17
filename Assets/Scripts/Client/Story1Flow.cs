using System.Collections;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// Story1 의 "흐름" 담당(씬에 놓인 UI): 왼쪽 위 목표 표시, 퍼즐을 끝낼 때마다 뜨는 도윤의 속마음, 최종 퍼즐 뒤의 엔딩(셔터 개방 → 컷씬 → 클리어 결과 → 스테이지 선택 복귀).
    /// 퍼즐 판정은 건드리지 않고 각 퍼즐의 Cleared / 전원 상태만 읽는다.
    /// </summary>
    public sealed class Story1Flow : MonoBehaviour
    {
        public static bool Ending { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Ending = false; }

        public Story1Scene scene;
        public string nextScene = "Lobby";

        [Header("목표 표시")]
        public RectTransform hudRect;
        public CanvasGroup hud;
        public PixelText hudTitle;
        public PixelText[] hudLines = new PixelText[3];

        [Header("도윤의 속마음")]
        public CanvasGroup thought;
        public PixelText thoughtName, thoughtText;

        [Header("엔딩")]
        public CanvasGroup ending;
        public Image flash;
        public Texture2D[] sheets = new Texture2D[4];   // 조작자·통역·내비게이터·관측자
        public Image[] heroes = new Image[4];
        public PixelText[] heroNames = new PixelText[4];
        public PixelText narName, narText, narHint;
        public PixelText resTitle, resTime, resBest, resHint;

        private bool _p0, _gallery, _relic, _water, _endStarted;
        private FuseBox _fuse; private GalleryFloor _galleryObj; private RelicCase _relicObj; private WaterValves _waterObj; private FinalFloor _finalObj;
        private readonly bool[] _applied = new bool[5];   // 이미 내 화면에 반영한 퍼즐
        private float _nextPos;
        private Vector2 _lastSent;
        private float _t0, _thoughtUntil;
        private Coroutine _thoughtCo;

        private void Awake()
        {
            transform.SetAsLastSibling();
            if (hud != null) hud.alpha = 0f;
            if (thought != null) thought.alpha = 0f;
            if (ending != null) { ending.alpha = 0f; ending.blocksRaycasts = false; }
            if (flash != null) flash.color = new Color(1, 1, 1, 0);
            for (int i = 0; i < heroes.Length; i++)
            {
                var sp = Story1Companions.Slice(sheets[i]);
                if (sp != null && sp.Length > PixelSprites.IdleFrame && sp[PixelSprites.IdleFrame] != null) heroes[i].sprite = sp[PixelSprites.IdleFrame];
                heroNames[i].text = Roles.Name(i); heroNames[i].color = Roles.Color(i);
            }
            SetEndingTexts(false);
        }

        private void Start()
        {
            _t0 = Time.time;
            _fuse = FindFirstObjectByType<FuseBox>(); _galleryObj = FindFirstObjectByType<GalleryFloor>(); _relicObj = FindFirstObjectByType<RelicCase>();
            _waterObj = FindFirstObjectByType<WaterValves>(); _finalObj = FindFirstObjectByType<FinalFloor>();
        }

        private void OnEnable() { Story1Net.Received += OnNet; Story1Scene.RoomEntered += OnRoom; Story1Net.RoundAdvanced += OnRound; }
        private void OnDisable() { Story1Net.Received -= OnNet; Story1Scene.RoomEntered -= OnRoom; Story1Net.RoundAdvanced -= OnRound; }

        private void OnRound(int id, int round) { Think("좋아, 한 번 더! (" + round + " / " + Story1Net.NeedRounds(id) + ") 이번엔 새 문제다.", 0.6f); }

        // ---- 방 이름(들어갈 때 잠깐)
        [Header("방 이름")]
        public CanvasGroup roomBanner;
        public PixelText roomBannerText;
        private Coroutine _bannerCo;

        // 방 이름 표는 퍼즐·창이 떠 있는 동안 숨긴다(퍼즐 제목과 겹치지 않게)
        private void LateUpdate() { if (roomBanner != null && Story1Scene.InputLocked) roomBanner.alpha = 0f; }

        private void OnRoom(string name)
        {
            if (roomBanner == null || string.IsNullOrEmpty(name)) return;
            if (_bannerCo != null) StopCoroutine(_bannerCo);
            _bannerCo = StartCoroutine(BannerRoutine(name));
        }

        private IEnumerator BannerRoutine(string name)
        {
            roomBannerText.text = name;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { roomBanner.alpha = t / 0.3f; yield return null; }
            roomBanner.alpha = 1f;
            yield return new WaitForSecondsRealtime(2f);
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { roomBanner.alpha = 1f - t / 0.6f; yield return null; }
            roomBanner.alpha = 0f;
            _bannerCo = null;
        }

        // 다른 사람이 퍼즐을 풀었다 — 내 화면에도 반영(열려 있던 패널은 잠깐 알려 주고 닫는다)
        private void OnNet(GameEvent e)
        {
            if (e.a != Story1Net.Clear) return;
            if (e.b >= 0 && e.b < _applied.Length && !Story1Net.IsFinal(e.b))   // 아직 다음 라운드가 남았다: 퍼즐을 끝내지 않고 새 문제만 알린다
            {
                if (!Story1Net.IsMine(e)) StartCoroutine(RemoteRound(e.b));
                return;
            }
            if (e.b >= 0 && e.b < _applied.Length) { if (Story1Net.IsMine(e)) { _applied[e.b] = true; return; } if (_applied[e.b]) return; _applied[e.b] = true; }
            if (Story1Net.IsMine(e)) return;
            StartCoroutine(RemoteClear(e.b));
        }

        private static bool LocalCleared(int id)
        {
            switch (id)
            {
                case Story1Net.PFuse: return Story1Blackout.PowerOn;
                case Story1Net.PGallery: return GalleryPanel.Cleared;
                case Story1Net.PRelic: return RelicPanel.Cleared;
                case Story1Net.PWater: return WaterPanel.Cleared;
                default: return FinalPanel.Cleared;
            }
        }

        private IEnumerator RemoteRound(int id)
        {
            PixelText status = null; bool open = false; System.Action close = null;
            switch (id)
            {
                case Story1Net.PFuse: if (_fuse != null) { open = FusePanel.IsOpen; status = _fuse.panel.status; close = _fuse.panel.Close; } break;
                case Story1Net.PGallery: if (_galleryObj != null) { open = GalleryPanel.IsOpen; status = _galleryObj.panel.status; close = _galleryObj.panel.Close; } break;
                case Story1Net.PRelic: if (_relicObj != null) { open = RelicPanel.IsOpen; status = _relicObj.panel.status; close = _relicObj.panel.Close; } break;
                case Story1Net.PWater: if (_waterObj != null) { open = WaterPanel.IsOpen; status = _waterObj.panel.status; close = _waterObj.panel.Close; } break;
            }
            Story1Net.AnnounceRound(id);
            if (open && status != null)
            {
                status.text = "동료가 풀었다! 새 문제가 나와요"; status.color = new Color(0.45f, 0.95f, 0.6f);
                Sfx.Play("power_on", 0.8f);
                yield return new WaitForSecondsRealtime(1.2f);
                close();
            }
        }

        private IEnumerator RemoteClear(int id)
        {
            PixelText status = null; bool open = false; System.Action close = null, solve = null;
            switch (id)
            {
                case Story1Net.PFuse: if (_fuse != null) { open = FusePanel.IsOpen; status = _fuse.panel.status; close = _fuse.panel.Close; solve = _fuse.OnSolved; } break;
                case Story1Net.PGallery: if (_galleryObj != null && !GalleryPanel.Cleared) { open = GalleryPanel.IsOpen; status = _galleryObj.panel.status; close = _galleryObj.panel.Close; solve = _galleryObj.OnSolved; } break;
                case Story1Net.PRelic: if (_relicObj != null && !RelicPanel.Cleared) { open = RelicPanel.IsOpen; status = _relicObj.panel.status; close = _relicObj.panel.Close; solve = _relicObj.OnSolved; } break;
                case Story1Net.PWater: if (_waterObj != null && !WaterPanel.Cleared) { open = WaterPanel.IsOpen; status = _waterObj.panel.status; close = _waterObj.panel.Close; solve = _waterObj.OnSolved; } break;
                case Story1Net.PFinal: if (_finalObj != null && !FinalPanel.Cleared && !FinalPanel.IsOpen) solve = _finalObj.OnSolved; break;   // 열려 있으면 패널이 직접 마무리한다
            }
            if (solve == null) yield break;
            if (open && status != null)
            {
                status.text = "동료가 해냈다!"; status.color = new Color(0.45f, 0.95f, 0.6f);
                Sfx.Play("power_on", 0.8f);
                yield return new WaitForSecondsRealtime(1.2f);
                close();
            }
            solve();
        }

        private void SetEndingTexts(bool result)
        {
            narName.gameObject.SetActive(!result); narText.gameObject.SetActive(!result); narHint.gameObject.SetActive(!result);
            resTitle.gameObject.SetActive(result); resTime.gameObject.SetActive(result); resBest.gameObject.SetActive(result); resHint.gameObject.SetActive(result);
        }

        private void Update()
        {
            if (Ending) return;

            // 단계가 끝난 순간 속마음 한 줄
            if (Story1Blackout.PowerOn && !_p0) { _p0 = true; Think("불이 켜졌다. 말은 안 통해도… 같이 해냈다.", 1.6f); }
            if (GalleryPanel.Cleared && !_gallery) { _gallery = true; Think("내 신호는 안 통했는데, 린이 길을 알려줬다.", 0.8f); }
            if (RelicPanel.Cleared && !_relic) { _relic = true; Think("복제품을 알아본 건 아이샤였다. 나 혼자선 몰랐을 거다.", 0.8f); }
            if (WaterPanel.Cleared && !_water) { _water = true; Think("마리아가 읽어 주지 않았다면 밸브도 못 돌렸을 거다.", 0.8f); }

            // 다른 사람이 이미 푼 퍼즐(내가 씬에 늦게 들어왔거나 이벤트를 놓친 경우)은 서버 상태에서 따라잡는다
            if (Story1Net.Online)
            {
                int done = Session.State.s1Done;
                for (int id = 0; id < _applied.Length; id++)
                    if ((done & (1 << id)) != 0 && !_applied[id]) { _applied[id] = true; if (!LocalCleared(id)) StartCoroutine(RemoteClear(id)); }
            }

            // 내 위치: 움직일 때 0.15초마다, 가만히 있어도 0.8초마다(동료 화면에서 사라지지 않게). 비신뢰 RPC 라 부담이 작다
            if (Story1Net.HasHumanPeers && Time.time >= _nextPos)
            {
                Vector2 pp = scene.player.position;
                bool moved = (pp - _lastSent).sqrMagnitude > 0.0004f;
                _nextPos = Time.time + (moved ? 0.15f : 0.8f);
                _lastSent = pp;
                Story1Net.SendPos(pp);
            }

            if (thought.gameObject.activeSelf == Story1Scene.InputLocked) thought.gameObject.SetActive(!Story1Scene.InputLocked);   // 퍼즐 화면 위에는 띄우지 않는다

            bool show = !Story1Scene.InputLocked && !scene.Busy;
            hud.alpha = Mathf.MoveTowards(hud.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            RefreshHud();

            if (FinalPanel.Cleared && !_endStarted) { _endStarted = true; StartCoroutine(EndingRoutine()); }
        }

        // ---- 목표
        // 줄 수에 맞춰 패널 높이를 줄이고, 위·아래·왼쪽 여백(17/17/30)이 항상 같도록 글자를 맞춘다
        private void LayoutHud(int lines)
        {
            const float top = 17f, titleH = 36f, gap = 6f, lineH = 36f, step = 38f, bottom = 17f;
            float h = top + titleH + gap + lines * step - (step - lineH) + bottom;
            hudRect.sizeDelta = new Vector2(hudRect.sizeDelta.x, h);
            hudRect.anchoredPosition = new Vector2(24f, -24f);   // 왼쪽 위 모서리 고정(피벗 왼쪽 위)
            hudTitle.rectTransform.anchoredPosition = new Vector2(0f, h / 2f - top - titleH / 2f);
            for (int i = 0; i < hudLines.Length; i++)
            {
                hudLines[i].gameObject.SetActive(i < lines);
                hudLines[i].rectTransform.anchoredPosition = new Vector2(0f, h / 2f - top - titleH - gap - lineH / 2f - i * step);
            }
        }

        private void RefreshHud()
        {
            if (!Story1Blackout.PowerOn)
            {
                hudTitle.text = "목표";
                hudLines[0].text = "입구 안내실의 퓨즈함으로 전원을 켜자";
                hudLines[1].text = ""; hudLines[2].text = "";
                LayoutHud(1);
                return;
            }
            bool g = GalleryPanel.Cleared, r = RelicPanel.Cleared, w = WaterPanel.Cleared;
            if (g && r && w)
            {
                hudTitle.text = "목표";
                hudLines[0].text = "중앙 홀 바닥의 원으로 가자";
                hudLines[1].text = "네 명이 동시에 밟아야 한다";
                hudLines[2].text = "";
                LayoutHud(2);
                return;
            }
            hudTitle.text = "목표 · 출구를 열 단서";
            hudLines[0].text = (g ? "[x] " : "[ ] ") + "회화 전시실: 물웅덩이 건너기";
            hudLines[1].text = (r ? "[x] " : "[ ] ") + "수장고: 유물 전시대 맞추기";
            hudLines[2].text = (w ? "[x] " : "[ ] ") + "학예실: 배수 밸브 돌리기";
            LayoutHud(3);
        }

        // ---- 속마음
        private void Think(string text, float delay)
        {
            if (_thoughtCo != null) StopCoroutine(_thoughtCo);
            _thoughtCo = StartCoroutine(ThinkRoutine(text, delay));
        }

        private IEnumerator ThinkRoutine(string text, float delay)
        {
            yield return new WaitForSeconds(delay);
            thoughtName.text = "도윤의 속마음"; thoughtName.color = Roles.Color((int)GameRole.Operator);
            thoughtText.text = text;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { thought.alpha = t / 0.3f; yield return null; }
            thought.alpha = 1f;
            yield return new WaitForSeconds(4.2f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { thought.alpha = 1f - t / 0.5f; yield return null; }
            thought.alpha = 0f;
            _thoughtCo = null;
        }

        // ---- 엔딩
        private static readonly string[][] Script =
        {
            new[] { "", "셔터가 올라가고, 바깥의 빛이 홀 안으로 들어왔다." },
            new[] { "", "서로의 말은 끝내 몰랐지만, 우리는 함께 해냈다." },
            new[] { "도윤", "생각해 보니까… 나 처음으로 뭘 몰라서 답답한 게 뭔지 알 것 같아." },
            new[] { "도윤", "얘네는 맨날 이런 기분이었나?" },
        };

        private static bool Pressed()
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                || (ms != null && ms.leftButton.wasPressedThisFrame);
        }

        private IEnumerator EndingRoutine()
        {
            Ending = true;
            hud.alpha = 0f; if (_thoughtCo != null) StopCoroutine(_thoughtCo); thought.alpha = 0f;
            SoloBots.Hide();
            float elapsed = Story1Net.Online ? Mathf.Max(0f, Session.Clock - Session.State.gameStartClock) : Time.time - _t0;   // 온라인은 모두가 같은 시간(서버 시계)

            // 셔터가 열리는 순간: 흰 번쩍임 + 소리
            Sfx.Play("power_on", 1f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { flash.color = new Color(1f, 0.97f, 0.85f, t / 0.5f); yield return null; }
            ending.alpha = 1f; ending.blocksRaycasts = true;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { flash.color = new Color(1f, 0.97f, 0.85f, 1f - t / 0.9f); yield return null; }
            flash.color = new Color(1, 1, 1, 0);
            yield return new WaitForSeconds(0.4f);

            for (int i = 0; i < Script.Length; i++)
            {
                narName.text = Script[i][0]; narName.color = Roles.Color((int)GameRole.Operator);
                narText.text = Script[i][1];
                narHint.text = i == Script.Length - 1 ? "Enter  결과 보기" : "Enter  다음";
                float shown = Time.time;
                yield return null;
                while (!(Pressed() && Time.time - shown > 0.35f) && Time.time - shown < 7f) yield return null;
                Sfx.Play("ui_select", 0.6f);
            }

            // 결과
            bool newBest = Progress.bestSeconds <= 0f || elapsed < Progress.bestSeconds;
            Progress.stage1Cleared = true;
            if (newBest) Progress.bestSeconds = elapsed;
            SetEndingTexts(true);
            resTitle.text = "STAGE 1 CLEAR";
            resTime.text = "클리어 시간   " + ResultScreen.FormatTime(elapsed);
            resBest.text = newBest ? "새 최고 기록!" : "최고 기록   " + ResultScreen.FormatTime(Progress.bestSeconds);
            resHint.text = "Enter  방으로";
            Sfx.Play("power_on", 0.8f);
            float rs = Time.time;
            yield return null;
            while (!(Pressed() && Time.time - rs > 0.8f)) yield return null;

            Sfx.Play("ui_select", 0.8f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { flash.color = new Color(0, 0, 0, t / 0.6f); yield return null; }
            flash.color = Color.black;
            Bgm.Stop();
            SceneManager.LoadScene(nextScene);
        }
    }
}
