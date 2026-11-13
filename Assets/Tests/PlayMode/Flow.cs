using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paradox.Tests
{
    /// <summary>화면 캡처(스크린샷 투어용). 환경변수 PARADOX_SHOTS 에 폴더가 있을 때만 동작한다.</summary>
    public static class Shots
    {
        public static string Dir => Environment.GetEnvironmentVariable("PARADOX_SHOTS");
        public static bool Enabled => !string.IsNullOrEmpty(Dir);
        public const int W = 1600, H = 900;

        public static IEnumerator Capture(string name)
        {
            if (!Enabled) yield break;
            Directory.CreateDirectory(Dir);
            yield return null;
            yield return null;

            var cam = Camera.main;
            var canvasGo = GameObject.Find("UIRootCanvas");
            var canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            Assert.IsNotNull(cam, "카메라 없음");
            Assert.IsNotNull(canvas, "UI 캔버스 없음");

            var rt = new RenderTexture(W, H, 24);
            var prevTarget = cam.targetTexture;
            var prevMode = canvas.renderMode;
            int prevOrder = canvas.sortingOrder;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 30000; // 실제 플레이(오버레이)처럼 UI가 월드 위에 오도록
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());

            cam.targetTexture = prevTarget;
            canvas.renderMode = prevMode;
            canvas.worldCamera = null;
            canvas.sortingOrder = prevOrder;
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            yield return null;
        }
    }

    /// <summary>
    /// 혼자 테스트(호스트 + 봇 3명) 세션을 코드로 몰아가는 공용 드라이버(STRANGER Stage 1).
    /// 모든 명령은 Session.Send 로 보내고, 서버 근접 조건을 맞추기 위해 먼저 담당 오브젝트 앞으로 텔레포트한다.
    /// 상태 단언은 전부 Until 로 감싼다(호스트 루프백이라도 즉시 단언은 하지 않는다).
    /// </summary>
    public static class Flow
    {
        /// <summary>역할 약자(영문) — 스크린샷 파일 이름용: 0 op(조작자) 1 in(통역) 2 na(내비게이터) 3 ob(관측자).</summary>
        public static readonly string[] RoleTag = { "op", "in", "na", "ob" };

        /// <summary>테스트가 쓰는 텔레포트 오프셋(담당 오브젝트 바로 아래).</summary>
        public static readonly Vector2 ApproachOffset = new Vector2(0f, -0.9f);

        // ------------------------------------------------------------------ 기본 도구

        public static IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            // 실제 경과 시간으로 잰다(프레임이 느려져 deltaTime이 잘려도 시간 초과가 늘어나지 않게)
            float start = Time.realtimeSinceStartup;
            float last = start;
            int frames = 0;
            while (!cond())
            {
                float now = Time.realtimeSinceStartup;
                if (now - last > 1.5f) Debug.LogWarning($"[Flow] 느린 프레임 {now - last:0.0}초 (대기: {what}, {frames}프레임)");
                last = now;
                float elapsed = now - start;
                if (elapsed > timeout)
                    Assert.Fail($"시간 초과: {what} ({Where()}, {Positions()}, {frames}프레임/{elapsed:0.0}초)");
                frames++;
                yield return null;
            }
            if (frames > 0) Debug.Log($"[Flow] 완료: {what} ({frames}프레임/{Time.realtimeSinceStartup - start:0.0}초)");
        }

        /// <summary>실패 메시지용 현재 위치 요약(phase/step/status + 관문 진행 값).</summary>
        public static string Where()
        {
            var s = Session.State;
            if (s == null) return "state=null";
            var st = s.st;
            string stage = st == null ? "" : $", step={st.step}, status='{st.status}', g1={st.gate1Done}, g2={st.gate2Done}, round={st.round}, penalty={st.penalty:0}, cleared={st.cleared}, failed={st.failed}";
            return $"local={LocalNav.Screen}, connected={Session.Connected}, phase={s.phase}, stage={s.stage}{stage}";
        }

        public static string Positions()
        {
            var sb = new System.Text.StringBuilder("pos:");
            for (int i = 0; i < 4; i++)
            {
                var p = Session.PosOf(i);
                sb.Append(p == null ? $" [{i}]?" : $" [{i}]({p.Value.x:0.0},{p.Value.y:0.0})");
            }
            return sb.ToString();
        }

        /// <summary>테스트가 실패하더라도 다음 테스트가 깨끗한 상태에서 시작하도록 세션을 끝낸다.</summary>
        public static IEnumerator Teardown()
        {
            TutorialOverlay.AutoSkip = true;
            if (NetHub.Instance != null && NetHub.Instance.IsRunning) NetHub.Instance.Shutdown();
            yield return null;
            yield return null;
        }

        /// <summary>시점·조종 슬롯 전환. 호스트는 봇 자리를 대신 조종할 수 있다(그 봇의 자동 행동은 멈춘다 — 봇 자율 테스트에서는 0번에만 쓸 것).</summary>
        public static void As(int slot)
        {
            Session.SetLocalSlot(slot);
            Assert.AreEqual(slot, Session.LocalSlot, $"슬롯 {slot} 조종 전환 실패");
        }

        public static void Teleport(int slot, Vector2 pos)
        {
            Assert.IsTrue(Session.Avatars.ContainsKey(slot), $"아바타 {slot} 없음");
            var av = Session.Avatars[slot];
            av.MoveTo(pos);
            if (slot == Session.LocalSlot && GameApp.Instance != null && GameApp.Instance.Follow != null)
                GameApp.Instance.Follow.SnapToTarget();
        }

        /// <summary>담당 오브젝트 앞에 서는 자리. 기본은 pos + (0,-0.9). 그 자리가 방 밖/벽이면(바닥 벽에 붙은 배전함·현황판) 위·옆으로 바꾼다.</summary>
        public static Vector2 Approach(string propId)
        {
            Assert.IsNotNull(Session.Map, "스테이지 맵 없음");
            var prop = Session.Map.Prop(propId);
            Assert.IsNotNull(prop, $"맵에 '{propId}' 오브젝트가 없음");
            var map = Session.Map;
            int room = map.RoomAt(prop.pos);
            Vector2[] offsets = { ApproachOffset, new Vector2(0f, 0.9f), new Vector2(-0.9f, 0f), new Vector2(0.9f, 0f) };
            foreach (var o in offsets)
            {
                var p = prop.pos + o;
                if (!map.Walkable(p, StageMaps.AvatarRadius)) continue;
                if (room >= 0 && map.RoomAt(p) != room) continue;
                return p;
            }
            return prop.pos + ApproachOffset;
        }

        /// <summary>슬롯을 담당 오브젝트 앞으로 텔레포트하고 한 프레임 쉰다(서버 Near 조건 + 클라 발광 조건).</summary>
        public static IEnumerator GoTo(int slot, string propId)
        {
            Teleport(slot, Approach(propId));
            yield return null;
        }

        /// <summary>시점을 바꾸고 그 슬롯을 오브젝트 앞으로 보낸다.</summary>
        public static IEnumerator AsAt(int slot, string propId)
        {
            As(slot);
            yield return GoTo(slot, propId);
        }

        public static IEnumerator Shot(Func<string, IEnumerator> shot, string name)
        {
            if (shot != null) yield return shot(name);
        }

        /// <summary>실제 시간으로 잠깐 기다린다(부정 검증: 이 동안 값이 바뀌지 않아야 할 때).</summary>
        public static IEnumerator WaitReal(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        // ------------------------------------------------------------------ HUD 배치 검사(회귀 방지)

        /// <summary>
        /// 우하단 역할 패널(내용에 따라 위로 늘어남)이 미니맵 아래 조종 전환 줄을 덮지 않는지.
        /// 한계 = 캔버스 높이 − 패널 아래 여백(RolePos.y) − 조종 전환 줄 아래 변(−SwitchPos.y + SwitchSize.y) − 8px.
        /// </summary>
        public static void AssertRolePanelFits(string where)
        {
            var ss = StageScreen.Instance;
            if (ss == null) return;
            var root = ss.RolePanelRect;
            Assert.IsNotNull(root, $"역할 패널 루트 없음 ({where})");
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            float limit = HudLayout.CanvasSize().y - HudLayout.RolePos.y - (-HudLayout.SwitchPos.y + HudLayout.SwitchSize.y) - 8f;
            Assert.LessOrEqual(root.rect.height, limit, $"역할 패널({where}, 슬롯 {Session.LocalSlot}) 높이 {root.rect.height:0}px 가 조종 전환 줄을 덮는다(한계 {limit:0}px)");
        }

        /// <summary>최종 집결 게이지 패널(중앙 상단)이 출구 셔터(라벨 포함)를 가리지 않는지 — 둘 다 뷰포트 좌표(0~1)로 바꿔 겹침을 본다.</summary>
        public static void AssertGatherPanelClearOfShutter()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Flow] Camera.main 없음 — 집결 패널/셔터 겹침 검사 생략"); return; }
            Vector3 a = cam.WorldToViewportPoint(new Vector3(StageWorld.ShutterX0, StageWorld.ShutterBottom, 0f));
            Vector3 b = cam.WorldToViewportPoint(new Vector3(StageWorld.ShutterX1, StageWorld.ShutterTopWithLabel, 0f));
            var shutter = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var canvas = HudLayout.CanvasSize();
            var pr = HudLayout.ToCanvasRect(HudLayout.GatherAnchor, HudLayout.GatherPos, HudLayout.GatherSize, canvas);
            var panel = Rect.MinMaxRect((pr.xMin + canvas.x * 0.5f) / canvas.x, (pr.yMin + canvas.y * 0.5f) / canvas.y,
                                        (pr.xMax + canvas.x * 0.5f) / canvas.x, (pr.yMax + canvas.y * 0.5f) / canvas.y);
            Assert.IsFalse(panel.Overlaps(shutter), $"집결 게이지 패널 {panel} 이 출구 셔터 {shutter} 를 가린다(뷰포트 좌표, 셔터 개방 연출이 안 보임)");
        }

        // ------------------------------------------------------------------ 재시도(튜토리얼 없음, 타이머 즉시 시작 — 명세 14-1 D1(2))

        /// <summary>
        /// 시간 초과 → failed → 호스트 Retry → 체크포인트 1(관문 2부터) 경로(QA test-T1).
        /// 720초를 기다리는 대신 호스트 권한 상태의 penalty 로 TimeLeft ≤ 0 을 만들어 GameLogic.Tick 의 실제 시간 초과 판정(Fail)을 태운다.
        /// 튜토리얼을 켠 채(AutoSkip=false)로 재시도 스테이지에 들어가 역할 팝업·코치마크가 뜨지 않고(입력 잠금 없음)
        /// 타이머가 바로 도는지, 시드·통계·스폰·라운드 초기화가 명세대로인지 확인한다. 끝나면 AutoSkip 을 복원한다.
        /// </summary>
        public static IEnumerator RetrySkipsTutorial()
        {
            TutorialOverlay.AutoSkip = false;
            try
            {
                var logic = SessionNet.Instance != null ? SessionNet.Instance.Logic : null;
                Assert.IsNotNull(logic, "호스트 Logic(테스트는 호스트와 같은 프로세스)");
                int seedBefore = Session.St.seed;
                int before = Session.St.retries;
                int statRetriesBefore = Session.State.stats.retries;
                float playBefore = Session.State.stats.playSeconds;
                Assert.IsTrue(Session.St.timerRunning, "실패 유도 전 타이머가 돌고 있어야 함");
                Assert.AreEqual(1, Session.St.checkpoint, "관문 1 통과 → 체크포인트 1 기록");
                Assert.AreEqual(-1, Session.St.lastPingRoom, "관문 1 해제 시 핑 초기화(Stage1Logic.DigitConfirm) — 실패 중 Ping 거부 검증의 전제");

                // 시간 초과: penalty = timeLimit → TimeLeft ≤ 0 → GameLogic.Tick 이 Fail
                logic.S.st.penalty = logic.S.st.timeLimit;
                logic.MarkDirty();
                yield return Until(() => Session.St.failed && Session.St.moveMode == 0, 4f, "시간 초과 → failed(이동 잠김)");
                Assert.LessOrEqual(Session.TimeLeft, 0f, "실패 시점의 남은 시간은 0 이하");
                Assert.Greater(Session.State.stats.playSeconds, playBefore, "실패한 구간도 플레이 시간에 누적된다");

                // 실패 중에는 스테이지 명령이 거부된다(Ping 은 근접 조건이 없어 실패가 아니면 항상 통한다)
                As(2);
                Session.Send("Ping", 3);
                yield return WaitReal(0.4f);
                Assert.AreEqual(-1, Session.St.lastPingRoom, "실패 중에는 스테이지 명령(Ping)이 거부되어야 함");
                As(0);

                Session.Send("Retry");
                yield return Until(() => Session.St.retries == before + 1 && !Session.St.failed && StageScreen.Instance != null, 5f, "재시도 스테이지 화면");
                yield return null;
                yield return null;
                Assert.IsTrue(Session.St.timerRunning, "재시도는 타이머 즉시 시작(D1-2)");
                Assert.IsNotNull(TutorialOverlay.Instance, "튜토리얼 오버레이 인스턴스");
                Assert.IsFalse(TutorialOverlay.Instance.Active, "재시도 스테이지에는 튜토리얼(역할 팝업·코치마크)이 뜨지 않아야 한다");
                Assert.IsFalse(InteractionState.InputLocked, "재시도 직후 이동 잠금이 없어야 한다");

                // 체크포인트·시드·통계·라운드 초기화(명세 5-1, 14-1 D1(2))
                Assert.AreEqual(1, Session.St.checkpoint, "재시도는 체크포인트를 유지");
                Assert.IsTrue(Session.St.gate1Done, "체크포인트 1 = 관문 1 통과 상태로 시작");
                Assert.AreEqual(1, Session.St.step, "체크포인트 1 = 관문 2부터");
                Assert.IsFalse(Session.St.gate2Done, "관문 2 는 다시");
                Assert.AreEqual(0, Session.St.round, "라운드 0 부터");
                Assert.AreEqual(0, Session.St.gate2Attempts, "관문 2 시도 횟수 초기화");
                Assert.AreEqual(0f, Session.St.penalty, 0.01f, "페널티 초기화");
                Assert.AreEqual(seedBefore + 17, Session.St.seed, "재시도 시드 = 이전 시드 + 17");
                Assert.AreEqual(statRetriesBefore + 1, Session.State.stats.retries, "통계 retries 증가");
                Assert.AreEqual(Session.St.retries, Session.State.stats.retries, "Stats.retries 와 StageState.retries 는 같아야 함");
                Assert.AreEqual(1, Session.St.moveMode, "재시도 뒤 이동 가능");
                Assert.AreEqual(-1, Session.St.dirCard, "재시도 뒤 방향 카드는 비어 있다");
                Assert.AreEqual(-1, Session.St.colorReport, "재시도 뒤 색 신고는 비어 있다");
                Assert.IsTrue(Stage1Logic.ValidateLadder(Session.St), "재시도로 새로 생성된 사다리도 규칙을 만족");
                yield return Until(() => Session.Avatars.Count == 4 && Session.Map != null, 5f, "재시도 스테이지 맵·아바타");
                yield return Until(() =>
                {
                    for (int i = 0; i < 4; i++) if (Session.RoomOf(i) != StageMaps.RoomAtrium) return false;
                    return true;
                }, 4f, "재시도 스폰은 아트리움(Teleport 이벤트)");
                yield return Until(() => Session.TimeLeft < Session.St.timeLimit - 0.15f, 4f, "재시도 타이머 감소");
                Assert.Greater(Session.TimeLeft, Session.St.timeLimit - 10f, "재시도 시간은 다시 12분에서 시작");
            }
            finally { TutorialOverlay.AutoSkip = true; }
        }

        // ------------------------------------------------------------------ 이벤트 검증 도우미

        /// <summary>내비 핑을 보내고 상태(lastPingRoom/lastPingClock)와 Ping 이벤트(slot=2, a=room, pos=방 입구 — 명세 7-2 계약)를 함께 검증한다(QA test-T8).</summary>
        public static IEnumerator PingAndVerify(int room, string what)
        {
            GameEvent ping = null;
            Action<GameEvent> rec = e => { if (e != null && e.type == "Ping") ping = e; };
            float clockBefore = Session.St.lastPingClock;
            Session.EventReceived += rec;
            try
            {
                As(2);
                Session.Send("Ping", room);
                yield return Until(() => Session.St.lastPingRoom == room && ping != null, 3f, what);
            }
            finally { Session.EventReceived -= rec; }
            Assert.AreEqual(2, ping.slot, "Ping 이벤트 slot = 내비게이터(말풍선이 뜨는 캐릭터, AvatarNet)");
            Assert.AreEqual(room, ping.a, "Ping 이벤트 a = 핑한 방");
            var door = StageMaps.RoomDoor(room);
            Assert.AreEqual(door.x, ping.pos.x, 0.01f, "비콘 위치 = 방 입구(x) — StageWorld 비콘");
            Assert.AreEqual(door.y, ping.pos.y, 0.01f, "비콘 위치 = 방 입구(y)");
            Assert.Greater(Session.St.lastPingClock, clockBefore, "핑 시각 갱신(미니맵 핀 10초 수명 기준)");
        }

        /// <summary>수신 로그에서 조건에 맞는 마지막 이벤트(없으면 null).</summary>
        public static GameEvent LastLogged(Func<GameEvent, bool> pred)
        {
            for (int i = Session.Log.Count - 1; i >= 0; i--)
                if (Session.Log[i].e != null && pred(Session.Log[i].e)) return Session.Log[i].e;
            return null;
        }

        /// <summary>결과 화면 "팀 기록" 패널에서 라벨(예: "오답 횟수")의 값 텍스트(리치 태그 제거)를 돌려준다. 없으면 실패(QA test-T9).</summary>
        public static string StatValue(string label)
        {
            var rs = GameObject.Find("ResultScreen");
            Assert.IsNotNull(rs, "ResultScreen 루트 없음");
            foreach (var t in rs.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                if (t.text != label || t.transform.parent == null || t.transform.parent.name != "Row") continue;
                var v = t.transform.parent.GetChild(1).GetComponent<UnityEngine.UI.Text>();
                Assert.IsNotNull(v, $"'{label}' 행의 값 Text 없음");
                return System.Text.RegularExpressions.Regex.Replace(v.text, "<.*?>", "");
            }
            Assert.Fail($"결과 화면에 '{label}' 행이 없음");
            return null;
        }

        /// <summary>
        /// 브리핑 게이트(v2 D1) 통과. briefAll 이면 4슬롯을 As()로 돌며 Brief 를 보낸다(봇 수동 테스트: 봇은 스스로 Brief 하지 않는다).
        /// 봇 자율 테스트는 As 를 0번 외에 쓰면 안 되므로 내 Brief 만 보내고 봇의 자동 Brief(1.5초)에 기댄다.
        /// </summary>
        public static IEnumerator BriefAll(bool briefAll)
        {
            if (briefAll)
            {
                Assert.IsFalse(Session.St.timerRunning, "스테이지 시작 직후에는 타이머가 멈춰 있어야 함(브리핑 게이트)");
                Assert.AreEqual(Session.St.timeLimit, Session.TimeLeft, 0.01f, "Brief 전 TimeLeft == timeLimit");
                Assert.AreEqual(Session.St.floodBase[StageMaps.RoomAquarium], Stage1Logic.Flood(Session.St, StageMaps.RoomAquarium, Session.Clock), 0.01f, "Brief 전 침수율은 기본값에서 정지");
                yield return WaitReal(0.5f);
                Assert.IsFalse(Session.St.timerRunning, "전원 Brief 전에는 타이머가 시작되면 안 됨");
                Assert.AreEqual(Session.St.timeLimit, Session.TimeLeft, 0.01f, "Brief 전에는 시간이 흐르지 않아야 함");
                // 브리핑 게이트 동안 서버는 관문 명령을 거부한다: 제어반 앞에서 Digit 을 보내도 calcInput 이 비어 있어야 한다
                yield return AsAt(0, "panel");
                Session.Send("Digit", 7);
                yield return WaitReal(0.4f);
                Assert.AreEqual("", Session.St.calcInput, "브리핑 게이트(timerRunning=false) 중 Digit 은 거부되어야 함");
                for (int s = 0; s < 4; s++)
                {
                    int slot = s;
                    As(slot);
                    Session.Send("Brief");
                    yield return Until(() => Session.St.briefDone != null && Session.St.briefDone[slot], 3f, $"슬롯 {slot} 브리핑 완료");
                    if (slot < 3)
                    {
                        // 게이트는 "present 전원"이다 — n/4 만 Brief 한 상태에서는 정지(QA test-T12)
                        yield return WaitReal(0.3f);
                        Assert.IsFalse(Session.St.timerRunning, $"{slot + 1}/4 만 Brief 한 상태에서는 타이머가 정지해야 함");
                        Assert.AreEqual(slot + 1, GameLogic.BriefCount(Session.St), "브리핑 완료 인원 표시(n/4)");
                    }
                }
                As(0);
                yield return null;
            }
            else
            {
                Session.Send("Brief");
                yield return Until(() => Session.St.briefDone != null && Session.St.briefDone[Session.LocalSlot], 3f, "내 브리핑 완료");
            }
            yield return Until(() => Session.St.timerRunning, briefAll ? 3f : 8f, "브리핑 게이트 해제(timerRunning)");
            yield return Until(() => Session.TimeLeft < Session.St.timeLimit - 0.15f, 4f, "Brief 후 타이머 감소");
            if (briefAll)
            {
                // 중복 Brief 는 startClock 을 다시 찍지 않는다(QA test-T12)
                float startClockAfter = Session.St.startClock;
                Session.Send("Brief");
                yield return WaitReal(0.3f);
                Assert.AreEqual(startClockAfter, Session.St.startClock, 0.0001f, "중복 Brief 는 startClock 을 다시 찍지 않는다");
                Assert.AreEqual(4, GameLogic.BriefCount(Session.St), "중복 Brief 로 인원이 늘지 않는다");
                // 게이트가 풀리면 같은 명령이 허용된다(슬롯 0은 위에서 제어반 앞에 세워 뒀다)
                Session.Send("Digit", 7);
                yield return Until(() => Session.St.calcInput == "7", 3f, "브리핑 게이트 해제 뒤 Digit 허용");
                Session.Send("DigitClear");
                yield return Until(() => Session.St.calcInput == "", 3f, "Digit 지우기");
            }
        }

        /// <summary>네 역할 시점을 차례로 캡처한 뒤 원래 슬롯으로 돌아온다. 파일명 "{prefix}_{s}{op|in|na|ob}". 봇 자율 모드에서는 쓰지 말 것(As 부작용).</summary>
        public static IEnumerator RoleViews(Func<string, IEnumerator> shot, string prefix)
        {
            if (shot == null) yield break;
            int prev = Session.LocalSlot;
            for (int s = 0; s < 4; s++)
            {
                As(s);
                yield return null;
                yield return shot($"{prefix}_{s}{RoleTag[s]}");
                AssertRolePanelFits(prefix);
            }
            As(prev);
            yield return null;
        }

        /// <summary>호스트가 컷씬을 건너뛰고 다음 단계가 될 때까지 기다린다.</summary>
        public static IEnumerator SkipCutscene(Phase expectNext, int expectStage, string what)
        {
            Session.Send("Skip");
            yield return Until(() => Session.State.phase == expectNext && (expectStage < 0 || Session.Stage == expectStage), 8f, what);
        }

        /// <summary>컷씬에서 host `Next`를 보내 원하는 줄(index)까지 진행한다. 이미 지나쳤으면 그대로 둔다.</summary>
        public static IEnumerator NextUntilLine(int index, float timeout = 10f)
        {
            float start = Time.realtimeSinceStartup;
            while (Session.State.cutsceneIndex < index)
            {
                if (Time.realtimeSinceStartup - start > timeout)
                    Assert.Fail($"시간 초과: 컷씬 {index}번째 줄까지 진행 (지금 {Session.State.cutsceneIndex}, {Where()})");
                int before = Session.State.cutsceneIndex;
                Session.Send("Next");
                yield return Until(() => Session.State.cutsceneIndex != before || !IsCutscenePhase(Session.State.phase), 3f, $"컷씬 다음 줄({before + 1})");
                if (!IsCutscenePhase(Session.State.phase)) yield break;
            }
        }

        /// <summary>네트워크 컷씬 단계인가(오프닝·클리어).</summary>
        public static bool IsCutscenePhase(Phase p) => p == Phase.StageIntro || p == Phase.StageClear;

        /// <summary>컷씬 id 안에서 특정 슬롯에게 ???로 보이는(언어 장벽) 첫 줄의 index. 없으면 -1.</summary>
        public static int BarrierLineIndex(string cutsceneId, int slot)
        {
            var cut = StoryData.Get(cutsceneId);
            if (cut == null) return -1;
            for (int i = 0; i < cut.lines.Count; i++)
                if ((cut.lines[i].barrierMask & (1 << slot)) != 0) return i;
            return -1;
        }

        // ------------------------------------------------------------------ 부팅 / 로컬 화면

        /// <summary>씬 로드 + 매칭 화면으로 바로 이동 + 튜토리얼 자동 건너뛰기. 4역할 픽셀 아트가 모두 배선돼 있어야 한다.</summary>
        public static IEnumerator Boot()
        {
            TutorialOverlay.AutoSkip = true;
            SceneManager.LoadScene("Paradox");
            yield return null;
            yield return null;
            Assert.IsNotNull(GameApp.Instance, "GameApp 없음");
            Assert.IsNotNull(NetHub.Instance, "NetHub 없음");
            for (int s = 0; s < 4; s++)
                Assert.IsTrue(GameApp.Instance.HasRoleArt(s), $"역할 {s}({Roles.Name(s)}) 픽셀 캐릭터 시트가 GameApp.roleSheets 에 없음");
            if (NetHub.Instance.IsRunning) NetHub.Instance.Shutdown();
            yield return null;
            Assert.IsFalse(Session.Connected, "부팅 직후에는 접속 전이어야 함");
            // 로고/타이틀/프롤로그를 건너뛰고 매칭 화면에서 시작한다(화면 루트 이름 검증은 FullFlow 의 ToLocal 이 맡는다)
            LocalNav.Go(LocalScreen.Match);
            yield return Until(() => LocalNav.Screen == LocalScreen.Match, 2f, "매칭 화면으로 이동");
            yield return null;
        }

        /// <summary>로컬 화면 전이. ScreenManager 가 만든 화면 루트(클래스 이름의 GameObject)가 생길 때까지 기다린다.</summary>
        public static IEnumerator ToLocal(LocalScreen screen, string screenRootName)
        {
            LocalNav.Go(screen);
            yield return Until(() => LocalNav.Screen == screen && !Session.Connected && GameObject.Find(screenRootName) != null, 4f, $"로컬 화면 {screen} ({screenRootName})");
            yield return null;
        }

        public static IEnumerator ToMatch() => ToLocal(LocalScreen.Match, "MatchScreen");

        /// <summary>혼자 테스트(호스트 + 봇 3). botsAuto=false 이면 봇이 스스로 행동하지 않아 테스트가 네 역할을 직접 조작한다(결정적 검증용).</summary>
        public static IEnumerator StartSolo(bool botsAuto = false)
        {
            Assert.IsTrue(NetHub.Instance.StartHost(true), "솔로(Single) 시작 실패");
            yield return Until(() => Session.Connected && Session.State.phase == Phase.Lobby && Session.State.PresentCount == 4, 15f, "로비 + 봇 3명");
            Assert.AreEqual(0, Session.MySlot, "호스트는 0번(조작자) 자리");
            Assert.IsTrue(Session.State.players[1].isBot && Session.State.players[2].isBot && Session.State.players[3].isBot, "1~3번 자리는 봇이어야 함");
            Session.Send("BotsAuto", botsAuto ? 1 : 0);
            yield return Until(() => Session.State.botsAuto == botsAuto, 3f, "봇 자동 설정");
        }

        /// <summary>로비 → 이름 설정 → 준비 완료 → 오프닝(건너뛰기) → 스테이지 플레이(아바타 4개, 맵) → 브리핑 게이트 통과(타이머 시작).</summary>
        public static IEnumerator LobbyToStage(string name = "테스터", Func<string, IEnumerator> shot = null, bool briefAll = true)
        {
            Session.Send("StartGame");
            yield return Until(() => Session.State.phase == Phase.NameSetup, 5f, "이름 설정 화면");
            yield return Shot(shot, "06_names");
            Session.Send("Name", 0, 0, name);
            yield return Until(() => Session.Me != null && Session.Me.name == name, 3f, $"내 이름 '{name}' 반영");
            Session.Send("Ready", 1);
            yield return Until(() => Session.State.phase == Phase.Ready, 5f, "준비 완료 화면");
            yield return Shot(shot, "07_ready");
            yield return Until(() => Session.State.phase == Phase.StageIntro, 8f, "오프닝 컷씬(서버 자동 전환)");
            Assert.AreEqual("s1_intro", Session.State.cutsceneId, "오프닝 컷씬 id");
            yield return SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이");
            yield return Until(() => Session.Avatars.Count == 4, 10f, "아바타 4개 스폰");
            yield return Until(() => Session.Map != null, 5f, "스테이지 맵 생성");
            yield return Until(() => Session.St.step == 0 && !Session.St.gate1Done, 3f, "관문 1 시작 상태");
            yield return BriefAll(briefAll);
        }

        // ------------------------------------------------------------------ 관문 1

        /// <summary>
        /// 관문 1(계산 자물쇠)을 네 역할을 번갈아 조종해 푼다.
        /// 내비 핑 → 관측자 색 신고 → 조작자 홀짝 채팅 → 통역 숫자 카드 → 조작자 오답 1회(페널티 확인) → 정답.
        /// 정답은 제어반 팝업을 연 채로 입력해 "완료 ✓" 상태(3초 뒤 자동 닫힘, D6c)까지 확인한다. shot 이 있으면 그 화면을 찍는다.
        /// </summary>
        public static IEnumerator Gate1Manual(Func<string, IEnumerator> shot = null)
        {
            Assert.IsNotNull(Session.Map, "Stage 1 맵 없음");

            // 내비게이터: "여기로!" 안내데스크(방 1) — 상태 + 이벤트 내용 검증
            yield return PingAndVerify(1, "내비 핑(안내데스크)");

            // 내비가 관문 1 중 현황판을 열어 둔 상태("지금 할 일" + 침수 격자 + 안전 라인)에서 역할 패널이 조종 전환 줄을 덮지 않는지
            yield return AsAt(2, "board");
            Session.Send("OpenBoard");
            yield return Until(() => Session.St.boardOpened, 3f, "침수 현황판 열기(관문 1)");
            yield return null;
            AssertRolePanelFits("관문 1 · 내비 · 현황판 열림");

            // 관측자: 경고등 색 신고 → 통역에게
            int lamp = Session.St.lampColor;

            // ---- 게이팅 부정 검증(역할·근접·단계, QA test-T5): 서버가 거부해야 하므로 값이 바뀌지 않아야 한다 ----
            Assert.AreEqual(-1, Session.St.colorReport, "관문 1 시작 시 색 신고는 비어 있어야 함");
            Assert.AreEqual(-1, Session.St.numberCard, "관문 1 시작 시 숫자 카드는 비어 있어야 함");
            // 역할: 조작자가 램프 앞에서 Color → 무시(slot != Ob). 근접을 맞춰 두어 역할 검사만 시험한다
            yield return AsAt(0, "lamp");
            Session.Send("Color", lamp);
            yield return WaitReal(0.3f);
            Assert.AreEqual(-1, Session.St.colorReport, "조작자의 Color 는 무시(관측자만)");
            // 근접: 관측자가 스폰 자리(램프에서 약 12유닛)에서 Color → 거부(NearReach 2.2)
            As(3); Teleport(3, Session.Map.spawns[3]); yield return null;
            Session.Send("Color", lamp);
            yield return WaitReal(0.3f);
            Assert.AreEqual(-1, Session.St.colorReport, "램프에서 멀면 Color 거부(NearReach 2.2)");
            // 역할: 조작자가 벽보 앞에서 Card → 무시(slot != In)
            yield return AsAt(0, "poster");
            Session.Send("Card", 3);
            yield return WaitReal(0.3f);
            Assert.AreEqual(-1, Session.St.numberCard, "조작자의 Card 는 무시(통역만)");
            // 근접: 통역이 스폰 자리에서 Card → 거부
            As(1); Teleport(1, Session.Map.spawns[1]); yield return null;
            Session.Send("Card", 3);
            yield return WaitReal(0.3f);
            Assert.AreEqual(-1, Session.St.numberCard, "벽보에서 멀면 Card 거부");
            // 근접: 내비가 스폰 자리에서 OpenBoard → 거부. 투어는 23_popup_board_na 에서 이미 열었으므로 '변하지 않음'으로 단언
            bool boardBefore = Session.St.boardOpened;
            As(2); Teleport(2, Session.Map.spawns[2]); yield return null;
            Session.Send("OpenBoard");
            yield return WaitReal(0.3f);
            Assert.AreEqual(boardBefore, Session.St.boardOpened, "현황판에서 멀면 OpenBoard 거부");
            // 역할·범위: 조작자의 Ping / 방 번호 범위 밖 Ping → 무시(직전 핑 1 유지)
            As(0); Session.Send("Ping", 3);
            yield return WaitReal(0.3f);
            Assert.AreEqual(1, Session.St.lastPingRoom, "조작자의 Ping 은 무시(내비만)");
            As(2); Session.Send("Ping", StageMaps.RoomCount);
            yield return WaitReal(0.3f);
            Assert.AreEqual(1, Session.St.lastPingRoom, "방 번호 범위 밖 Ping 무시");
            // 역할: 통역이 제어반 앞에서 Digit → 무시(slot != Op)
            yield return AsAt(1, "panel");
            Session.Send("Digit", 5);
            yield return WaitReal(0.3f);
            Assert.AreEqual("", Session.St.calcInput, "통역의 Digit 은 무시(조작자만)");
            // 단계: 관문 1 전에는 DirCard / Lever / Judge 가 거부된다
            float penalty0 = Session.St.penalty;
            yield return AsAt(1, "mural"); Session.Send("DirCard", 0); yield return WaitReal(0.3f);
            Assert.AreEqual(-1, Session.St.dirCard, "관문 1 전 DirCard 거부");
            yield return AsAt(0, "lever"); Session.Send("Lever", 0); yield return WaitReal(0.3f);
            Assert.AreEqual(0, Session.St.round, "관문 1 전 Lever 거부");
            yield return AsAt(2, "junction"); Session.Send("Judge", 1); yield return WaitReal(0.3f);
            Assert.IsFalse(Session.St.gate2Done, "관문 1 전 Judge 거부");
            Assert.AreEqual(penalty0, Session.St.penalty, 0.001f, "관문 1 전 Judge 는 페널티도 없다");
            // 빈 입력 DigitConfirm 은 오답이 아니다(토스트만) / DigitClear
            yield return AsAt(0, "panel");
            Session.Send("DigitConfirm");
            yield return WaitReal(0.3f);
            Assert.AreEqual(0, Session.St.calcFails, "빈 입력 DigitConfirm 은 오답이 아니다");
            Assert.AreEqual(penalty0, Session.St.penalty, 0.001f, "빈 입력 DigitConfirm 은 페널티가 없다");
            Assert.IsFalse(Session.St.gate1Done, "빈 입력으로 문이 열리면 안 됨");
            Session.Send("Digit", 4);
            yield return Until(() => Session.St.calcInput == "4", 2f, "Digit 4 입력");
            Session.Send("DigitClear");
            yield return Until(() => Session.St.calcInput == "", 2f, "DigitClear 로 지우기");

            // 관측자: 경고등 색 신고 → 통역에게(정상 경로)
            yield return AsAt(3, "lamp");
            Session.Send("Color", lamp);
            yield return Until(() => Session.St.colorReport == lamp, 3f, $"관측자 색 신고({Stage1Logic.ColorName(lamp)})");
            // 수신 로그 가시성(QA test-T7): 색 이름은 통역·보낸 사람만, 다른 사람에게는 "[색 신고]"
            yield return Until(() => LastLogged(e => e.type == "Color" && e.slot == 3 && e.a == lamp) != null, 3f, "색 신고가 수신 로그에 남아야 함");
            var colorE = LastLogged(e => e.type == "Color" && e.slot == 3 && e.a == lamp);
            StringAssert.Contains(Comms.ColorName(lamp), Comms.LogText(colorE, 1), "통역 로그에는 색 이름");
            StringAssert.Contains(Comms.ColorName(lamp), Comms.LogText(colorE, 3), "관측자 자신의 로그에는 색 이름");
            StringAssert.Contains("[색 신고]", Comms.LogText(colorE, 0), "조작자 로그에는 [색 신고]");
            Assert.IsFalse(Comms.LogText(colorE, 0).Contains(Comms.ColorName(lamp)), "조작자에게 색 이름 누설 금지");

            // 조작자: 연산 결과의 홀짝을 채팅으로(통역만 읽는다)
            int parity = Stage1Logic.Parity(Session.St.calcResult);
            int chatsBefore = Session.State.stats.chats;
            string parityText = parity == 0 ? "결과 짝수야" : "결과 홀수야";
            yield return AsAt(0, "panel");
            Session.Send("Chat", 0, 0, parityText);
            yield return Until(() => Session.State.stats.chats > chatsBefore, 3f, "조작자 홀짝 채팅");
            // 수신 로그 가시성(QA test-T7): 통역·자신은 원문, 관측자는 ??? (외국어)
            yield return Until(() => LastLogged(e => e.type == "Chat" && e.slot == 0 && e.s == parityText) != null, 3f, "홀짝 채팅이 수신 로그에 남아야 함");
            var chatE = LastLogged(e => e.type == "Chat" && e.slot == 0 && e.s == parityText);
            Assert.AreEqual(parityText, Comms.LogText(chatE, 1), "통역 로그에는 원문");
            Assert.AreEqual(parityText, Comms.LogText(chatE, 0), "조작자 자신의 로그에는 원문");
            StringAssert.Contains("(외국어)", Comms.LogText(chatE, 3), "관측자 로그에는 외국어");
            Assert.IsFalse(Comms.LogText(chatE, 3).Contains("짝수") || Comms.LogText(chatE, 3).Contains("홀수"), "관측자에게 홀짝 누설 금지");

            // 통역: 매뉴얼 표(홀짝 × 색)로 정답 숫자를 찾아 숫자 카드 전송
            int answer = Stage1Logic.Table1[parity][lamp];
            Assert.AreEqual(Session.St.calcAnswer, answer, "표로 계산한 정답이 서버 정답과 같아야 함");
            yield return AsAt(1, "poster");
            Session.Send("Card", answer);
            yield return Until(() => Session.St.numberCard == answer, 3f, $"통역 숫자 카드({answer})");

            // 조작자: 오답 1회 → 페널티 + 실패 횟수 증가, 문은 그대로
            yield return AsAt(0, "panel");
            int wrong = (answer + 1) % 10;
            float penaltyBefore = Session.St.penalty;
            int failsBefore = Session.St.calcFails;
            Session.Send("Digit", wrong);
            yield return Until(() => Session.St.calcInput == wrong.ToString(), 3f, $"오답 입력({wrong})");
            Session.Send("DigitConfirm");
            yield return Until(() => Session.St.calcFails == failsBefore + 1 && Session.St.penalty > penaltyBefore, 4f, "오답 → 페널티 증가");
            Assert.IsFalse(Session.St.gate1Done, "오답으로는 문이 열리면 안 됨");
            Assert.AreEqual(wrong, Session.St.lastWrongDigit, "오답 입력값이 lastWrongDigit 에 남는다(B2 — 통역 벽보 팝업 status)");
            Assert.AreEqual(Stage1Logic.Gate1Penalty, Session.St.penalty - penaltyBefore, 0.01f, "관문 1 오답 페널티는 −10초(v2 D3)");
            Assert.AreEqual(10f, Stage1Logic.Gate1Penalty, 0.001f, "관문 1 오답 페널티 상수는 10초");

            // 키패드 3초 잠금(D3): 잠금 중 Digit 은 거부된다
            Assert.IsTrue(Stage1Logic.KeypadLocked(Session.St, Session.Clock), "오답 직후 키패드가 잠겨야 함");
            Session.Send("Digit", answer);
            yield return WaitReal(0.4f);
            Assert.AreEqual("", Session.St.calcInput, "키패드 잠금 중에는 Digit 이 거부되어야 함");
            yield return Until(() => !Stage1Logic.KeypadLocked(Session.St, Session.Clock), 5f, "키패드 잠금 해제(3초)");

            // 정답 — 제어반 팝업을 연 채로(D6c 검증)
            Assert.IsNotNull(StageScreen.Instance, "StageScreen 없음");
            StageScreen.Instance.OpenPopup("panel");
            yield return Until(() => StageScreen.Instance.IsPopupOpen && StageScreen.Instance.CurrentPopup == "panel", 3f, "제어반 팝업 열기");
            Session.Send("Digit", answer);
            yield return Until(() => Session.St.calcInput == answer.ToString(), 3f, $"정답 입력({answer})");
            Session.Send("DigitConfirm");
            yield return Until(() => Session.St.gate1Done && Session.St.step == 1, 4f, "관문 1 해제 → 이동 구간");
            Assert.AreEqual(-1, Session.St.lastWrongDigit, "정답이면 lastWrongDigit 은 −1 로 돌아간다(B2)");
            Assert.AreEqual(-1, Session.St.colorReport, "관문 1 해제 뒤 경고등 신고는 지워져야 함(관문 2 색상등에 쓸 수 없다)");
            Assert.AreEqual(-1, Session.St.lastPingRoom, "관문 1 해제 시 핑 초기화(Stage1Logic.DigitConfirm) — 이동 구간에서 미니맵 핀·안내문이 '내비 안내 대기'로 돌아가야 함");
            Assert.AreEqual(1, Session.St.checkpoint, "관문 1 통과 → 체크포인트 1");

            // 팝업은 즉시 닫히지 않고 "완료 ✓" 상태로 3초 유지된 뒤 스스로 닫힌다
            yield return Until(() => StageScreen.Instance != null && StageScreen.Instance.IsPopupOpen && StageScreen.Instance.IsPopupDone, 2f, "제어반 팝업 완료 ✓ 상태");
            yield return Shot(shot, "24_popup_panel_done_op");
            yield return WaitReal(1.2f);
            Assert.IsTrue(StageScreen.Instance != null && StageScreen.Instance.IsPopupOpen && StageScreen.Instance.IsPopupDone, "완료 ✓ 팝업은 3초 동안 열려 있어야 함");
            yield return Until(() => StageScreen.Instance == null || !StageScreen.Instance.IsPopupOpen, Popups.DoneCloseDelay + 2f, "완료 ✓ 팝업 자동 닫힘(3초)");

            // 단계(QA test-T5): 관문 1 뒤에는 숫자 카드와 경고등(lamp) 색 신고가 거부된다 — 이제 방향 카드·색상등(colorlamp) 차례
            float colorClockBefore = Session.St.colorReportClock;
            yield return AsAt(1, "poster");
            Session.Send("Card", (answer + 1) % 10);
            yield return AsAt(3, "lamp");
            Session.Send("Color", (lamp + 1) % 4);
            yield return WaitReal(0.3f);
            Assert.AreEqual(answer, Session.St.numberCard, "관문 1 뒤 숫자 카드 거부(값 유지)");
            Assert.AreEqual(colorClockBefore, Session.St.colorReportClock, 0.001f, "관문 1 뒤 경고등(lamp) 색 신고 거부");
            Assert.AreEqual(-1, Session.St.colorReport, "관문 1 뒤 경고등(lamp) 색 신고는 기록되지 않는다");
        }

        // ------------------------------------------------------------------ 관문 2

        /// <summary>
        /// 관문 2(방향 자물쇠 4라운드). wrongFirst 면 첫 패스의 첫 라운드를 일부러 틀린 방향으로 당긴 뒤
        /// "안전" 판정 → 페널티 + 라운드 리셋을 확인하고, 다시 4라운드를 정답으로 채워 gate2Done 까지 간다.
        /// shot 이 있으면 마지막 패스 2라운드 뒤에 관문 2 팝업 4장(30~33)을 찍는다.
        /// </summary>
        public static IEnumerator Gate2Manual(bool wrongFirst, Func<string, IEnumerator> shot = null)
        {
            Assert.IsTrue(Session.St.gate1Done, "관문 2는 관문 1 뒤에");

            // 이동 구간: 내비가 침수 현황판을 열고 모자이크방(방 3)으로 핑
            yield return AsAt(2, "board");
            Session.Send("OpenBoard");
            yield return Until(() => Session.St.boardOpened, 3f, "침수 현황판 열기");
            yield return PingAndVerify(3, "내비 핑(모자이크방)");

            if (wrongFirst)
            {
                yield return Gate2Rounds(0, null);
                yield return Until(() => Session.St.round == 4, 3f, "4라운드 완료(틀린 것 포함)");
                Assert.AreNotEqual(Session.St.ladderSafe, Stage1Logic.PathEnd(Session.St), "틀린 레버가 있으면 경로는 안전 단자로 가면 안 됨");

                // 내비가 그래도 "안전"이라고 판정 → 페널티 + 처음부터
                float penaltyBefore = Session.St.penalty;
                int attemptsBefore = Session.St.gate2Attempts;
                yield return AsAt(2, "junction");
                Session.Send("Judge", 1);
                yield return Until(() => Session.St.penalty > penaltyBefore && Session.St.round == 0 && Session.St.gate2Attempts == attemptsBefore + 1, 4f, "안전 오판 → 페널티 + 라운드 리셋");
                Assert.IsFalse(Session.St.gate2Done, "오판으로 셔터가 열리면 안 됨");
                Assert.AreEqual(Stage1Logic.Gate2Penalty, Session.St.penalty - penaltyBefore, 0.01f, "'안전' 오판 페널티는 −15초(v2 D3)");
                Assert.AreEqual(15f, Stage1Logic.Gate2Penalty, 0.001f, "관문 2 오판 페널티 상수는 15초");
                foreach (int d in Session.St.chosenDir) Assert.AreEqual(-1, d, "리셋 뒤 선택 방향은 비어야 함");

                // ---- 내비의 '위험 — 처음부터'(Judge 0, QA test-T4): 감점 0(v2 D3), gate2Attempts++, 새 시드 재생성, RoundReset 1회, round==0 이면 무시
                yield return Gate2Rounds(-1, null, stopAfter: 1);
                Assert.AreEqual(1, Session.St.round, "위험 재시도 전 1라운드만 진행");
                int attBefore = Session.St.gate2Attempts;
                float penBefore = Session.St.penalty;
                int startBefore = Session.St.ladderStart, safeBefore = Session.St.ladderSafe;
                var digitsBefore = (int[])Session.St.brailleDigit.Clone();
                var colorsBefore = (int[])Session.St.lampColor2.Clone();
                var nextBefore = (int[])Session.St.ladderNext.Clone();
                int resets = 0, judge0 = 0;
                int signalsBefore = Session.State.stats.signals;
                var navSignals = new System.Collections.Generic.List<string>();   // D-A5: 서버가 대신 내는 위험 → 아니오 몸짓 카드
                Action<GameEvent> rec = e =>
                {
                    if (e.type == "RoundReset") resets++;
                    if (e.type == "Judge" && e.a == 0) judge0++;
                    if (e.type == "Signal" && e.slot == 2) navSignals.Add(e.s);
                };
                Session.EventReceived += rec;
                try
                {
                    yield return AsAt(2, "junction");
                    Session.Send("Judge", 0);
                    yield return Until(() => Session.St.gate2Attempts == attBefore + 1 && Session.St.round == 0, 4f, "위험 판정 → 감점 없는 라운드 리셋");
                    Assert.AreEqual(penBefore, Session.St.penalty, 0.01f, "'위험' 재시도는 감점 0(v2 D3)");
                    Assert.IsFalse(Session.St.gate2Done, "위험 판정으로 셔터가 열리면 안 됨");
                    Assert.AreEqual(1, resets, "RoundReset 이벤트 1회");
                    Assert.AreEqual(1, judge0, "Judge(a=0) 이벤트 1회");
                    CollectionAssert.AreEqual(new[] { "DANGER", "NO" }, navSignals, "사람 내비의 '위험 — 처음부터'도 팀에게 위험 → 아니오 몸짓 카드로 보인다(서버 발행, D-A5)");
                    Assert.AreEqual(signalsBefore + 2, Session.State.stats.signals, "서버가 낸 카드 2장도 통계 signals 에 센다");
                    Assert.AreEqual(-1, Session.St.dirCard, "리셋 뒤 방향 카드는 비어야 함");
                    Assert.AreEqual(-1, Session.St.colorReport, "리셋 뒤 색 신고는 비어야 함");
                    foreach (int d in Session.St.chosenDir) Assert.AreEqual(-1, d, "위험 리셋 뒤 선택 방향은 비어야 함");
                    Assert.IsTrue(Stage1Logic.ValidateLadder(Session.St), "위험 리셋 뒤 다시 생성된 사다리도 규칙을 만족해야 함");
                    bool samePuzzle = startBefore == Session.St.ladderStart && safeBefore == Session.St.ladderSafe
                        && System.Linq.Enumerable.SequenceEqual(digitsBefore, Session.St.brailleDigit)
                        && System.Linq.Enumerable.SequenceEqual(colorsBefore, Session.St.lampColor2)
                        && System.Linq.Enumerable.SequenceEqual(nextBefore, Session.St.ladderNext);
                    Assert.IsFalse(samePuzzle, "위험 리셋은 새 시드로 퍼즐을 다시 만든다(같은 퍼즐 반복 = 정보 누설)");

                    // round==0 에서 '위험'은 "아직 시작 전" 토스트만 — 상태·이벤트 변화 없음
                    Session.Send("Judge", 0);
                    yield return WaitReal(0.3f);
                    Assert.AreEqual(attBefore + 1, Session.St.gate2Attempts, "round==0 에서 Judge 0 은 무시");
                    Assert.AreEqual(1, resets, "round==0 에서는 RoundReset 이 나오면 안 됨");
                    Assert.AreEqual(2, navSignals.Count, "round==0 에서 Judge 0 은 몸짓 카드도 내지 않는다");
                }
                finally { Session.EventReceived -= rec; }
            }

            yield return Gate2Rounds(-1, shot);
            yield return Until(() => Session.St.round == 4, 3f, "4라운드 완료(전부 정답)");
            Assert.AreEqual(Session.St.ladderSafe, Stage1Logic.PathEnd(Session.St), "전부 정답이면 경로는 안전 단자(아트리움 통로)");

            if (shot == null)
            {
                // 부정 검증 3(QA test-T3): 4라운드가 끝난 뒤에는 방향 카드·레버 모두 거부된다(판독 대기)
                yield return AsAt(1, "mural");
                Session.Send("DirCard", 0);
                yield return WaitReal(0.3f);
                Assert.AreEqual(-1, Session.St.dirCard, "4라운드 뒤 방향 카드는 거부되어야 함");
                yield return AsAt(0, "lever");
                Session.Send("Lever", 0);
                yield return WaitReal(0.3f);
                Assert.AreEqual(4, Session.St.round, "4라운드 뒤 레버는 거부되어야 함(round 유지)");
                Assert.AreEqual(Session.St.ladderSafe, Stage1Logic.PathEnd(Session.St), "4라운드 뒤 레버는 경로를 바꾸지 않는다");
            }

            yield return AsAt(2, "junction");
            Session.Send("Judge", 1);
            yield return Until(() => Session.St.gate2Done && Session.St.step == 2, 4f, "안전 판정 → 최종 집결");
        }

        /// <summary>현재 round 부터 stopAfter 라운드까지 진행(기본 4). wrongRound 번째 라운드는 정답과 다른 방향으로(음수면 전부 정답).</summary>
        private static IEnumerator Gate2Rounds(int wrongRound, Func<string, IEnumerator> shot, int stopAfter = 4)
        {
            while (Session.St.round < stopAfter)
            {
                int r = Session.St.round;
                var st = Session.St;
                int digit = st.brailleDigit[r];
                int color = st.lampColor2[r];
                int answer = Stage1Logic.Table2[Stage1Logic.Row(digit)][color];
                Assert.AreEqual(st.answerDir[r], answer, $"라운드 {r + 1}: 표로 계산한 방향이 서버 정답과 같아야 함");
                int dir = r == wrongRound ? (answer + 1) % 4 : answer;

                if (shot == null && r == 0)   // 부정 검증(QA test-T3)은 FullFlow 에서만(투어 스크린샷에 거부 토스트가 남지 않게)
                {
                    // 부정 검증 1: 방향 카드 없이 당긴 레버는 거부된다(명세 5-6 "dirCard 가 도착했을 때만 허용")
                    Assert.AreEqual(-1, Session.St.dirCard, "라운드 1 시작 시 방향 카드는 비어 있어야 함(부정 검증 전제)");
                    yield return AsAt(0, "lever");
                    Session.Send("Lever", answer);
                    yield return WaitReal(0.4f);
                    Assert.AreEqual(0, Session.St.round, "방향 카드 없이는 레버가 거부되어야 함(round 유지)");
                    Assert.AreEqual(-1, Session.St.chosenDir[0], "방향 카드 없이는 chosenDir 이 기록되면 안 됨");
                }
                if (shot == null && r == 1)
                {
                    // 부정 검증 2: 라운드 도중 "안전" 판정은 무시된다(페널티·리셋 없음, 이미 당긴 레버 유지)
                    float pen = Session.St.penalty;
                    int att = Session.St.gate2Attempts;
                    int kept = Session.St.chosenDir[0];
                    yield return AsAt(2, "junction");
                    Session.Send("Judge", 1);
                    yield return WaitReal(0.4f);
                    Assert.IsFalse(Session.St.gate2Done, "4라운드 전 판정으로 셔터가 열리면 안 됨");
                    Assert.AreEqual(pen, Session.St.penalty, 0.01f, "4라운드 전 판정은 페널티 없이 무시");
                    Assert.AreEqual(att, Session.St.gate2Attempts, "4라운드 전 판정은 라운드를 리셋하지 않음");
                    Assert.AreEqual(1, Session.St.round, "4라운드 전 판정 뒤에도 round 유지");
                    Assert.AreEqual(kept, Session.St.chosenDir[0], "4라운드 전 판정 뒤에도 이미 당긴 레버 유지");
                }

                // 관측자: 이번 라운드 색상등 신고(같은 색이 이어질 수 있어 신고 시각으로 확인)
                float reportBefore = st.colorReportClock;
                yield return AsAt(3, "colorlamp");
                Session.Send("Color", color);
                yield return Until(() => Session.St.colorReport == color && Session.St.colorReportClock > reportBefore, 3f, $"라운드 {r + 1} 색 신고({Stage1Logic.ColorName(color)})");

                // 조작자: 점자 점 번호를 채팅으로 ("점자 1,2,4")
                int chatsBefore = Session.State.stats.chats;
                yield return AsAt(0, "lever");
                Session.Send("Chat", 0, 0, "점자 " + string.Join(",", Stage1Logic.BrailleDots(digit)));
                yield return Until(() => Session.State.stats.chats > chatsBefore, 3f, $"라운드 {r + 1} 점자 채팅");

                // 통역: 방향 카드
                yield return AsAt(1, "mural");
                Session.Send("DirCard", dir);
                yield return Until(() => Session.St.dirCard == dir, 3f, $"라운드 {r + 1} 방향 카드({Stage1Logic.DirGlyph(dir)})");

                // 조작자: 레버. 다음 라운드가 있으면 관측자에게만 "라운드 n 시작 — 색이 같아도 다시 신고!"(B1)
                bool roundToast = false;
                Action<GameEvent> roundRec = e => { if (e.type == "Toast" && e.target == 3 && e.s == Stage1Logic.RoundStartText(r + 2)) roundToast = true; };
                Session.EventReceived += roundRec;
                try
                {
                    yield return AsAt(0, "lever");
                    Session.Send("Lever", dir);
                    yield return Until(() => Session.St.round == r + 1 && Session.St.chosenDir[r] == dir && Session.St.dirCard == -1, 3f, $"라운드 {r + 1} 레버({Stage1Logic.DirGlyph(dir)})");
                    if (r + 1 < Stage1Logic.Rounds) yield return Until(() => roundToast, 2f, $"라운드 {r + 2} 시작 관측자 토스트(B1)");
                    else { yield return WaitReal(0.3f); Assert.IsFalse(roundToast, "4라운드 뒤에는 라운드 시작 토스트가 없다"); }
                }
                finally { Session.EventReceived -= roundRec; }
                Assert.AreEqual(-1, Session.St.colorReport, $"라운드 {r + 1} 레버 뒤 이전 라운드 색 신고는 지워져야 함");

                if (shot != null && r == 1) yield return Gate2Popups(shot);
            }
        }

        /// <summary>관문 2 팝업 4장(배선이 두 줄 그려진 시점).</summary>
        private static IEnumerator Gate2Popups(Func<string, IEnumerator> shot)
        {
            yield return PopupShot(shot, 0, "lever", "30_gate2_lever_op");
            yield return PopupShot(shot, 1, "mural", "31_gate2_mural_in");
            yield return PopupShot(shot, 3, "colorlamp", "32_gate2_colorlamp_ob");
            yield return PopupShot(shot, 2, "junction", "33_gate2_junction_na");
        }

        /// <summary>담당 슬롯 시점에서 오브젝트 앞으로 가 팝업을 열고 캡처한 뒤 닫는다.</summary>
        public static IEnumerator PopupShot(Func<string, IEnumerator> shot, int slot, string propId, string name)
        {
            yield return AsAt(slot, propId);
            yield return null;
            Assert.IsNotNull(StageScreen.Instance, "StageScreen 없음");
            StageScreen.Instance.OpenPopup(propId);
            yield return Until(() => StageScreen.Instance != null && StageScreen.Instance.IsPopupOpen, 3f, $"팝업 열기({propId})");
            yield return Shot(shot, name);
            AssertRolePanelFits(name);
            if (StageScreen.Instance != null) StageScreen.Instance.ClosePopup();
            yield return Until(() => StageScreen.Instance == null || !StageScreen.Instance.IsPopupOpen, 3f, $"팝업 닫기({propId})");
        }

        /// <summary>떠 있는 팝업/로그를 닫는다(투어에서 다음 장면으로 넘어가기 전).</summary>
        public static void CloseOverlays()
        {
            var ss = StageScreen.Instance;
            if (ss == null) return;
            ss.ClosePopup();
            ss.CloseDonut();
            ss.ShowLog(false);
        }

        // ------------------------------------------------------------------ 최종 집결

        /// <summary>네 명의 자기 원 중심(움직인다). 호스트 클록 기준.</summary>
        public static Vector2 Gather(int slot) => Stage1Logic.GatherCenter(slot, Session.St, Session.Clock);

        /// <summary>네 명 모두 자기 원 위로 텔레포트(한 번).</summary>
        public static void TeleportAllToGather()
        {
            for (int i = 0; i < 4; i++) Teleport(i, Gather(i));
        }

        /// <summary>
        /// 최종 집결 부정 검증(QA test-T13): 3명만 원 안이면 클리어되지 않고 allInSince 도 시작되지 않으며,
        /// 4명이 모여 allInSince 가 시작된 뒤 한 명이 나가면 유지 시간이 리셋된다(0.6초 "동시" 조건).
        /// allInSince 시작 판정은 호스트 권한 상태(같은 프로세스)로 읽어 브로드캐스트 지연 동안 클리어되는 일이 없게 한다.
        /// </summary>
        public static IEnumerator GatherNegative()
        {
            Assert.AreEqual(2, Session.St.step, "최종 집결 단계여야 함");
            var logic = SessionNet.Instance != null ? SessionNet.Instance.Logic : null;
            Assert.IsNotNull(logic, "호스트 Logic(테스트는 호스트와 같은 프로세스)");
            Assert.Greater(Stage1Logic.AllInHold, 0f, "동시 유지 시간은 0 보다 커야 한다");

            // 3명만 원 안(관측자는 스폰 자리): 1.2초 동안 클리어·allInSince 시작 모두 없어야 한다
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.2f)
            {
                for (int i = 0; i < 3; i++) Teleport(i, Gather(i));
                Teleport(3, Session.Map.spawns[3]);
                Assert.IsFalse(Session.St.cleared, "3명으로는 클리어되면 안 됨");
                yield return null;
            }
            Assert.IsFalse(Session.St.cleared, "3명으로는 클리어되면 안 됨");
            Assert.Less(Session.St.allInSince, 0f, "3명으로는 동시 유지 시간이 시작되면 안 됨");
            Assert.AreEqual(3, Session.GatherCount, "원 안 인원 표시 = 3");

            // 4명 모두 → allInSince 시작(호스트 권한 상태) → 한 명 이탈 → 리셋
            start = Time.realtimeSinceStartup;
            while (logic.S.st.allInSince < 0f)
            {
                TeleportAllToGather();
                Assert.Less(Time.realtimeSinceStartup - start, 3f, "4명이 모이면 allInSince 가 시작되어야 함");
                yield return null;
            }
            Teleport(3, Session.Map.spawns[3]);
            yield return Until(() => Session.St.allInSince < 0f && !Session.St.cleared, 2f, "이탈 시 유지 시간 리셋");
            Assert.IsFalse(logic.S.st.cleared, "이탈했으면 클리어되지 않는다");
            Assert.AreEqual(3, Session.GatherCount, "이탈 뒤 원 안 인원 표시 = 3");
        }

        /// <summary>cleared 가 될 때까지 매 프레임 4명을 자기 원 중심으로 텔레포트한다(원이 움직이므로 계속 따라간다). 최대 maxSeconds.</summary>
        public static IEnumerator GatherAll(float maxSeconds = 4f)
        {
            Assert.AreEqual(2, Session.St.step, "최종 집결 단계여야 함");
            float start = Time.realtimeSinceStartup;
            int frames = 0;
            while (!Session.St.cleared)
            {
                TeleportAllToGather();
                if (Time.realtimeSinceStartup - start > maxSeconds)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Session.PosOf(i);
                        bool inside = p != null && Stage1Logic.InGather(i, p.Value, Session.St, Session.Clock);
                        sb.Append($" [{i}] in={inside} center=({Gather(i).x:0.0},{Gather(i).y:0.0})");
                    }
                    Assert.Fail($"시간 초과: 최종 집결 클리어 (allInSince={Session.St.allInSince:0.0}, {Where()}, {Positions()},{sb}, {frames}프레임)");
                }
                frames++;
                yield return null;
            }
            Debug.Log($"[Flow] 완료: 최종 집결 클리어 ({frames}프레임/{Time.realtimeSinceStartup - start:0.0}초)");
        }
    }
}
