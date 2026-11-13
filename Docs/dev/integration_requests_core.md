# CORE 영역 통합 메모 / 요청 (Game/*, Client/Session.cs)

명세(`STRANGER_Stage1_구현명세.md`) 7절 이름은 그대로 구현했다. 아래는 명세에 없거나 명세와 조금 다른 점, 다른 영역이 알아야 할 것, 다른 영역에 요청하는 것이다.

## 1. 명세에 없는 추가 API (자유롭게 써도 된다)

| 위치 | 멤버 | 용도 |
| --- | --- | --- |
| `Roles` | `DefaultNames[]`, `DefaultName(slot)`, `Countries[]`, `Country(slot)`, `SlotOfDefaultName(name)` | 이름 설정 카드 기본값 / 국기색 라벨 "한국·필리핀·…" |
| `StageDef` | `blurb`, `locked` / `StageDefs.IsLocked(stage)` | 스테이지 선택 카드 문구·TOP SECRET |
| `StageDefs.FogRadius(stage, slot)` | 항상 0 (인자 3개짜리 옛 시그니처는 삭제) | |
| `MapProp.room` | 프롭이 놓인 방(-1 = 복도) — 발광 규칙 `prop.slot == LocalSlot && RoomAt(myPos) == prop.room` | |
| `StageMaps` | `RoomCount=6`, `RoomAtrium/RoomDesk/RoomOffice/RoomMosaic/RoomAquarium/RoomGarden`, `RoomRect(room)`, `DoorRect(room)`, `RoomDoor(room)`, `RoomName(room)`, `LockedDoor3`, `FountainSolid` | 바닥 색·문 표시·미니맵 윤곽 |
| `MapData` | `rooms[6]`, `RoomAt(pos)`, `RoomName(room)`(static), `RoomDoor(room)` | |
| `Stage1Logic` | `ProgressGlyphs(st)`("■□□"), `RolePropId(slot, st)`, `RoleMissionDone(slot, propId, st)`, `PropName(id)`, `CalcExpr(st)`("13 × 7 − 4"), `DotsChat(digit)`("점자 1,2,4"), `DigitOfDots(set)`, `FloodLevel(pct)`(0 초록/1 노랑/2 빨강), `RowDigits[][]`, `BrailleDigits[]`, `DirName(d)`, `PathAt(st, r)`(부분 배선용), `AnswerPathAt`, `ValidateLadder(st)`, `GenerateGate2`, `ResetRounds`, 상수 `NearReach/Gate1Penalty/Gate2Penalty/GatherOmega/GatherR/GatherWobble/DangerFlood/Rounds/Terminals/Zones` | HUD·팝업·봇·테스트 |
| `GameLogic` | `SignalIds[]`, `IsSignalId(id)`, `ReadyDelay`, `ClearDelay`, `RoomOf(slot)`, `AnyInRoom(room)`, `AllInGather()`, `InGatherCount()` | |
| `Session` | `RoomOf(slot)`, `GatherCount`(표시용 n/4), `LoggedTypes[]` | HUD |
| `StoryLine` | `bg`(배경 키: classroom/school/camp/museum_dark/museum_relief, ""=이전 줄 유지), `group`(네 사람 초상 나란히 — s1_clear 첫 줄) | VnView |
| `Cutscene` | `BackgroundAt(index)` — 빈 bg를 앞쪽 줄에서 채워서 돌려준다 | VnView |
| `StoryData.PrologueId` | `"prologue"` | PrologueScreen |
| `BotDriver.ParseDots(text)` | 채팅 → 점자 숫자(정적, 테스트 가능) | |

## 2. 명세와 다르게/보충해서 정한 것

