# 통합 요청 — WORLD 영역 (StageWorld / StageInteractions / AvatarView / AvatarNet+BotBrain / SpriteLib / CameraFollow / FeedbackFX / YDepthSort)

WORLD 영역이 명세 이름 그대로 호출하는 다른 영역 API와, 명세에 없어서 정한 것·확인이 필요한 것을 적는다.

## 1. 명세 이름으로 호출 중인 것(존재만 확인하면 됨)

| 영역 | 호출 | 쓰는 곳 |
| --- | --- | --- |
| CORE `Stage1Logic` | `IsDanger(StageState, int room, float clock)` | AvatarNet 속도 절반, StageWorld 비네트/침수 색 |
| CORE `Stage1Logic` | `Flood(StageState, int zone, float clock)` | StageWorld 침수 오버레이 알파 |
| CORE `Stage1Logic` | `GatherCenter(int slot, StageState, float clock)`, `InGather(int slot, Vector2, StageState, float clock)`, `const GatherRadius` | StageWorld 집결 원, BotBrain 집결 목표, StageInteractions 안내 |
| CORE `Stage1Logic` | `RoomDoor(int room)` | StageWorld 핑 비콘(이벤트 pos가 0이면) |
| CORE `Stage1Logic` | `ColorName(int)`, `DirGlyph(int)` | AvatarNet 색 신고·카드 말풍선 |
| CORE `MapData` | `rooms : Rect[6]`, `RoomAt(Vector2)`(인스턴스) | StageWorld 바닥/테두리/침수, StageInteractions 발광 판정, AvatarNet 위험 방 |
| CORE `MapData` | **`RoomName(int)` 를 정적(static) 메서드로 가정**해 `MapData.RoomName(room)` 으로 호출 | `StageWorld.RoomLabel(int)` 한 곳에서만 호출(방 이름 라벨·핑 말풍선). 인스턴스 메서드라면 이 한 줄만 고치면 된다 |
| CORE `StageState` | `step, gate1Done, gate2Done, cleared, failed, startClock, retries, moveMode, lastPingRoom, boardOpened` | StageInteractions.MissionDone, BotBrain 게이팅 |
| CORE `MapProp` | `id`(= kind), `pos`, `slot`, `text` — 방 번호는 `map.RoomAt(prop.pos)`로 계산하므로 `room` 필드는 필요 없음 | StageWorld, StageInteractions |
| HUD `Comms` | `CanUnderstand(int viewer, int sender)` | AvatarNet 채팅 말풍선 |
| HUD `StageScreen` | `static Instance`, `bool IsPopupOpen`, `OpenPopup(string propId)` | AvatarNet.HandleKeys(E / 좌클릭) |
| CORE 이벤트 | `Ping(slot=2, a=room, pos=문 위치)`, `Signal(slot, s)`, `Chat`, `Color(slot,a)`, `NumberCard(slot,a)`, `DirCard(slot,a)`, `GateOpen(a)`, `Alarm`, `Clear`, `Teleport` | AvatarNet.OnEvent, StageWorld.OnEvent |

## 2. WORLD가 제공하는 API(다른 영역이 쓸 수 있음)

- `StageInteractions.Reach = 1.6`, `ClickRadius = 0.9`, `Prompt { text, cmd, popupId }`, `Get(slot, pos)`, `IsGlowing(MapProp, slot, myPos)`, `MissionDone(st, id)`, `PropName(id)`, `PromptText(id)`, `ClickTarget(slot, myPos, worldPoint)`, `CanInteract(slot, pos)`(액션 열 "상호작용 E" 강조용).
- `AvatarView.ShowCard(id)`, `ShowBubble(text, color, duration, sub)`, 정적 `CardColor/CardSymbol/CardWord(id)` — 도넛(HUD)이 같은 기호·색을 쓰면 통일된다. you 카드 기호는 폰트 제한 때문에 `★`(☝ 대신).
- `AvatarNet.SignalText(id)`(로그용 "기호 단어"), `AvatarNet.InDanger`(내가 위험 방 안인가), `BotBrain.MissionProp(slot, step)`, `BotBrain.Instructed(st, room, clock)`.
- `FeedbackFX.Vignette(bool)`; `CameraFollow.SetFixed(pos, size)` / `ClearFixed()` / `fixedMode`; `SpriteLib.DashedRing()`, `SpriteLib.Vignette()`, `SpriteLib.Icon(panel|poster|lamp|board|leverpad|mural|colorlamp|junction|fountain|shutterdoor|beacon|sign)`; `YDepthSort.OrderFor(y, scale)`.
- `StageWorld.RoomLabel(int room)`.

## 3. 결정 사항(명세에 없어서 정함)

1. 오브젝트 id `lever`의 아이콘은 `SpriteLib.Icon("leverpad")`로 그린다(옛 "lever" 아이콘과 구분).
2. 발광 "미션 완료" 판정: panel/poster/lamp = `gate1Done`, lever/mural/colorlamp/junction = `gate2Done`, board = `step >= 2`(최종 집결부터는 현황판 불필요; 그 전에는 내비가 몇 번이고 다시 볼 수 있음).
3. 핑 말풍선 "→ ⚑"는 폰트 미지원 기호라 `"→ {방 이름}"` + 작은 글씨 "여기로!"로 대체.
4. 색 신고 말풍선: 통역·보낸 사람에게는 "■ 노랑", 나머지에게는 "[색 신고]". 카드 말풍선: 조작자·보낸 사람에게는 "숫자 카드 7"/"방향 카드 ▶", 나머지에게는 "[카드]".
5. 봇(BotBrain)은 `Session.State.botsAuto == false`면 움직이지 않는다(테스트가 직접 텔레포트·조작하는 모드).
6. 내비 봇: 관문 1에서는 현황판 → (`boardOpened && lastPingRoom == 1`이면) 아트리움 (−2.2, 0.9)에서 대기 → 이동 구간에서는 `lastPingRoom == 3`(또는 25초 경과) 뒤 배전함 앞. 다른 봇의 지시 게이팅은 `lastPingRoom == 목표 방 || 내비 GO 신호 이후 || 단계 시작 25초 후`(단계 시작 = 클라이언트가 step 변화를 처음 본 서버 시각, 새 스테이지/재시도는 `startClock`).
7. 클리어 셔터는 아트리움 북쪽 벽 x 1.7~4.0(연못정원 문 오른쪽)에 두고 `cleared`/`Clear` 이벤트에서 1.6초 동안 올라간다(장식, 통행 영향 없음).
8. 정전 분위기: 카메라 배경 #06080D + 월드 가장자리 어두운 비네트(안개 없음). 스테이지를 떠나면 원래 배경색/카메라 크기로 복구.
9. 이동은 `st.moveMode != 0 && !cleared && !failed`일 때만(클리어·실패 뒤에는 멈춤).

## 4. 확인 부탁

- HUD `StageScreen`이 `_world.Build()/Refresh()/Tick()/OnEvent(e)/Dispose()`를 그대로 호출하는지(공개 API는 옛 것과 같음). `StageScreen.Dispose`에서 카메라 offset/useBounds를 만지는 옛 코드는 있어도 무해하다(fixedMode가 우선).
- 서버 `Ping` 이벤트의 `pos`에 `RoomDoor(room)`을 채워 주면 클라 계산 없이 그대로 쓴다(비어 있으면 `Stage1Logic.RoomDoor(e.a)`로 대체).
- 최종 집결 상단 게이지 "n/4 도착"은 HUD가 `Stage1Logic.InGather`로 계산(월드는 원만 그린다).
