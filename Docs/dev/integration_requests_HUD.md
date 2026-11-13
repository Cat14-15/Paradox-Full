# 통합 요청 — HUD 영역 (StageScreen · StagePanels · Popups · Donut · LogPanel · TutorialOverlay · StageGuide · MinimapView · Comms)

HUD는 명세의 이름만 호출한다. 아래는 (1) 명세에 있지만 정확한 형태를 가정한 것, (2) 명세 밖에서 필요한 것, (3) 다른 영역이 쓰면 좋은 HUD 쪽 API.

## 1. 가정한 시그니처(명세 7-3·4절) — CORE가 이 형태로 제공해야 컴파일된다

| 호출 | 어디서 | 가정 |
| --- | --- | --- |
| `Session.Map.RoomAt(Vector2) : int` (인스턴스) | StageGuide, TopBar 진행 표시, MinimapView, Comms.IsGlowing | −1 = 복도/문 |
| `Session.Map.rooms : Rect[6]` | MinimapView | 방 0~5 순서 = 명세 4절 표 |
| `Session.Map.Prop(string id) : MapProp` (`pos` 필드) | MinimapView(내 담당 표시), StageScreen.TryInteract | 기존 API 유지 |
| `Stage1Logic.ZoneName(int zone) : string` | 전역(방/구역 이름) | 0~5 = 방 이름, 6 지하 창고, 7 옥상 계단 |
| `Stage1Logic.Flood(StageState, int zone, float clock) : float` | 역할 패널·현황판·배전함 | 0~100 |
| `Stage1Logic.IsDanger(StageState, int room, float clock) : bool` | MinimapView 물결 | |
| `Stage1Logic.PathEnd(StageState) : int` | 배전함 판독 | 미완이면 −1 |
| `Stage1Logic.GatherCenter(int slot, StageState, float clock) : Vector2` | 집결 게이지·미니맵 | |
| `Stage1Logic.InGather(int slot, Vector2 pos, StageState, float clock) : bool` | 집결 게이지·조작자 콘솔 3번째 줄 | |
| `Stage1Logic.RoomDoor(int room) : Vector2` | 핑 비콘(미니맵) | |
| `Stage1Logic.BrailleDots(int digit) : int[]` | 레버반 팝업 | 점 번호 1~6 |
| `Stage1Logic.GatherRadius` (`public const float`) | MinimapView 집결 원 크기 | 0.95 |
| `Roles.Code(slot)` 3글자(KOR/PHL/VNM/ZAF), `Roles.Job(slot)` 기획서 문구, `Roles.Nation(slot)` "필리핀인" 형태 | 배지·튜토리얼 | Comms.NationShort가 끝의 "인"을 뗀다 |
| `StageState` 필드: step, gate1Done, gate2Done, calcA/B/C, calcOp, calcResult, calcInput, calcFails, lampColor, colorReport, numberCard, dirCard, lastPingRoom, lastPingClock, boardOpened, round, brailleDigit[4], lampColor2[4], chosenDir[4], ladderStart, ladderNext[128], terminalZone[8], gate2Attempts, checkpoint, retries, seed, failed, cleared, timed | 전역 | 명세 7절 그대로 |
| 이벤트 타입: Signal(slot,s) · Chat(slot,s) · Color(slot,a) · NumberCard(slot,a) · DirCard(slot,a) · Ping(slot,a) · GateOpen(a) | LogPanel·Popups·StageScreen | 명세 7-2 그대로 |

`Stage1Logic.Table1/Table2`는 배열 모양(`int[,]` vs `int[][]`)이 명세에서 애매해 **HUD는 쓰지 않고** `Comms.Table1/Table2/Table2Digits/BrailleDots`에 표시용 사본을 둔다. 서버 표를 바꾸면 Comms도 같이 바꿔야 한다(통합 단계에서 한쪽을 참조하도록 합치면 좋다).

## 2. 명세 밖에서 필요한 것

1. **Session.Dispatch 로그 필터**(파일 소유자 미지정 — CORE 또는 SCREENS): 명세 7-2대로 `Signal, Chat, Color, NumberCard, DirCard, Ping, Toast`를 `Session.Log`에 넣어야 수신 로그(C)·역할 패널 "최근 신고"가 채워진다. 현재 코드는 Signal/Chat/Toast/LetterArrive만 넣는다.
2. **관문 1 클리어 토스트 중복 방지**(CORE): "이렇게 서로 다른 정보를 모아야 퍼즐이 풀려요. 이제부터는 스스로 해보세요!"는 HUD가 `GateOpen a=1` 이벤트를 받아 **클라이언트에서** 띄운다(`TutorialOverlay.AutoSkip`이면 생략). 서버는 같은 문장을 보내지 않아도 된다(보내면 두 번 뜬다).
3. **AvatarNet.CurrentPrompt**(WORLD): `StageInteractions.Prompt { text, cmd }` 구조를 유지해 달라. HUD가 하단 "[E] 제어반 조사" 문구를 `ControlledAvatar.CurrentPrompt.text`에서 읽고, 액션 열의 [상호작용 E] 버튼은 `cmd != null`이면 그 명령을 보내고 아니면 발광 중인 내 담당 오브젝트(Reach 이내)의 팝업을 연다.
4. **팝업 열기 경로**(WORLD): E/클릭 판정 후 `StageScreen.Instance?.OpenPopup(propId)`를 호출하면 된다(propId = panel/poster/lamp/board/lever/mural/colorlamp/junction). 발광 규칙 판정에 `Comms.IsGlowing(id, slot, pos, st)`를 써도 된다(HUD가 같은 규칙으로 미니맵을 그린다).
5. **위험 구역 비네트/토스트**(WORLD·CORE): HUD는 그리지 않는다(명세 5-5대로 WORLD의 FeedbackFX/서버 토스트).

## 3. 다른 영역이 쓰면 좋은 HUD 쪽 API (`Paradox.Client.Comms`, 전부 static)

- `Comms.CanUnderstand(viewer, sender)` / `Comms.Garble(text)` / `Comms.ChatText(viewer, sender, text)` — 말풍선(AvatarView)·컷씬이 같은 규칙을 쓰면 좋다.
- `Comms.SignalGlyph(id)`, `SignalWord(id)`, `SignalColor(id)`, `SignalLabel(id)` — 카드 말풍선(색 카드 + 기호 + 영문 단어). id = YES/NO/WHERE/GO/YOU/DANGER.
- `Comms.ColorName(a)`, `ColorValue(a)`, `DirGlyph(d)`, `NumberCardText(a)`, `DirCardText(d)`.
- `Comms.PropName/PropTitle/PropSlot/PropRoom/MissionDone/IsGlowing` — 오브젝트 표(명세 4절).
- 테스트 훅(명세 8-2): `StageScreen.Instance.OpenPopup(id)`, `ClosePopup()`, `OpenDonut()`, `ShowLog(bool)`, `IsPopupOpen`, 추가로 `CurrentPopup`, `IsDonutOpen`, `IsLogVisible`, `ShowHelp()`, `TryInteract()`, `TutorialOverlay.AutoSkip`.

## 4. 키 배치(참고)

Q 도넛 · C 로그 · TAB 미니맵 확대 · T 채팅 포커스 · Esc 팝업/도넛/로그 닫기(실패 중 호스트는 Retry) · 상단 ? 조작 안내. 팝업이 열려 있으면 숫자 키 0~9 / Enter / Backspace가 제어반 키패드로 동작한다(팝업이 InteractionState 잠금을 쥐고 있어 1~4 조종 전환과 겹치지 않는다).