- `SessionState.readyClock` 추가(준비 완료 → 1.5초 자동 전환 기준 시각). `Ready 0`은 NameSetup에서 "취소".
- 로비에서 봇의 이름은 "봇"이 아니라 그 슬롯의 기본 이름(도윤/마리아/린/아이샤)이다. 봇 표시는 `PlayerInfo.isBot`으로 구분해 달라(LobbyScreen/NameSetupScreen).
- 자리 이동(`Slot`) 시 사람이 기본 이름을 쓰고 있었다면 새 역할의 기본 이름으로 바뀐다. `PlayerInfo.character`는 항상 슬롯 번호.
- 게임 중(Ready/StageIntro/StagePlay/StageClear) 이탈은 봇 전환, Lobby/NameSetup/Result 이탈은 자리 비움.
- 관문 1 클리어 직후 전원 토스트 "이렇게 서로 다른 정보를 모아야 퍼즐이 풀려요. 이제부터는 스스로!"는 **서버가 보낸다**(`GateOpen a=1` 직후 Toast 이벤트). HUD(TutorialOverlay/StageGuide)는 같은 문구를 중복으로 띄우지 말 것.
- `Ping` 이벤트에 `s = 방 이름`도 실어 보낸다(로그 표기용). `Color` 이벤트 `s = 색 이름`, `NumberCard` `s = 숫자`, `DirCard` `s = 방향 기호`.
- `Judge` 이벤트: `a=1 s="safe"`(성공) / `a=1 s="wrong"`(오판, 이어서 `Alarm s="gate2"`, `RoundReset`) / `a=0 s="reset"`. `Alarm`은 `s="gate1"|"gate2"`, `a=1|2`.
- `Wire` 이벤트 `s="from>to"`의 from/to는 **단자 번호(0~7)** 이다. 화면에는 +1 해서 1~8로 보여 달라(PDF s30).
- 침수 구역 이름(`Stage1Logic.ZoneName`)은 짧은 이름("아트리움 통로", "안내데스크", "관리실", "모자이크방", "수족관방", "연못정원방", "지하 창고", "옥상 계단"). 방 이름(`StageMaps.RoomName`)은 긴 이름("중앙 분수 아트리움", "관리실·보안 데스크", "나침반 모자이크방" …).
- `Stats`는 `playSeconds, wrongInputs, cards, chats, signals, retries`(옛 `stageSeconds[]/retries[]/letters/losses` 삭제). `playSeconds`는 실패·클리어 시점마다 그 시도의 경과 시간을 누적한다.
- `status` 문구(서버): step 0 "관문 1 — 계산 자물쇠 · 안내데스크", step 1 "이동 구간 — 침수 현황판" / (모자이크방에 누가 들어오면) "관문 2 — 방향 자물쇠 · 라운드 r/4" / (4라운드 끝) "배전함 판독 대기 — 내비게이터", step 2 "최종 집결 — 중앙 분수 아트리움".
- `s1_intro` 마지막 줄은 `system=true` "조작을 시작합니다 …" 한 줄(유저플로우 "조작을 시작합니다" 안내). 스태프 줄은 index 4(0부터), `barrierMask = 조작자 외 전원`. 정전 줄(index 7, 8)은 `dark=true`.
- 프롤로그 sceneTag: "교실 점심시간"(index 0) / "조별 과제"(index 6) / "며칠 후 · 하굣길"(index 10). bg: classroom, classroom, school.
- `Retry`: 새 `StageState`를 만들되 `checkpoint`(관문 1 통과 시 1)를 이어받는다. checkpoint 1이면 `gate1Done=true, step=1`로 시작하고 관문 2 퍼즐은 새 시드(`seed+17`). 스폰은 항상 아트리움 4자리.
- `RebuildMapIfNeeded` 키는 long(`stage, seed, retries, gate1Done, step`). 서버 `RebuildMap`은 Dirty마다 돌지만 맵 생성은 가벼움(Rect 12개 + 프롭 13개).

## 3. 삭제된 것(참조하면 컴파일 오류)

- `Phase.CharacterSelect / Prologue / Ending` → `NameSetup / Ready` 로 교체. 프롤로그는 로컬(LocalNav).
- `StageState`의 옛 필드 전부(rail, word, picked, inflight, received, fork…, consoleMode…, gas…, device, fires, codeDone…). `LetterMsg` 삭제.
- `MapData.zones / zoneRadius / InZone / sectors / SectorAt / railNodes / hazards / flooded / exitRoom` 삭제 → 집결 원은 `Stage1Logic.GatherCenter/InGather`, 방은 `rooms/RoomAt`.
- `StageMaps.Stage1NodeX / Stage1ObstacleNode / Stage1LobbyNode / Stage1ForkOfNode / Stage3* / Stage4* / SectorZones / SectorName / IsBlocked` 삭제. `SlotOffsets`, `AvatarRadius`는 유지.
- `StageDefs.Syllables / ForkPairs / ConsoleModes / CodeWords / IsLetterSender / HasDirectionPad / HasGoggles` 삭제. `Get / IsLocked / LocalSlot / CanRead / ChatAllowed(항상 true) / HasMinimap(내비) / FogRadius(stage, slot)=0` 유지.
- `GameLogic.ResetLetters / PickToTray / SendTray / DeliverLetters / ValidSyllable / TrayMax / AllInZones / InZoneCount` 삭제. `MeterA/MeterB`는 SessionNet 호환용으로 남김(항상 0).
- `Stage1Logic`의 옛 멤버(GuardGlow, RelayReady, CanPullLever, LeverLabel, Phase01, InGuardWindow, LettersInTransit, Cycle, WordLength …) 삭제.
- 명령 `Char / Pick / SendLetters / Relay / ClearLetters / Mash / Console / Hold / Pull / Mark / Confirm / Operate / Key / KeyClear` 삭제. `Signal`의 `b(target)`는 무시된다(전원 대상).
- `Signal` id는 `YES NO WHERE GO YOU DANGER` 6종만 서버가 받는다(그 외는 무시). 옛 `GO STOP OK ASK WARN L R U D`는 `GO`만 살아남음.
- `StoryData`의 `s2_intro / s2_clear / s3_* / s4_intro / ending` 삭제. `Npc`는 그대로(Jiho, Friend1, Friend2, Teacher, Staff).
- `Stage2Logic.cs / Stage3Logic.cs / Stage4Logic.cs`는 `Prototype_Backup/ParadoxV1/Game/`으로 옮기고 삭제(.meta 포함). 옛 Game 파일 전부도 같은 폴더에 백업해 두었다.

