# 통합 요청 — SCREENS 영역 (접속 전 로컬 화면 · 로비/이름/준비/컷씬/결과 · 화면 라우팅)

SCREENS 코드가 **명세에 있는 이름으로 이미 호출하고 있는** 다른 영역의 API 목록이다. 통합 단계에서 실제 시그니처가 아래와 다르면 SCREENS 쪽을 맞춘다.

## 1. CORE 가 제공해야 하는 것 (없으면 컴파일 불가)

| 필요한 API | 왜 | 쓰는 곳 | 제안 시그니처 |
| --- | --- | --- | --- |
| `StoryLine.bg` (string, 기본 "") | 컷씬 줄마다 배경 키(classroom/school/camp/museum_dark/museum_relief). VnView 는 현재 줄부터 거슬러 올라가 처음 나오는 bg 를 쓰고, 하나도 없으면 호출자의 기본값(프롤로그=classroom, s1_intro=camp, s1_clear=museum_relief)을 쓴다 | `VnView.Show` | `public string bg = "";` — 프롤로그: 씬1·2 줄에 `classroom`, 씬3(하굣길) 첫 줄에 `school`; s1_intro: 씬1 `camp`, 정전 이후 줄에 `museum_dark`; s1_clear: `museum_relief` |
| `Roles.DefaultName(int slot)` | 기본 이름(도윤/마리아/린/아이샤). 이름 설정 placeholder, 접속 전 프롤로그 이름표, 준비/클리어 화면의 빈 이름 대체 | VnView, NameSetupScreen, ReadyScreen, SettingsScreen | `public static string DefaultName(int slot)` |
| `Roles.Code(slot)` 3글자 (KOR/PHL/VNM/ZAF) | 로비·결과 카드 표기 | LobbyScreen, ResultScreen | 기존 메서드, 값만 교체 |
| `Phase { Title, Lobby, NameSetup, Ready, StageIntro, StagePlay, StageClear, Result }` | 라우팅 키(Lobby/Name/Ready/Cut_<id>/Stage_…/Result) | `ScreenManager.ComputeKey`, `CutsceneScreen.DefaultBg` | 명세 1-2 그대로 |
| `Stats.playSeconds`(float), `Stats.wrongInputs`, `Stats.cards`, `Stats.chats`(int) | 결과 화면 기록 표 | `ResultScreen.Build` | 명세 7절. `retries` 는 Stats 대신 `StageState.retries`(int)를 읽으므로 Stats.retries 의 타입은 상관없다 |
| `StageState.retries`(int), `SessionState.gameStartClock` | 결과 화면 재시도 횟수 / playSeconds 가 0일 때 총 시간 대체 | ResultScreen | 기존 필드 유지 |
| 명령 `Name(s)`, `Ready(a)`, `Back`, `Next`, `Skip`, `Again`, `Slot(a)`, `FillBots`, `StartGame` | 각 화면 버튼 | NameSetup/Cutscene/Result/Lobby | 명세 7-1 |
| 서버: `Ready` 가 `character >= 0` 을 요구하지 않을 것 | 캐릭터 선택 화면이 없어졌고 `Char` 명령을 더 이상 보내지 않는다 | NameSetupScreen | `PlayerInfo.character` 는 서버가 슬롯 번호로 채운다 |
| 서버: `Name` 명령을 NameSetup 단계에서 ready 전이면 언제든 허용, 빈 문자열이면 `Roles.DefaultName(slot)` | 이름 설정 화면이 Build 시점과 편집 종료 시점에 Name 을 보낸다 | NameSetupScreen | 기존 규칙 유지 |
| `Npc.Name(int id)` (StoryData.cs) | NPC 실루엣 초상의 첫 글자 | `UIPortrait.Npc` | 기존 유지 |

## 2. Session.cs (소유자 미지정 — 통합 단계에서 처리)

- `Session.Reset()` 은 `LocalNav` 를 건드리지 않는다(명세 4절). 지금 코드도 건드리지 않으므로 그대로 두면 된다.
- `RebuildMapIfNeeded` 의 키에서 옛 필드(gatesOpen/obstacleCleared/codeDone/fires)를 지우고 `stage, seed, retries, gate1Done, step` 으로 바꿔야 한다(WORLD/CORE 와 합의). SCREENS 는 `Session.Map` 을 쓰지 않는다.
- `Session.Dispatch` 로그 필터에 `Color, NumberCard, DirCard, Ping` 추가(HUD 요청 사항이지만 같은 파일).

## 3. NET (NetHub) — 동작 확인 요청

- `NetHub.Disconnected` 가 **호스트가 직접 `Shutdown()` 했을 때도** 발행되는지 확인. SCREENS 는 안전하게 "나가기/스테이지 선택으로" 버튼에서 `LocalNav.Go(StageSelect)` 를 먼저 부른 뒤 `Shutdown()` 한다. Disconnected 가 오면 `ScreenManager.OnDisconnected` 가 다시 `LocalNav.Go(StageSelect)` + 이유 토스트를 띄운다(중복 호출은 무해).
- 참가 실패(잘못된 IP)도 Disconnected → 스테이지 선택으로 돌아가며 실패 이유가 토스트로 표시된다. 매칭 화면에 남기를 원하면 `ScreenManager.OnDisconnected` 한 줄만 바꾸면 된다.

## 4. HUD / WORLD / TESTS 에게 알리는 것 (SCREENS 가 제공)

- `Paradox.Client.Skin` (ScreenManager.cs 안): 공통 팔레트(`Bg, Panel, Border, Amber, Ink, Dim, DarkText, Red, Green`), `Bordered/BorderedCell`(사각 테두리 패널), `Accent`(앰버 버튼), `Outline`(윤곽 버튼), `Swatch`, `Vignette`, `Spaced`. 스테이지 HUD/팝업도 같은 톤을 쓰려면 이걸 쓰면 된다.
- `LocalNav.Go / Screen / SkipLogo / Changed`, `Progress.*`, `Backgrounds.Get/GetSprite/Draw/DrawCode`, `UIPortrait.Npc(parent, id, size)`, `UIPortrait.MakeForSlot` 유지, `VnView.PlayerName(slot)`(접속 전이면 저장 이름/기본 이름), `ResultScreen.FormatTime`, `ScreenManager.Instance.CurrentKey`(디버그).
- 화면 키: 접속 전 `Local_Logo / Local_Title / Local_Prologue / Local_StageSelect / Local_Match / Local_Settings`, 접속 후 `Lobby / Name / Ready / Cut_<cutsceneId> / Stage_<stage>_<seed>_<retries> / Result`.
- 테스트 `Boot()` 는 `LocalNav.Go(LocalScreen.Match)` 로 시작하면 바로 매칭 화면이 뜬다(로고 자동 3.5초 대기 없음). 로고를 거치는 테스트는 `LocalNav.SkipLogo = true` 또는 아무 키 입력.
- `Progress` 는 PlayerPrefs 라 테스트 간에 남는다. 화면 투어에서 "이어하기 비활성" 상태를 찍고 싶으면 `Progress.ResetAll()` 먼저.

## 5. 자산

- `Assets/Resources/Backgrounds/README.txt` 를 만들었다(배경 png 넣는 법). png 파일명: `classroom.png`, `school.png`, `camp.png`, (선택) `museum_dark.png`, `museum_relief.png`.
- `CharacterSelectScreen.cs` 는 `Prototype_Backup/ParadoxV1/Client/` 로 복사 후 삭제(.meta 포함).
