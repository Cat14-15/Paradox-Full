# 통합 요청 — TESTS 영역 (Assets/Tests/PlayMode/*)

테스트가 명세(`STRANGER_Stage1_구현명세.md`) 이름으로 호출하는 것 외에, 명세에 없거나 애매해서 통합 단계에서 확인·처리가 필요한 항목이다.
(필수 = 없으면 컴파일/테스트 실패, 권장 = 없으면 일부 캡처가 어긋남)

| # | 구분 | 대상 영역 | 필요한 API / 동작 | 왜 | 제안 시그니처 |
| --- | --- | --- | --- | --- | --- |
| 1 | 필수 | CORE (`Stage1Logic`) | `ValidateLadder(StageState st) : bool` — 사다리 규칙(정답 4개 → `ladderSafe`, 하나라도 틀리면 다른 단자, `terminalZone[ladderSafe]==0`) 검사 | 지시문 11절 요구. `FullFlowTests` 가 스테이지 시작 직후와 리셋 뒤에 단언한다 | `public static bool ValidateLadder(StageState st)` |
| 2 | 필수 | CORE (`Stage1Logic`) | `Table1`, `Table2` 는 **jagged 배열**(`int[][]`)로 `Table1[parity][color]` 인덱싱이 되어야 함 | 명세 5-4/9절이 `Table1[p][c]` 표기라 그대로 썼다 (`Flow.Gate1Manual`, `Gate2Rounds`) | `public static readonly int[][] Table1 = { new[]{5,0,9,1}, new[]{3,7,2,0} };` |
| 3 | 필수 | CORE (`MapData`) | `RoomAt(Vector2) : int` 와 기존 `Walkable(Vector2, float)` 유지 | `Flow.Approach` 가 배전함·현황판처럼 방 아래 벽에 붙은 오브젝트는 `pos+(0,-0.9)` 가 방 밖이라 위/옆 자리를 고른다 | (명세 4절 그대로) |
| 4 | 필수 | SCREENS (`ScreenManager`/`ScreenBase`) | 화면 루트 GameObject 이름 = 화면 클래스 이름 유지(`LogoScreen`, `TitleScreen`, `PrologueScreen`, `StageSelectScreen`, `MatchScreen`) — 기존 `ScreenBase.Build` 의 `UI.Stretch(parent, GetType().Name)` 관례 | `Flow.ToLocal` 이 `GameObject.Find(이름)` 으로 로컬 화면 키가 ScreenManager 에 반영됐는지 확인한다(`ScreenManager` 가 현재 키를 노출하지 않음) | 또는 `public string CurrentKey { get; }` 를 `ScreenManager` 에 추가하면 테스트를 그쪽으로 바꿀 수 있음 |
| 5 | 필수 | SCREENS (`NetHub.Shutdown` 경로) | 호스트가 직접 `NetHub.Shutdown()` 해도 `LocalNav.Go(StageSelect)` 가 일어나야 함(끊김뿐 아니라 자발 종료도) | `FullFlowTests` 마지막에 `LocalNav.Screen == StageSelect` 를 단언(명세 4절 "접속 종료(NetHub.Shutdown/끊김) 시") | — |
| 6 | 필수 | HUD (`StageScreen`) | `OpenPopup(propId)` 훅은 담당 슬롯 시점 + 오브젝트 앞(방 안, 거리 ≤ Reach)에서 호출되므로 그 조건이면 반드시 열려야 하고, `IsPopupOpen` 이 다음 프레임에 true | `Flow.PopupShot` 이 `Until(IsPopupOpen)` 로 기다린다 | (명세 8-2 그대로) |
| 7 | 권장 | HUD (`StageScreen`) | `ClosePopup()` 이 도넛도 함께 닫거나, `CloseDonut()`/`OpenDonut()` 토글 제공 | 투어가 `11_donut` 뒤에 `Flow.CloseOverlays()`(=ClosePopup + ShowLog(false)) 만 부른다. 도넛이 남으면 이후 캡처를 가린다 | `public void CloseDonut()` 또는 `ClosePopup()` 안에서 도넛 닫기 |
| 8 | 권장 | HUD (`TutorialOverlay`) | (a) `AutoSkip` 이 true 로 바뀌면 떠 있는 오버레이도 즉시 닫힘 (b) 코치마크 n단계로 바로 가는 훅 | `ScreenshotTour.CaptureCoachmark` 가 `AutoSkip=false` 로 스테이지에 들어가 첫 오버레이를 `09_coachmark_1` 로 찍는다. 역할 팝업이 먼저 뜨는 구조면 그 장은 역할 팝업이 된다 | `public static TutorialOverlay Instance; public void ShowCoachmark(int step)` |
| 9 | 권장 | SCREENS (`PrologueScreen`) | 대사 줄 index 로 점프하는 훅 | `02_prologue_scene2 / scene3` 캡처용. 지금은 첫 장면(`02_prologue_1`)만 찍는다 | `public static PrologueScreen Instance; public void JumpTo(int lineIndex)` |
| 10 | 권장 | WORLD (`BotBrain`) | `botsAuto == false` 일 때 봇 아바타는 제자리(리더 추종·오브젝트 이동 안 함) | `FullFlowTests`/투어는 명령 직전에 텔레포트하지만, 봇이 매 프레임 걸어가면 `40_gather` 같은 캡처가 어긋나고 근접 판정이 아슬아슬해진다 | `if (!Session.State.botsAuto) return Vector2.zero;` |
| 11 | 확인 | CORE (`BotDriver.OnChat`) | 같은 문장이 여러 번 와도 문제없이 해석(마지막 값 유지) | `BotFlowTests` 는 봇 통역이 못 읽었을 때를 대비해 10초마다 같은 채팅("결과 짝수야", "점자 1,2,4")을 다시 보낸다 | — |
| 12 | 확인 | CORE (`GameLogic`) | `Ready` 단계 → 1.5초 뒤 자동 `StageIntro`(cutsceneId `s1_intro`), `StageClear` 에서 호스트 `Skip` → `Result`, `Result` 에서 `Again` → `Lobby`(ready 해제) | `Flow.LobbyToStage`, `FullFlowTests` 가 그대로 단언 | (명세 1-2 그대로) |
| 13 | 확인 | CORE (`Stats`) | `stats.chats`, `stats.signals` 가 int 이고 Stage 1 에서 `Chat`/`Signal` 마다 증가 | 채팅 도착 확인을 `stats.chats` 증가로 한다(통역 외에는 원문을 못 읽으므로 상태로 확인) | — |

## 테스트가 쓰는 오브젝트 id / 명령 (명세 이름 그대로)
- 오브젝트: `panel poster lamp board lever mural colorlamp junction`
- 명령: `StartGame Name Ready Skip Next Again BotsAuto Control Ping OpenBoard Color Chat Card Digit DigitConfirm DirCard Lever Judge Signal`
- 상태: `StageState.{step, gate1Done, gate2Done, round, penalty, cleared, calcResult, calcAnswer, calcInput, calcFails, lampColor, colorReport, colorReportClock, numberCard, dirCard, lastPingRoom, boardOpened, brailleDigit[], lampColor2[], answerDir[], chosenDir[], ladderSafe, gate2Attempts, allInSince, timed, timeLimit}`, `SessionState.{phase, stage, cutsceneId, cutsceneIndex, botsAuto, players[].name/ready/isBot, stats.chats/signals}`

## 실행
`powershell -NoProfile -ExecutionPolicy Bypass -File "<scratch>\run_tests.ps1" -Filter FullFlowTests -Tag full` / `-Filter BotFlowTests -Tag bot` / `-Filter ScreenshotTour -Tag tour -Shots`