## 4. 다른 영역에 요청

### WORLD (`Net/AvatarNet.cs`의 BotBrain, `Client/StageWorld.cs`, `StageInteractions.cs`)
- **BotBrain 목표**(명세 9절, BotDriver가 전제로 한다): 목표 = `Session.Map.Prop(Stage1Logic.RolePropId(slot, st)).pos + (0, −0.9)`, 정지 거리 0.25 이내면 `GameLogic.Near(…, 2.2)`가 만족된다.
  - 내비 봇: step 0 → `board`(관리실) → `st.boardOpened`가 되면 아트리움(스폰 자리)에서 대기. step 1 → `boardOpened`가 아니면 `board`, 아니면 `junction`. step 2 → `GatherCenter(slot)`.
  - 조작자·통역·관측자 봇: **`st.lastPingRoom == 목표 방` 또는 내비의 GO 신호 이후, 또는 단계 시작 25초 후**에만 담당 오브젝트로 출발(그 전엔 제자리). 단계 = `st.step`(+ `st.retries`).
  - 최종 집결: 0.5초마다 `Stage1Logic.GatherCenter(slot, st, Session.Clock)` 재계산, stop 0.25.
  - 리더 추종·18유닛 순간이동은 이 맵(25×18)에서는 사실상 필요 없다 — 유지해도 무방.
- `AvatarNet.HandleKeys`의 SPACE→`Mash` 하드코딩 삭제, `st.moveMode == 2`(레일) 분기 삭제(`rail` 필드 없음). 위험 구역 속도 ×0.5는 `Stage1Logic.IsDanger(st, Session.Map.RoomAt(pos), Session.Clock)`.
- `StageWorld`: 프롭 kind는 `panel poster lamp board lever mural colorlamp junction fountain door3 sign`. `door3`는 `!st.gate1Done`일 때 잠금 표시. `sign` 문구는 `Session.CanReadSigns`(통역)만 원문.
- `StageInteractions.Get`: 팝업 열기용 프롬프트만(명령은 팝업 안 버튼이 보낸다). 근접 판정은 `Reach = 1.6`(클라) / 서버는 2.2.

### HUD (`StageScreen`, `Popups`, `StagePanels`, `LogPanel`, `Comms`)
- 팝업 명령 이름: `Digit(a) DigitClear DigitConfirm Color(a) Card(a) DirCard(a) Lever(a) Judge(a) Ping(a) OpenBoard`. `OpenBoard`는 내비가 현황판 팝업을 **열 때** 한 번 보낸다.
- 배전함 판독 문구는 `Stage1Logic.PathEnd(st)`(−1 = 미완)와 `st.terminalZone[t]` → `Stage1Logic.ZoneName(...)`으로 클라이언트가 계산. 안전 여부는 말하지 않는다. `boardOpened`면 `Stage1Logic.Flood(st, zone, Session.Clock)`로 % 병기.
- 부분 배선(라운드 r까지)은 `Stage1Logic.PathAt(st, r)`로 그릴 수 있다. 라운드 색 = `st.lampColor2[r]`.
- 관문 1 클리어 토스트는 서버가 보내므로 HUD는 중복 금지(위 2절).
- 로그 표기 규칙(6-3절)은 `Comms`에서: `Color` 이벤트는 통역(`target==1`)에게만 색 이름, 나머지 "[색 신고]"; `NumberCard/DirCard`는 조작자에게만 값.

### SCREENS (`VnView`, `NameSetupScreen`, `LobbyScreen`, `ResultScreen`)
- `VnView`: `StoryLine.bg`(빈 값이면 `Cutscene.BackgroundAt(index)`), `StoryLine.group`(네 초상 나란히), `system` 줄은 "안내" 스타일.
- `NameSetupScreen`: `Ready 1` = CONFIRM, `Ready 0` = 취소, 호스트 `Back`. 기본 이름은 `Roles.DefaultName(slot)`(서버가 이미 채워 둔다).
- `ResultScreen`: `Stats.playSeconds/wrongInputs/cards/chats/signals/retries`.

### TESTS
- `Stage1Logic.ValidateLadder(Session.St)`는 `StagePlay` 진입 직후와 `RoundReset` 뒤에 호출해 true를 단언할 수 있다.
- 관문 2 "틀린 레버 1개 포함 → 안전 판정 → 페널티·리셋" 시나리오: `Judge 1` 뒤 `st.gate2Attempts`가 1 증가하고 `st.round == 0`, `st.penalty`가 20 증가한다.
- 명령의 근접 조건(2.2) 때문에 테스트는 담당자를 프롭 위치 + (0, −0.9)로 텔레포트한 뒤 명령을 보내야 한다. 관문 2 오브젝트는 `gate1Done` 전에는 잠긴 문 때문에 걸어서 못 들어가지만 텔레포트는 된다(서버는 문을 검사하지 않는다).
- `Ready` 화면은 1.5초 뒤 자동 전환이므로 `Until(phase == StageIntro, 5s)`로 기다린다.
