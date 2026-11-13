# STRANGER Stage 1 — 구현 명세 (계약 문서 v1)

이 문서는 `STRANGER_Stage1_기획.md`(사용자 기획서)와 `STRANGER_UserFlow_노트.md`(PDF 화면)를 현재 코드베이스(Paradox) 위에 구현하기 위한 **단일 계약**이다.
기획서에 없는 항목은 여기서 결정했고, 마지막 절 "결정 목록"에 근거를 적었다. 구현 담당(사람/에이전트)은 이 문서의 이름(필드·명령·이벤트·오브젝트 id·화면 키)을 그대로 쓴다.

---

## 0. 범위와 원칙

- 만드는 것: 로고 → 타이틀 → 프롤로그 → 스테이지 선택 → 매칭 → 이름 설정 → 준비 완료 → Stage 1 오프닝 → Stage 1(관문 1 → 이동 → 관문 2 → 최종 집결) → 클리어 컷씬 → 결과 → 스테이지 선택 복귀.
- Stage 2~4는 스테이지 선택에서 TOP SECRET 잠금만 표시. 옛 Paradox Stage 1~4 로직은 삭제(코드는 `Prototype_Backup/ParadoxV1/`로 옮겨 보관, 컴파일 대상 아님).
- 아키텍처 유지: 단일 씬, 코드 생성 UGUI, NGO 호스트 권한 `GameLogic`, 상태 JSON 브로드캐스트, 봇, PlayMode 테스트, 스크린샷 투어.
- 뷰: 탑다운 2D(아이소메트릭 아님), **한 층 전체가 항상 보이는 고정 카메라**. 픽셀 캐릭터 4종은 `Assets/Art/Characters/*.png`(이미 있음).
- 언어: 모든 UI/문구 한국어. 코드 주석 한국어.

---

## 1. 플로우 ↔ 상태

### 1-1. 로컬 단계(접속 전) — `Paradox.Client.LocalNav`
```csharp
public enum LocalScreen { Logo, Title, Prologue, StageSelect, Match, Settings }
public static class LocalNav {
    public static LocalScreen Screen;                 // 기본 Logo
    public static event System.Action Changed;
    public static void Go(LocalScreen s);            // Screen 바꾸고 Changed 발행
    public static bool SkipLogo;                      // 테스트/이어하기용
}
```
ScreenManager.ComputeKey: 접속 전이면 `"Local_" + LocalNav.Screen`, 접속 후는 서버 phase.
접속이 끊기면(NetHub.Disconnected) → `LocalNav.Go(StageSelect)` (Stage 1을 클리어했으면 카드에 CLEAR 표시).

### 1-2. 서버 단계 — `Paradox.Game.Phase` (교체)
```csharp
public enum Phase { Title = 0, Lobby, NameSetup, Ready, StageIntro, StagePlay, StageClear, Result }
```
| Phase | 화면 키 | 진입 | 이탈 |
| --- | --- | --- | --- |
| Lobby | Lobby | 접속 | 호스트 `StartGame`(4명) → NameSetup |
| NameSetup | Name | | 전원 `Ready 1` → Ready. 호스트 `Back` → Lobby |
| Ready | Ready | | 서버가 1.5초 후 자동 → StageIntro(cutsceneId `s1_intro`) |
| StageIntro | Cut_s1_intro | | 호스트 `Next` 끝/`Skip` → StartStage(1) |
| StagePlay | Stage_1_{seed}_{retries} | | cleared 3.2초 후 → StageClear(`s1_clear`) |
| StageClear | Cut_s1_clear | | 끝 → Result |
| Result | Result | | 호스트 `Again` → Lobby(준비 해제). "나가기"는 클라이언트가 Shutdown → 로컬 StageSelect |

프롤로그는 **로컬**(접속 전, 각자 재생)이다. 오프닝 스토리(`s1_intro`)는 역할별 ??? 줄이 있어 **네트워크 컷씬**으로 호스트가 진행한다.

### 1-3. 저장(PlayerPrefs) — `Paradox.Client.Progress`
`stranger.prologueSeen`(0/1), `stranger.stage1Cleared`(0/1), `stranger.bestSeconds`(float), `stranger.myName`(string), `stranger.volume`(0~1), `stranger.fullscreen`(0/1).
- 타이틀 "이어하기"는 `prologueSeen || stage1Cleared`일 때만 활성 → StageSelect로 바로.
- "시작하기"는 항상 프롤로그부터(프롤로그 SKIP 가능).

---

## 2. 역할 상수 (`GameRole.cs`)

| 슬롯 | 역할 | 국적 | 코드 | 기본 이름 | 색(HTML) |
| --- | --- | --- | --- | --- | --- |
| 0 | 조작자 | 한국인 | KOR | 도윤 | F2C572 |
| 1 | 통역 | 필리핀인 | PHL | 마리아 | 5FD3C4 |
| 2 | 내비게이터 | 베트남인 | VNM | 린 | E98A8A |
| 3 | 관측자 | 남아공인 | ZAF | 아이샤 | 9F8FE0 |

`Roles.DefaultName(slot)`, `Roles.Code`는 3글자(KOR/PHL/VNM/ZAF)로 바꾼다. `Roles.Jobs`(역할 설명)은 기획서 문구로 교체:
- 조작자: "스위치와 레버를 조작할 수 있는 유일한 사람. 자유롭게 채팅하지만, 이 나라의 글자는 읽지 못한다."
- 통역: "이 스테이지의 표지판·벽보를 읽을 수 있는 유일한 사람. 모두의 채팅을 읽고 쓸 수 있고, 숫자·방향 카드를 보낸다."
- 내비게이터: "맵과 침수 상황을 볼 수 있다. 팀이 어디로 가야 하는지는 너만 안다."
- 관측자: "램프와 패널의 색을 구분할 수 있다. 색은 너만 볼 수 있다."

캐릭터 선택은 없다. `PlayerInfo.character`는 항상 슬롯 번호로 두고 UI는 `UIPortrait.MakeForSlot`(픽셀 시트)만 쓴다. Jovial 종이인형 코드는 남겨도 되지만 화면에서 쓰지 않는다.

---

## 3. 화면 명세 (PDF 화면 번호 참조)

공통 톤: 배경 #06080D, 패널 #0D1420(알파 0.96), 테두리 #2A3A55, 강조 앰버 #F2C572, 본문 #F0F2F5, 흐린 글자 #8A96A8. 버튼은 사각(라운드 없음), 강조 버튼은 앰버 배경 + 어두운 글자.

| 키 | 화면 | 요소 | 전이 |
| --- | --- | --- | --- |
| Local_Logo (s01) | 로고 인트로 | 앰버 비네트, 가운데 로고(텍스트 "STRANGER" + 코드로 그린 랜턴 빛·발자국), "PARALLAX · 2026 유네스코학교 교육의 미래 프로젝트", "아무 키나 누르면 넘어갑니다" | 3.5초 자동 또는 아무 키/클릭 → Title |
| Local_Title (s02) | 타이틀 | 좌측 로고, 버튼 ■시작하기(앰버)/이어하기/설정, 우하단 "v0.1 · 4인 협동 · PC" | 시작하기 → Prologue, 이어하기 → StageSelect, 설정 → Settings |
| Local_Prologue (s03) | 프롤로그(로컬 VN) | `VnView`(아래) 로 `StoryData.Get("prologue")` 재생. 배경: 씬1·2 = 교실, 씬3 = 학교 정문. 우상단 SKIP ▶▶ | 끝/SKIP → `Progress.prologueSeen=1` → StageSelect |
| Local_StageSelect (s04) | 스테이지 선택 | "국제 청소년 교류 캠프 / 스테이지 선택", 카드 4장(Stage 1 앰버 테두리 + 설명 + PLAY, 2~4 TOP SECRET 도장·자물쇠·"베트남/남아공/한국 · 미정", 클릭 시 흔들림), 하단 "Stage 1을 클리어하면 다음 봉투가 열린다", 좌하단 "타이틀로" | PLAY → Match |
| Local_Match | 매칭 | 제목 "팀 모으기 — Stage 1 · 침수된 박물관", 버튼 방 만들기(호스트)/IP 입력+참가하기/혼자 테스트(봇 3명), 상태 문구, 뒤로 | 접속 성공 → 서버 Lobby |
| Local_Settings | 설정 | 음량 슬라이더(값만 저장), 전체화면 토글, 내 기본 이름 입력, "프롤로그 다시 보기" 버튼, 뒤로 | |
| Lobby | 팀 모으기(기존 LobbyScreen 재스타일) | 역할 카드 4장(픽셀 초상, 이름/봇/빈 자리, 자리 이동 버튼), 내 IP, 봇 채우기, 게임 시작(호스트), 나가기 | StartGame → NameSetup |
| Name (s05) | 이름 설정 | 커튼 배경, "이름을 정해 주세요 / 국적과 역할은 캠프에서 이미 배정되었습니다", 카드 4장(국기색 라벨, 픽셀 초상, 이름 입력 — 내 카드만 편집 가능, 아래 역할명), 내 카드 앰버 테두리, [뒤로](호스트만) / [확인](=Ready). 확인 후 [준비 취소] 가능 (PDF 의 BACK/CONFIRM 을 아이 눈높이로 한국어) | 전원 Ready → Ready |
| Ready (s06) | 준비 완료 | "네 명 모두 준비됐습니다", 카드 4장 역할색 테두리 + 이름 + "✓ 완료", 하단 "4명 전원이 중앙 분수에 함께 모여야 클리어됩니다" | 서버 1.5초 후 자동 |
| Cut_s1_intro (s07/s08) | 오프닝 | `VnView` 네트워크 모드. 좌상단 "이름 · 국가 시점" 배지, SKIP(호스트), 다음(호스트, Enter). 스태프 줄은 조작자 외 ??? 지글 | 끝 → StagePlay |
| Stage_1_* (s12~s34) | 플레이 | 4절 HUD | |
| Cut_s1_clear | 클리어 컷씬 | 네 사람 안도(픽셀 초상 4개 나란히) + 내레이션 | 끝 → Result |
| Result | 결과 | "STAGE 1 CLEAR", 팀 초상 4, 기록(총 시간, 오답 횟수, 카드 수, 채팅 수, 재시도), 내레이션 한 줄, 버튼 "스테이지 선택으로"(모두: Shutdown) / "로비로 (한 번 더)"(호스트 `Again`) | |

### 3-1. `VnView` (공용 비주얼노벨 렌더러, `Assets/Scripts/Client/VnView.cs`)
- 입력: `Cutscene` + 현재 줄 index + 시점 슬롯(-1 = 로컬 프롤로그) + 진행 권한 여부.
- 구성: 배경 레이어(`Backgrounds.Get(key)` 텍스처가 있으면 Image, 없으면 코드 배경) → 어둠 레이어(dark) → 초상(좌/우) → 하단 대사창(좌상단 이름표 앰버 탭, 본문 타자 효과 45자/초, 우하단 ▼ 다음, 상단 sceneTag "[ 교실 점심시간 ]") → 우상단 SKIP ▶▶.
- 언어 장벽 줄(`barrierMask`에 내 슬롯 비트)은 이름표 "???" + 본문을 0.12초마다 새로 만드는 ??? 덩어리 + 아래 작은 글씨 "(알아들을 수 없는 말이다)".
- 초상: 슬롯 0~3은 픽셀 앞모습(크게), NPC(10+)는 `UIPortrait.Npc(id)`(코드로 그린 실루엣: 머리 원 + 어깨 + 이름 첫 글자, id별 색).
- 배경 키: `classroom`, `school`, `camp`, `museum_dark`(정전 로비, 코드 배경만), `museum_relief`.

### 3-2. `Backgrounds` (`Assets/Scripts/Client/Backgrounds.cs`)
`Resources.Load<Texture2D>("Backgrounds/" + key)` 를 캐시. 파일 위치: `Assets/Resources/Backgrounds/{classroom,school,camp}.png` (사용자가 이미지를 넣는 자리; README에 안내). 없으면 `null` → VnView가 코드 배경(그라데이션 + 단순 도형: 교실 = 책상 격자, 학교 = 도로·횡단보도, 캠프 = 초록 바탕·버스)을 그린다.

---

## 4. 박물관 맵 (`StageMaps.Stage1`, 월드 유닛)

MM(x0,y0,x1,y1). 통행 영역(open)끼리는 1유닛 이상 겹친다.

| 방(room) | 번호 | open rect | 문(open) | 비고 |
| --- | --- | --- | --- | --- |
| 중앙 분수 아트리움 | 0 | (−4.5,−3.5, 4.5,3.5) | — | 시작 + 최종 집결. 분수 solid (−1,−1,1,1), 원형으로 그림 |
| 안내데스크 | 1 | (−11,0.5, −5,6.5) | (−6,1.5, −3.5,3.5) | 관문 1 |
| 관리실·보안 데스크 | 2 | (−11,−6.5, −5,−0.5) | (−6,−3.5, −3.5,−1.5) | 침수 현황판 |
| 나침반 모자이크방 | 3 | (5,−3, 10.5,5) | (3.5,−1, 6,1) | 관문 2. `gate1Done`이 아니면 solid (4.7,−1.2,5.3,1.2) = 잠긴 문 |
| 수족관방 | 4 | (−3,−7, 3,−4.5) | (−1,−5.5, 1,−2.5) | 위험 구역(막다른 방) |
| 연못정원방 | 5 | (−3,4.5, 3,7) | (−1,2.5, 1,5.5) | 위험 구역(막다른 방) |

- bounds = (−12.5,−9, 12.5,9). spawns = (−2.1,−2.4), (−0.7,−2.4), (0.7,−2.4), (2.1,−2.4).
- `MapData.rooms : Rect[6]`, `MapData.RoomAt(Vector2) : int`(−1 = 복도/문), `MapData.RoomName(int)`(한국어) 추가.
- 카메라: 고정. `CameraFollow`에 `fixedMode` 추가 → position (0.5, −0.6, −10), orthographicSize 9.4. 스테이지 화면에서만 켠다. 안개(FogRadius)는 0(한 층 전체가 보인다) — 정전 분위기는 어두운 바닥 색과 비네트로만.
- 깊이 정렬: `YDepthSort`는 −y×scale을 ±900으로 클램프하므로 이 맵(y ±9)에서는 scale 100이 포화된다 → 아바타 `Configure(50f, -0.45f)`, `StageWorld`의 정적 프롭도 `YDepthSort.OrderFor(y, 50f)`(scale 인자 오버로드 추가)로 통일.
- 클라 맵 재생성 키(`Session.RebuildMapIfNeeded`): `stage, seed, retries, gate1Done, step`.
- 접속 종료(`NetHub.Shutdown`/끊김) 시 `LocalNav.Go(StageSelect)`. `Session.Reset()`은 LocalNav를 건드리지 않는다(테스트 `Boot`가 `LocalNav.Go(Match)`로 시작).

오브젝트(`MapProp`, kind = id):
| id | 이름 | 담당 slot | room | 위치 |
| --- | --- | --- | --- | --- |
| panel | 비상 제어반 | 0 | 1 | (−8.0, 5.8) |
| poster | 코팅된 벽보(계산 모듈 매뉴얼) | 1 | 1 | (−10.2, 5.8) |
| lamp | 경고등 패널 | 3 | 1 | (−5.9, 5.8) |
| board | 침수 현황판 | 2 | 2 | (−8.0, −5.8) |
| lever | 방향 레버반 | 0 | 3 | (7.8, 0.8) |
| mural | 나침반 벽화 매뉴얼 | 1 | 3 | (9.8, 4.2) |
| colorlamp | 색상 표시등 | 3 | 3 | (7.8, 4.3) |
| junction | 배전함 | 2 | 3 | (9.8, −2.2) |
| fountain | 분수(장식) | −1 | 0 | (0,0) |
| door3 | 모자이크방 문(잠금 표시) | −1 | — | (5.0, 0) |
| sign0~2 | 필리핀어 표지판(분위기용, 통역만 읽힘) | 1 | 0/1/3 | (−3.8,3.0) "TYPHOON: Manatiling kalmado", (−8,0.9) "Emergency exit → Atrium", (7.8,−2.6) "Power room — authorized only" |

상호작용 반경 `StageInteractions.Reach = 1.6`. 오브젝트 클릭(마우스, 반경 0.9 이내)도 같은 동작.

---

## 5. Stage 1 규칙 (`Stage1Logic.cs`, 서버) — 고정 상수는 `Stage1Logic`의 public const

### 5-1. 시간
- `TimeLimit = 720`(12분). 표시 "침수까지 mm:ss". 남은 시간 = timeLimit − (Clock − startClock) − penalty.
- 페널티: 관문 1 오답 −15초, 관문 2 "안전" 오판 −20초. 토스트 + 경보 플래시(팝업은 닫지 않음).
- 0초 → `failed=true`, 이벤트 `Fail`. 호스트 `Retry` → `StartStage(1, retry:true)`: `checkpoint`(0 처음/1 관문 2부터)를 유지하고 시간은 다시 720초. checkpoint 1이면 `gate1Done=true, step=1`로 시작하며 스폰은 아트리움.

### 5-2. 단계(`step`)
- 0 관문 1, 1 이동 구간 + 관문 2, 2 최종 집결. `status` 문구는 서버가 갱신.
- 상단 진행 표시: `■□□`(관문 1) → `■■□`(이동/관문 2; 모자이크방에 한 명이라도 들어가면 "관문 2 — 방향 자물쇠", 아니면 "이동 구간 — 침수 현황판") → `■■■`(최종 집결). 관문 2에는 서브 게이지 `라운드 r/4`.

### 5-3. 오브젝트 발광·팝업(클라이언트 규칙)
- 발광: `prop.slot == LocalSlot && RoomAt(myPos) == prop.room && !해당 미션 완료`. 비담당자에게는 평범한 소품(회색 아이콘, 라벨 없음).
- 팝업 열기: 발광 중 + 거리 ≤ Reach + (E 또는 클릭). 팝업이 열려 있는 동안 `InteractionState.PushLock()`(이동 불가). 닫기 ×/Esc.
- 팝업은 담당자 화면에만 존재(로컬). 서버에는 열림 상태를 두지 않되, 명령의 근접 조건은 서버가 `Near(slot, id, 2.2)`로 검사한다(봇도 같은 조건).

### 5-4. 관문 1 — 계산 자물쇠
- 생성(seed): `calcA∈[11,19]`, `calcB∈[3,9]`, `calcC∈[1,9]`, `calcOp∈{0:+,1:−}` → `calcResult = A×B ± C`. `lampColor∈0..3`(0 빨강 1 노랑 2 초록 3 파랑). `calcAnswer = Table1[parity][lampColor]`, Table1 = 짝수 {5,0,9,1}, 홀수 {3,7,2,0}.
- 조작자 팝업(panel): 연산 결과 큰 숫자 + 식, 입력칸(한 자리), 키패드 0~9, 지우기, 확인. 힌트 "램프 색과 매뉴얼은 내 담당이 아니다. 홀짝을 채팅으로 통역에게".
  - 명령 `Digit a(0~9)` → `calcInput = a.ToString()`; `DigitClear`; `DigitConfirm` → 정답이면 `gate1Done=true, step=1, colorReport=-1`(경고등 신고는 관문 2에 쓸 수 없다), 이벤트 `GateOpen a=1`, 토스트 "잠금 해제! 모자이크방 문이 열렸다"; 오답이면 `calcFails++`, Penalty(15), 이벤트 `Alarm`.
- 관측자 팝업(lamp): 큰 램프 원(색), "색을 읽고 신고한다", 신고 버튼 4개. 명령 `Color a` → `colorReport=a, colorReportClock=Clock`, 이벤트 `Color slot=3 a target=1`(통역 화면에만 도착, 로그에도).
- 통역 팝업(poster): 크림색 매뉴얼 카드(표 색×홀짝→숫자), 우측 "지금 받은 정보: 램프 ■색(관측자) / 홀짝: (조작자 채팅에서 읽는다)", 숫자 카드 0~9 + "숫자 카드로 알리기". 명령 `Card a` → `numberCard=a`, 이벤트 `NumberCard slot=1 a target=0`.
- 조작자는 채팅으로 "짝수/홀수"를 보낸다(자유 문장). 통역만 읽는다(6절).
- 도착한 카드는 조작자 팝업/패널에 "통역이 보낸 숫자: n"으로 표시(자동 입력 아님 — 조작자가 직접 누른다).

### 5-5. 이동 구간 — 침수 현황판 (내비게이터)
- 침수율은 clock 함수: `Flood(zone, clock) = clamp(floodBase[z] + floodRate[z] × (clock − startClock), 0, 100)`. 8개 구역 = 방 0~5 + 6 "지하 창고" + 7 "옥상 계단"(현황판 표시용).
  - base/rate(시드로 ±10%/±20% 흔듦): 0 아트리움 10/0.020, 1 안내데스크 15/0.020, 2 관리실 20/0.030, 3 모자이크방 25/0.030, 4 수족관방 45/0.110, 5 연못정원방 40/0.120, 6 지하 창고 60/0.080, 7 옥상 계단 30/0.050.
  - 아트리움은 항상 최저가 되도록 생성 후 보정(rate·base 모두 최소).
- 위험: 방 4·5의 침수율 ≥ 70이면 "위험 구역": 안에 있는 플레이어 이동 속도 ×0.5, 화면 가장자리 붉은 비네트, 5초마다 토스트 "물이 허리까지 찼다 — 빠져나가!". 시간 감점은 없다.
- 내비게이터 팝업(board): 구역별 막대(초록 <40, 노랑 <70, 빨강 ≥70) + "안전한 경로를 골라 휠로 팀을 이동시킨다", go/위험 버튼. 열면 명령 `OpenBoard` → `boardOpened=true`(배전함 판독에 % 표시를 허용).
- 내비게이터 지시: 6칸 휠 + 역할 패널의 "여기로!" 방 버튼 6개(명령 `Ping a=room`) → 이벤트 `Ping slot=2 a=room pos=문 위치`. 모든 화면에서 그 방 입구에 10초간 앰버 비콘(▼ 깜빡임)과 미니맵 표시, 내비 머리 위 말풍선 "→ ⚑". 이동 자체는 강제하지 않는다(정보 비대칭으로만 유도).

### 5-6. 관문 2 — 방향 자물쇠(4라운드)
- 생성(seed): 라운드 r=0..3마다 `brailleDigit[r] ∈ {1,2,3,4,5,6,7,9}`, `lampColor2[r] ∈ 0..3`, `answerDir[r] = Table2[Row(digit)][color]`. Table2 행 {1,4}=▲▼▲▼, {2,7}=◀▶◀▶, {5,3}=◀▲▶▲, {6,9}=▲▶◀▼ (열 = 빨강,노랑,초록,파랑; 방향 0▲ 1▼ 2◀ 3▶).
- 점자(표준 6점, 1·2·3 왼쪽 열 위→아래, 4·5·6 오른쪽 열): 1{1} 2{1,2} 3{1,4} 4{1,4,5} 5{1,5} 6{1,2,4} 7{1,2,4,5} 9{2,4}. `Stage1Logic.BrailleDots(int digit) : int[]`.
- 사다리(배전함): 단자 8개(0~7). `ladderStart`, 정답 경로 p0..p4, `ladderNext[r*32 + t*4 + d]`. 규칙: `next[r][p_r][answerDir[r]] = p_{r+1}`; 그 외 (t,d)는 `p_{r+1}`이 아닌 무작위 단자. → 4개 전부 정답이면 반드시 `ladderSafe = p4`, 하나라도 틀리면 반드시 다른 단자. 단자 라벨 = 8개 구역 이름(`ladderSafe`는 반드시 0 "아트리움 통로"가 되도록 라벨 순열을 시드로 섞되 안전 단자에 아트리움을 배정).
- 조작자 팝업(lever): 점자 셀(2×3, 켜진 점 밝게, 각 점에 작은 번호 1~6), "라운드 r/4", 방향 버튼 ▲▼◀▶(가운데 "레버"), "통역이 보낸 방향 카드: ▶"(없으면 "대기 중"), 점 번호 칩(누르면 채팅 입력칸에 "점자 1,2,4" 자동 작성 + Enter로 전송). 명령 `Lever a(0~3)`: `dirCard`가 이번 라운드에 도착했을 때만 허용(아니면 토스트 "통역의 방향 카드를 받아야 레버를 당길 수 있어요"). 처리: `chosenDir[round]=a`, 이벤트 `Wire a=round s="from>to"`, `round++`, `dirCard=-1`, `colorReport=-1`(색상등이 다음 라운드 색으로 바뀐다 — 통역 팝업은 "미확인"으로), 정오 판정 없음(누적만). `round==4`면 `status="배전함 판독 대기 — 내비게이터"`.
- 관측자 팝업(colorlamp): "색상등 · 라운드 r", 큰 원 = `lampColor2[round]`(round 4면 꺼짐), 신고 버튼 4개(`Color a` 재사용, 이벤트 target=1), 메모 "라운드마다 색이 바뀐다".
- 통역 팝업(mural): Table2 카드(각 행에 점자 패턴 그림 + 숫자 + 점 번호), "지금 받은 정보: 램프 ■색(관측자) / 점자: 조작자 채팅에서", 방향 카드 ▲▼◀▶ + "방향 카드 전송". 명령 `DirCard a` → `dirCard=a, dirCardClock`, 이벤트 `DirCard slot=1 a target=0`.
- 내비게이터 팝업(junction): 단자 1~8(라벨 = 구역 이름, `boardOpened`면 % 함께), 라운드별 배선(색 = 그 라운드 램프색, from→to), 판독 문구(서버가 계산해 `status`에 넣지 않는다 — 클라이언트가 `Stage1Logic.PathEnd(st)`로 계산해 "n번 단자(구역명)로 이어진다"만 표시, 안전 여부는 말하지 않음), 버튼 "안전 — 셔터 개방"(round==4일 때만) / "위험 — 처음부터". 명령 `Judge a`: a=1 → 실제 `PathEnd==ladderSafe`면 `gate2Done=true, step=2, gatherStart=Clock`, 이벤트 `GateOpen a=2`; 아니면 Penalty(20), 이벤트 `Alarm`, `ResetRounds()`. a=0 → `ResetRounds()`(감점 없음), 토스트 "처음부터 다시".
- `ResetRounds()`: `gate2Attempts++`, 새 시드 `seed + 101*gate2Attempts`로 숫자·색·사다리 재생성, `round=0, chosenDir=-1, dirCard=-1, colorReport=-1`, 이벤트 `RoundReset`.

### 5-7. 최종 집결 (`step==2`)
- 원 4개(슬롯 색), 반지름 `GatherRadius=0.95`. 중심: `GatherCenter(i, clock) = (0,0) + R·(cos θ, sin θ)`, θ = 90°·i + 45° + ω·(clock − gatherStart), ω = 0.11 rad/s, R = 2.55 + 0.35·sin(0.35·(clock − gatherStart) + i). (`Stage1Logic.GatherCenter` public static — 클라·서버·봇·테스트 공용)
- 미도착 점선 원 / 안에 있으면 실선 발광. 서버: 4명 모두 자기 원 안 상태가 `AllInHold=0.6초` 지속 → `Cleared()`. 하나라도 나가면 `allInSince=-1`.
- 상단 게이지 "n/4 도착"(클라이언트가 위치로 계산).

### 5-8. 클리어
`Cleared()` → 셔터 개방 연출(아트리움 북쪽 벽 셔터 스프라이트 올라감), "CLEAR" 배너 → 3.2초 후 StageClear.

---

## 6. 소통 규칙

### 6-1. 채팅
- 명령 `Chat s`(60자). 이벤트 `Chat slot s`. 표시 규칙(클라이언트 `Comms.CanUnderstand(viewer, sender)`): `viewer == sender || viewer == 1 || sender == 1`. 못 읽으면 말풍선·로그·피드에 `??? ??` 덩어리(기존 Garble) + "(외국어)".
- 채팅 입력은 모든 역할 패널의 맨 아래 한 줄(플레이스홀더 "메시지 · Enter" / 통역은 "모두가 읽을 수 있어요 · Enter"). T로 포커스.
- 서버 `BotDriver.OnChat(slot, text)` 훅(봇 통역이 조작자 채팅을 읽는다).

### 6-2. 6칸 휠(도넛) — 전원
- Q 또는 좌하단 "수신 로그" 아이콘 → 화면 중앙 도넛. 칸: 상좌 yes(○, 흰), 상우 no(×, 흰), 좌 where(?, 분홍), 우 go(→, 초록), 하좌 you(☝, 노랑), 하우 위험(!, 빨강). 각 칸에 내 픽셀 캐릭터 앞모습 + 기호.
- 명령 `Signal s ∈ {YES,NO,WHERE,GO,YOU,DANGER}`(target 없음). 이벤트 `Signal slot s` → 보낸 캐릭터 머리 위 **카드 말풍선**(색 카드 + 기호 + 영문 소문자 단어, 2.6초), 로그.
- 내비게이터 역할 패널의 go/위험/where/you 버튼과 침수 현황판 팝업의 go/위험도 같은 명령.

### 6-3. 색 신고 / 카드
- `Color a`: 관측자만, 근접 조건(lamp 또는 colorlamp). 수신자 통역: 통역 화면에 "관측자: ■노랑" 토스트 + 팝업의 "지금 받은 정보" 갱신 + 로그. 다른 사람 로그에는 "관측자 → 통역 [색]"으로 표시하되 색 이름은 통역만 보인다(다른 사람은 "[색 신고]").
- `Card a`(0~9) / `DirCard a`(0~3): 통역만, 근접 조건(poster / mural). 수신자 조작자: 토스트 + 팝업/패널 표시 + 로그. 다른 사람 로그: "통역 → 조작자 [카드]".

### 6-4. 수신 로그(C) — 읽기 전용 패널
최근 12개: 채팅(가시성 규칙 적용), 휠 신호(카드 아이콘), 색 신고, 카드, 핑. 보낸 이 색 배지 + "n초 전".

---

## 7. 서버 상태 모델 (`GameState.cs`) — 정확한 이름

```csharp
[Serializable] public class StageState {
    public int stage = 1, seed, retries, checkpoint;
    public bool timed = true; public float timeLimit = 720f, startClock, penalty;
    public bool cleared, failed; public float clearClock;
    public int step; public int moveMode = 1; public string status = "";
    // 관문 1
    public int calcA, calcB, calcC, calcOp, calcResult, lampColor, calcAnswer, calcFails;
    public string calcInput = ""; public bool gate1Done;
    // 소통
    public int colorReport = -1; public float colorReportClock = -99f;
    public int numberCard = -1; public float numberCardClock = -99f;
    public int dirCard = -1; public float dirCardClock = -99f;
    public int lastPingRoom = -1; public float lastPingClock = -99f;
    public bool boardOpened;
    // 관문 2
    public int round; public int[] brailleDigit = new int[4]; public int[] lampColor2 = new int[4];
    public int[] answerDir = new int[4]; public int[] chosenDir = { -1, -1, -1, -1 };
    public int ladderStart; public int[] ladderNext = new int[128]; public int ladderSafe;
    public int[] terminalZone = new int[8];   // 단자 i의 구역 라벨(0~7), terminalZone[ladderSafe] == 0
    public int gate2Attempts; public bool gate2Done;
    // 침수
    public float[] floodBase = new float[8]; public float[] floodRate = new float[8];
    // 최종 집결
    public float gatherStart; public float allInSince = -1f;
}
```
`SessionState`는 유지(hostControlSlot, botsAuto 포함). `Stats`: playSeconds, wrongInputs, cards, chats, signals, retries.
`LetterMsg`, rail, fork 등 옛 필드는 모두 삭제.

### 7-1. 명령(Cmd.type) 전체
`Slot(a)`, `FillBots`, `StartGame`, `Name(s)`, `Ready(a)`, `Back`, `Next`, `Skip`, `Retry`, `Again`, `Control(a)`, `BotsAuto(a)`,
`Signal(s)`, `Chat(s)`, `Color(a)`, `Card(a)`, `DirCard(a)`, `Digit(a)`, `DigitClear`, `DigitConfirm`, `Lever(a)`, `Judge(a)`, `Ping(a)`, `OpenBoard`.

### 7-2. 이벤트(GameEvent.type)
`Toast`, `Flash`, `Teleport(slot,pos)`, `StageStart`, `Clear`, `Fail(s)`, `Signal(slot,s)`, `Chat(slot,s)`, `Color(slot,a,target=1)`, `NumberCard(slot,a,target=0)`, `DirCard(slot,a,target=0)`, `Wire(a=round,s="f>t")`, `GateOpen(a=1|2)`, `Alarm(s)`, `Ping(slot,a=room,pos)`, `Judge(a,s)`, `RoundReset`.
`Session.Dispatch`가 로그에 남기는 타입: Signal, Chat, Color, NumberCard, DirCard, Ping, Toast.

### 7-3. 공용 정적 도우미 (`Stage1Logic`, 클라·봇·테스트가 같이 씀)
`Parity(int) : int(0 짝 1 홀)`, `Table1[2][4]`, `Table2[4][4]`, `Row(int digit) : int`, `BrailleDots(int) : int[]`, `Flood(StageState, int zone, float clock) : float`, `IsDanger(StageState, int room, float clock)`, `PathEnd(StageState) : int`(chosenDir로 따라간 단자, 미완이면 −1), `GatherCenter(int slot, StageState, float clock) : Vector2`, `InGather(int slot, Vector2 pos, StageState, float clock)`, `ZoneName(int)`, `ColorName(int)`, `DirGlyph(int)`, `RoomDoor(int room) : Vector2`(핑 비콘 위치).

---

## 8. 스테이지 HUD (`StageScreen` + 부품) — PDF s12 배치

| 위치 | 부품 | 내용 |
| --- | --- | --- |
| 상단 바(높이 62px, 전체 폭) | `TopBar` | [STAGE 1 · PHILIPPINES / 침수된 박물관] [현재 관문: … ■□□ (+ 라운드 r/4)] [팀 4칸: 코드 배지 + 역할 + 담당 오브젝트 이름, 내 칸 강조] [침수까지 mm:ss 빨강 모노] [? 버튼 → 조작 안내 시트] |
| 좌상단 | `RoleBadge` | 코드 배지 + 역할명 + "국적 · 12세" + 이름 |
| 우상단 (360×280) | `MinimapView` | "박물관 1층 · 내 담당 표시 / TAB(확대)". 내비: 방 6개 윤곽 + 방 이름 + 미션 ! 마커(관문 1: 방 1, 관문 2: 방 3, 현황판: 방 2) + 위험 방 물결 + 4명 점 + 핑. 나머지: 아트리움만 밝고 다른 방은 "?" 회색, 내 담당 오브젝트가 발광한 방만 색으로. 탈출(집결) 지점 점선 원 |
| 좌하단 세로 열 | `ActionBar` | [수신 로그 Q·도넛 열기] [지난 신호 C] [상호작용 E] [확대 휠 클릭] — 클릭도 동작 |
| 우하단 (360×360) | `RolePanel` | 역할별 패널(아래) + 맨 아래 채팅 입력 1줄 |
| 중앙 | `Popups`, `Donut`, `LogPanel`(좌측), `TutorialOverlay`, 최종 집결 게이지 패널(중앙 상단, PDF s31) | |
| 하단 중앙 | 토스트(위로 쌓임) + 상호작용 안내 "[E] 제어반 조사" | |
| 우측(호스트+봇) | 조종 전환 버튼 4개(기존) — 미니맵 아래 | |

RolePanel:
- 조작자: "조작자 콘솔 · 자유 채팅 가능" — 관문 진행 목록 3줄(1 계산 자물쇠·안내데스크 / 2 방향 자물쇠·나침반 모자이크방 / 3 최종 집결·중앙 분수 아트리움, 진행 중/잠김/완료), "통역이 보낸 카드: 숫자 n / 방향 ▶", 채팅.
- 통역: "통역 · 숫자/방향 카드" — 전송 대기 표시, 숫자 1~0 버튼, 방향 4버튼, "조작자에게 전송", "지우기", 안내 "숫자·방향 카드는 조작자 화면에만 도착한다", 채팅(모두 읽음). 카드 버튼은 팝업 밖에서도 누를 수 있지만 서버 근접 조건(poster/mural 근처)이 필요 → 멀면 토스트 "벽보 앞에서 보내야 해요".
- 관측자: "관측자 · 색상 신고" (우측 "도넛 · 채팅(통역만 읽음)") — 색 버튼 4개(빨강/노랑/초록/파랑), 최근 신고 목록, 채팅.
- 내비게이터: "내비게이터 · 침수 현황판" (우측 "도넛 · 채팅(통역만 읽음)") — 구역 막대 8줄(현황판을 연 뒤에만 값 표시, 전에는 "현황판에서 확인"), go/위험/where/you 버튼, "여기로!" 방 버튼 6개(Ping), 채팅.

### 8-1. 튜토리얼 (`TutorialOverlay`)
1. 스테이지 시작 시 역할 팝업 "당신은 ○○입니다" (PDF s15~18 문구: 할 수 있다/할 수 없다 3개씩, "4명 전원이 동시에 집결 지점(중앙 분수)에 도착해야 클리어된다") → 확인.
2. 코치마크 5단계(PDF 06-2): ① 좌하단 액션 열(WASD/E) ② 좌상단 역할 배지("역할은 고정") ③ 상단 팀 목록("네 화면은 서로 다르다") ④ 로그 아이콘("Q로 여섯 칸 도넛") ⑤ 타이머("0이 되면 마지막 관문 앞에서 재시작, 오답은 시간 감소"). 마지막 버튼 "시작". "건너뛰기" 항상.
3. 상황 힌트(기획서 온보딩 표): 관문 1 동안만, 역할 패널 상단 "지금 할 일" 한 줄이 상태에 따라 바뀐다(`StageGuide`). 관문 1 클리어 직후 전원 토스트 "이렇게 서로 다른 정보를 모아야 퍼즐이 풀려요. 이제부터는 스스로 해보세요!".
4. 상단 ? → 조작 안내 시트(PDF 06-1 5항목) 언제든.
- 테스트 훅: `TutorialOverlay.AutoSkip`(static bool) = true면 표시하지 않음. 오버레이가 떠 있는 동안 서버 시간은 흐른다(12분이면 충분). 재시도 스테이지(`retries>0`)는 표시하지 않는다(14-1 D1(2): 재시도는 튜토리얼 없이 타이머 즉시 시작).

### 8-2. 테스트 훅(정적)
`LocalNav.Go`, `TutorialOverlay.AutoSkip`, `StageScreen.Instance.OpenPopup(string propId)`, `StageScreen.Instance.ClosePopup()`, `StageScreen.Instance.OpenDonut()`, `StageScreen.Instance.ShowLog(bool)`, `StageScreen.Instance.IsPopupOpen`, `StageScreen.Instance.IsPopupDone`, `AvatarNet.InDanger`/`AvatarNet.CurrentSpeed`(이번 프레임 속도, 위험 구역 ×0.5), 호스트 명령 `ForceFail`. 호스트 권한 상태는 `SessionNet.Instance.Logic.S.st`(같은 프로세스)로 직접 만질 수 있다 — `MarkDirty()` 뒤 브로드캐스트.

---

## 9. 봇 (`BotDriver` 서버 명령 + `BotBrain` 이동)

지시 게이팅: 조작자·통역·관측자 봇은 `lastPingRoom == 목표 방` 또는 내비의 GO 신호 이후, 또는 단계 시작 25초 후에 움직인다. 내비 봇은 단계 시작 2초 후부터 행동.

| 역할 | 관문 1 | 이동/관문 2 | 최종 집결 |
| --- | --- | --- | --- |
| 내비(2) | 현황판(board)으로 → `OpenBoard` → `Ping 1` + `Signal GO` → 아트리움에서 대기 | `Ping 3` + GO → 배전함(junction) 앞 → round==4가 되면 `PathEnd==ladderSafe ? Judge 1 : Judge 0`(위험·아니오 몸짓 카드는 서버가 Judge 0 을 받아들이며 대신 발행, D-A5) | 자기 원(움직이는 목표) 추적 |
| 관측자(3) | lamp 앞 → `Color lampColor`(2초 뒤) | colorlamp 앞 → 라운드가 바뀔 때마다 `Color lampColor2[round]` | 자기 원 |
| 조작자(0) | panel 앞 → `Chat "결과 짝수야/홀수야"` → `numberCard`가 오면 `Digit`, `DigitConfirm` | lever 앞 → 라운드마다 `Chat "점자 1,2,4"` → `dirCard` 오면 `Lever dirCard` | 자기 원 |
| 통역(1) | poster 앞 → (colorReport ≥ 0 && 조작자 채팅에서 홀/짝 파악) → `Card Table1[p][c]` | mural 앞 → (색 + 채팅의 점 번호 → 숫자 → 행) → `DirCard` | 자기 원 |

봇 통역의 채팅 해석: "짝" 포함 → 0, "홀" 포함 → 1; "점자" 뒤 숫자 목록을 집합으로 만들어 `BrailleDots`와 일치하는 숫자를 찾는다. 사람 조작자가 자유 문장으로 써도 같은 규칙으로 최대한 해석한다(못 읽으면 `Signal WHERE`로 되묻고 6초 뒤 재시도).
BotBrain 목표: 담당 오브젝트 위치 + (0, −0.9); 최종 집결은 `GatherCenter(slot)`(0.5초마다 재계산, stop 0.25). 팝업 개념이 없으므로 봇은 근접만 하면 명령한다.

---

## 10. 아트/대체 자원

- 캐릭터: `Assets/Art/Characters/{korean,phil,vietnam,southafrica}.png`(이미 있음, `GameApp.roleSheets` 배선 완료).
- 배경: `Assets/Resources/Backgrounds/{classroom,school,camp}.png` 슬롯. 없으면 코드 배경. README에 넣는 법 안내.
- 오디오: 프로시저럴(`Core/Sfx.cs`, `AudioClip.Create` 44.1kHz 모노 — alarm·siren·wind·thud·clear·click·chime). `Resources/Sfx/*.wav` 가 있으면 우선 사용(선택, `Sfx.Play` 가 `Resources.Load<AudioClip>("Sfx/"+id)` 를 먼저 본다).
- 로고: 코드(텍스트 + 랜턴 빛 원 + 발자국 타원 6개).
- NPC 초상: 코드 실루엣.
- 오브젝트 아이콘: `SpriteLib.Icon`에 `panel, poster, lamp, board, leverpad, mural, colorlamp, junction, fountain, shutterdoor, beacon` 추가(16×16 픽셀, 담당자 발광은 뒤에 `Disc` 앰버 글로우 + 링).
- 휠 카드 말풍선: 색 카드(사각) + 기호 텍스트 + 단어.
- 바닥: 방마다 `FloorTile` 색 다르게(아트리움 밝은 베이지, 안내데스크 회청, 관리실 회색, 모자이크방 나침반 무늬(원 + 십자), 수족관방·연못정원방 청록), 침수 오버레이(방 사각형 위에 파란 반투명, 알파 = 침수율/100 × 0.55, 물결 흔들림).

---

## 11. 테스트 (`Assets/Tests/PlayMode`)

- `Flow.cs`: `Boot()`(씬 로드 + `LocalNav.Go(LocalScreen.Match)`, `TutorialOverlay.AutoSkip=true`, 단언은 `GameApp.Instance.HasRoleArt(0..3)` 전부 true), `ToMatch()`(LocalNav.Go(Match)), `StartSolo(botsAuto)`, `LobbyToStage()`(StartGame → Name(이름) → Ready → Ready 화면 → StageIntro → Skip → StagePlay, 아바타 4), `Gate1Manual()`, `Gate2Manual(bool wrongFirst)`, `GatherAll()`(1초 동안 매 프레임 4명을 `GatherCenter`로 텔레포트), `Until`(기존).
- `FullFlowTests`: 봇 수동. 로컬 화면 전이(로고 스킵 → 타이틀 → 프롤로그 Skip → 스테이지 선택 → PLAY → 매칭) → 혼자 테스트 → 이름/준비 → 오프닝(조작자 시점 정상, 통역 시점 ??? 확인은 투어에서) → 관문 1(오답 페널티 확인 → 정답) → 관문 2(틀린 레버 1개 포함 4라운드 → 안전 판정 → 페널티·리셋 확인 → 4라운드 정답 → 안전 → step 2) → 집결 → Clear → StageClear Skip → Result → Again → Lobby → Shutdown.
- `BotFlowTests`: 사람 = 조작자만, 봇 자동. 내비 봇의 Ping 대기 → 제어반 근처로 텔레포트 → `Chat 짝수/홀수` → numberCard 대기 → Digit/Confirm → 관문 2: 라운드마다 `Chat 점자 …` → dirCard 대기 → Lever → 봇 판정 → 집결(조작자만 텔레포트 추적, 봇은 걸어감) → Clear.
- `ScreenshotTour`: 로컬 6화면, 로비, 이름, 준비, 오프닝(KOR/PHL 시점), 스테이지 4역할 시점, 팝업 8개, 도넛, 로그, 코치마크 1장, 집결, 클리어, 결과.
- `Assets/Tests/EditMode`(어셈블리 `Paradox.EditModeTests`, Editor 전용): `CommsTests`(표시 규칙 순수 함수), `Stage1RuleTests`(IsDanger·Flood·사다리·점자 진리표), `GameLogicHeadlessTests`(GameLogic 을 네트워크 없이 구동 — 브리핑 게이트 30초, 시간 초과·재시도, 관문 2 게이팅, 집결 0.6초). 수 초 안에 끝나므로 규칙을 고치면 먼저 돌린다.
- 실행: 스크래치 `run_tests.ps1 -Filter <이름> -Tag <태그> [-Shots] [-CompileOnly] [-Platform EditMode]`(PxCopy로 동기화 후 배치 실행).

---

## 12. 작업 분할(파일 소유권)

| 담당 | 소유 파일 | 만들거나 바꾸는 것 |
| --- | --- | --- |
| CORE | Game/GameState.cs, GameRole.cs, StageDefs.cs, StageMaps.cs, Stage1Logic.cs, GameLogic.cs, StoryData.cs, BotDriver.cs, (삭제) Stage2~4Logic.cs | 7절 상태·명령·이벤트, 4절 맵, 5절 규칙, 9절 봇 명령, 프롤로그/오프닝/클리어 대사 데이터 |
| SCREENS | Client/LocalNav.cs, Progress.cs, Backgrounds.cs, VnView.cs, LogoScreen.cs, TitleScreen.cs, PrologueScreen.cs, StageSelectScreen.cs, MatchScreen.cs, SettingsScreen.cs, LobbyScreen.cs, NameSetupScreen.cs(CharacterSelectScreen 대체), ReadyScreen.cs, CutsceneScreen.cs, ResultScreen.cs, ScreenManager.cs, UIPortrait.cs | 1·3절 |
| HUD | Client/StageScreen.cs, StagePanels.cs(StageTools 대체), Popups.cs, Donut.cs, LogPanel.cs, TutorialOverlay.cs, StageGuide.cs, MinimapView.cs, Comms.cs(가시성 규칙·카드 말풍선 텍스트) | 6·8절 |
| WORLD | Client/StageWorld.cs, StageInteractions.cs, AvatarView.cs, Net/AvatarNet.cs(BotBrain 포함), Core/SpriteLib.cs, CameraFollow.cs, FeedbackFX.cs | 4·5-3·5-5 위험·5-7 원·9절 이동·10절 아이콘 |
| TESTS | Tests/PlayMode/* | 11절 |

경계 규칙: 자기 소유가 아닌 파일은 고치지 않는다. 다른 영역의 API가 필요하면 이 문서의 이름을 그대로 호출하고, 문서에 없는 것이 필요하면 `Docs/integration_requests.md`에 "누가 → 무엇을" 적는다(통합 단계에서 처리).

---

## 13. 결정 목록 (기획서에 없어서 정한 것)

1. 점자 전달: 조작자 팝업이 점 위치에 번호(1~6)를 붙여 주고, 조작자는 채팅으로 "점자 1,2,4"처럼 보낸다. 통역 매뉴얼에는 각 숫자의 점자 그림과 점 번호가 함께 있다. → 조작자는 여전히 숫자를 모른 채 "모양"만 전달하고, 해독은 통역만 한다.
2. 배전함: 8단자 사다리. 4개 정답이면 반드시 아트리움 통로(안전), 하나라도 틀리면 반드시 다른 단자. 안전 여부는 내비가 침수 현황판(구역 %)과 대조해 판단. "안전" 오판은 −20초 + 라운드 리셋, "위험"은 감점 없이 리셋.
3. 채팅: 기획서 규칙 그대로(PDF의 "통역 문장 채팅 불가"는 폐기).
4. 시간 12분, 오답 −15초, 오판 −20초, 0초면 마지막 관문(체크포인트)부터 12분으로 재시작. 위험 구역은 속도 절반 + 경고(감점 없음).
5. 내비 지시: 휠 + "여기로!" 방 핑(비콘). 이동 강제는 없음(정보 비대칭으로 유도, 봇은 지시를 기다림).
6. 온라인: 프롤로그·스테이지 선택은 로컬, PLAY 뒤 매칭 화면(방 만들기/참가/혼자 테스트), 이름 설정은 각자 자기 칸만 편집, 준비 완료는 서버 자동 전환. 이어하기 = 프롤로그 건너뛰기. 설정 = 음량·전체화면·기본 이름.
7. 뷰: 탑다운 고정 카메라(한 층 전체). 맵 4절 좌표.
8. 최종 집결: 자기 색 원, 반지름 0.95, 분수 주위를 52초에 한 바퀴 느리게 회전 + 반지름 출렁임, 4명 0.6초 유지 시 클리어.
9. 관문 2: 라운드별 정오 판정 없음(누적). 레버는 방향 카드 수신 후에만. 색상등은 라운드마다 바뀜.
10. 휠은 전원(몸짓은 만국 공통). 색 신고 수신 = 통역만(다른 사람 로그엔 "[색 신고]"), 카드 수신 = 조작자만.
11. 배경 3장은 `Assets/Resources/Backgrounds/`에 넣으면 자동 사용, 없으면 코드 배경. 로고·NPC 초상·오브젝트는 코드.
12. Stage 2~4 잠금, 옛 로직은 Prototype_Backup으로 이동.
13. 튜토리얼: 역할 팝업 → 코치마크 5단계 → 상황 힌트(관문 1) → ? 시트. 전부 건너뛰기 가능.
14. PDF 키패드에 0 없음 → 0 포함(매뉴얼 표에 0이 있음).
15. 봇은 9절대로 실제 역할을 수행(팝업 없이 명령만, 근접 조건 동일).
16. (T-01) 온보딩은 말풍선 오버레이 대신 역할 패널 "지금 할 일" 한 줄 + 코치마크. 관문 1은 튜토리얼 어조의 문장, 관문 1 뒤는 "· " 접두의 짧은 한 줄 지시(`StageGuide.Gate1/Move`) — 항상 표시.

---

## 14. v2 변경 목록 (설계 심사 반영 — 1차 구현 뒤에 적용)

세 갈래 설계 제안(A 기획 충실 / B 12세 UX / C 구현·테스트)과 심사 2건을 검토해 아래만 채택했다. 위 절의 내용과 다르면 **이 절이 우선**한다.

| # | 변경 | 근거 | 영향 파일 |
| --- | --- | --- | --- |
| D1 | **브리핑 게이트**: 스테이지 시작 후 `timerRunning=false`. 사람 슬롯 전원이 `Brief` 명령을 보내거나(튜토리얼 마지막 "시작" 버튼 / ? 시트 닫기) 스테이지 시작 30초가 지나면 `startClock=Clock, timerRunning=true`, 이벤트 `TimerStart`. 봇은 1.5초 뒤 자동 `Brief`. 침수율·타이머는 `timerRunning` 전에는 0에서 정지(HUD "준비 중 · 시작! n/4"). 상태 `bool[] briefDone = new bool[4]`, `bool timerRunning`. `TimeLeft`는 timerRunning 전에는 timeLimit. | 튜토리얼을 읽는 동안 시간이 흐르는 문제(세 제안 공통) | GameState, Stage1Logic, GameLogic, BotDriver, TutorialOverlay, StageScreen(타이머), Flow(테스트가 4슬롯 Brief 전송 — `TutorialOverlay.AutoSkip`이면 화면이 자동으로 Brief를 보낸다) |
| D2 | **안전 단자를 현황판이 알려준다**: 침수 현황판 팝업에 "비상 전원 라인 1~8" 줄을 추가하고 `ladderSafe+1`번만 ○(건전), 나머지 ✕(침수 단락). 배전함 팝업은 `boardOpened`면 "안전 라인: n번 (현황판에서 확인)", 아니면 "안전 라인: 미확인 — 침수 현황판을 보고 오세요". 단자 라벨은 번호 1~8(구역 이름 병기 삭제). 판정 문구는 "n번 단자로 이어진다"만. | 구역 % 비교는 12세에게 모호(어느 %부터 위험인지) — 두 심사 모두 ○ 표시 채택 | Popups(board, junction), Stage1Logic(terminalZone 미사용) |
| D3 | **페널티 값**: 관문 1 오답 −10초 + 키패드 3초 잠금(`lockUntil1`, 팝업 유지·입력만 비활성), 배전함 "안전" 오판 −15초. 위험 재시도 0. | 12세 좌절 방지, 무차별 입력 억제 | Stage1Logic 상수, Popups(panel 잠금 표시) |
| D4 | **조작자에게는 도넛(휠)이 없다**(기획서 9절 그대로). 대신 조작자 팝업에 빠른 문구 버튼: 제어반 [홀수라고 말하기] [짝수라고 말하기] → `Chat s="결과 홀수야/짝수야" a=1 b=(1 홀/0 짝)`; 레버반 점 클릭 → 입력칸 "점자 1,2,4" 자동 작성 + [통역에게 말하기] → `Chat a=2 b=점 비트마스크(1<<(점-1))`. `a/b`는 봇 해석 우선순위용이며 UI 강조·판정에는 쓰지 않는다(통역은 원문을 읽는다). `BotDriver.OnChat`은 a/b가 있으면 그것을, 없으면 텍스트 파서를 쓴다. 조작자의 Q 키는 "도넛은 없어요 — T로 말하면 통역만 알아들어요" 토스트. | 조작자의 고립이 이 스테이지의 메시지(심사 2건 일치) | Donut(조작자 제외), StageScreen, Popups, GameLogic(Chat a/b 통과), BotDriver |
| D5 | **핑 말풍선에 방 이름을 쓰지 않는다**(비내비게이터 화면): 내비 머리 위 "→ 여기로!" 카드 + 방 입구 비콘 + 미니맵 핀. 내비 자신의 화면·로그에만 방 이름. | 표지판 ??? 원칙(방 이름은 현지어) | AvatarNet/AvatarView, Comms, LogPanel |
| D6 | **팝업 UX 3건**: (a) 방향 카드가 오면 레버반의 해당 방향 버튼이 미리 선택(강조)된다(조작자가 바꿀 수 있음). (b) 관측자 패널·색상등 팝업에 라운드별 신고 기록("1R 빨강 · 2R 초록") 자동 표시. (c) 담당 미션이 끝나면(gate1Done/gate2Done) 팝업은 "완료 ✓" 상태로 바뀌고 3초 뒤 자동으로 닫힌다(이동 잠금 해제). 오답·오판·리셋에는 절대 닫지 않는다. | 12세 UX(B), 성공 후 "왜 안 움직여?" 방지 | Popups, StagePanels |
| D7 | **지름길 통로 2개**: 수족관방(4) ↔ 관리실(2) 문 open (−6,−6.5, −2.5,−5) 와 연못정원방(5) ↔ 안내데스크(1) 문 open (−6,5, −2.5,6.5). 위험 방이 막다른 방이 아니라 "빠르지만 침수되는 지름길"이 되어 내비게이터의 침수 판단과 위험 핑이 의미를 갖는다. 봇 길찾기는 침수율 ≥ 70인 방을 피한다: `MapData.FindPath(from, to, r, avoid: IList<Rect> = null)` 오버로드 추가, BotBrain이 위험 방 rect를 넘긴다. | 세 제안 모두 "정석 경로/지름길" 구조를 권함 | StageMaps(문 2개), AvatarNet(BotBrain avoid), MinimapView(문 표시) |
| D9 | **카메라 프레이밍**: 1차 투어 스크린샷에서 모자이크방(x 5~10.5)의 오른쪽이 미니맵·역할 패널(화면 x ≥ 76%) 아래에 깔리고 방 이름이 잘린다. 고정 카메라를 orthographicSize 10.0, 중심 (1.2, −0.6)으로 바꿔 맵 오른쪽 끝(x 10.5)이 화면 76% 안에, 왼쪽 끝(x −11)이 액션 열(220px) 오른쪽에 오게 한다. 투어 스크린샷(10_stage_*)으로 확인. | 1차 투어 검수 | CameraFollow/StageWorld(고정 카메라 값) |
| D10 | **스폰 간격과 이름표 겹침**: 네 명이 1.4유닛 간격으로 스폰해 머리 위 역할 배지·이름표·말풍선이 서로 겹친다. 스폰을 x −3.3, −1.1, 1.1, 3.3(y −2.4)로 벌리고, 배지 폭을 이름 길이에 맞춰 줄인다(기존 0.9+0.24×글자수 → 0.7+0.2×글자수). 최종 집결 원 표시도 겹치지 않게 확인. | 1차 투어 검수 | StageMaps(spawns), AvatarView(배지) |
| D11 | **바닥 방 이름 라벨은 내비게이터·통역에게만**: 조작자·관측자 화면에서는 월드 바닥의 방 이름 라벨을 그리지 않는다(호스트가 조종 슬롯을 바꾸면 갱신). 방 *이름*은 조작자 콘솔·상단 바에 이미 나오지만(PDF s15), 그 이름이 *어느 방*인지는 내비게이터의 "→ 여기로!" 비콘·미니맵 핀으로만 알게 해 안내 역할을 지킨다. D9의 "10_stage_* 4장에서 방 이름 확인"은 통역·내비 2장(1in·2na)에만 적용. | QA 검수(오케스트레이터 orch-E1). 검증자는 "D9와 일치하므로 코드 문제 아님"이라 기각했고, 이 항목이 그 기획 결정이다. | StageWorld(RefreshProps) |
| D12 | **브리핑 게이트 중 관문 명령 거부**: `timerRunning`이 false면 서버가 Digit/DigitClear/DigitConfirm/Color/Card/DirCard/Lever/Judge를 거부하고 토스트("아직 준비 중 — 모두 [시작]을 누르면 시작해요"). Chat/Signal/Ping/OpenBoard/Brief/이동은 허용. 봇은 타이머 시작 전에는 행동하지 않는다. | 빠른 플레이어가 타이머·침수가 멈춘 채 관문 1을 푸는 것을 막는다(delta 보고 "남은 우려"). | GameLogic(IsGateCommand), BotDriver.Tick |
| D8 | **문서**: README에 "PC · 같은 네트워크(LAN) 전용, 웹 브라우저 빌드는 범위 밖(UDP 트랜스포트)" 명시. | PDF 타이틀 "웹 브라우저" 문구와의 차이 | README_STRANGER |

채택하지 않은 것(이유): 팝업 열림을 서버 상태로 두고 명령 수락 조건으로 삼는 안(근접 조건으로 충분, 봇·테스트 단순), 오프닝을 각자 넘기는 안(동기화 복잡), 침수 100% 봉쇄·밀려남(동적 solid·끼임 위험), 튜토리얼 1장 카드로 축소(PDF 코치마크 유지), 제한시간 600초(12세 첫 플레이는 720초), 배전함 첫 오답 라운드부터 재시도(정보 누설), 판정 시 "사실은 안전" 알려주기(판독 역할 붕괴).

### 14-1. 적용 결과 (v2 구현 뒤 기록 — 명세와 다른 점만)

| # | 실제 구현 | 명세와 다른 점 / 보충 |
| --- | --- | --- |
| D1 | `StageState.briefDone[4]`, `timerRunning`, `lockUntil1` 추가. 명령 `Brief`(GameLogic), 이벤트 `TimerStart`. `GameLogic.TimeLeft`·`Session.TimeLeft`·`Stage1Logic.Flood`는 `timerRunning` 전에는 timeLimit / floodBase 고정. 30초(`Stage1Logic.BriefTimeout`) 자동 시작. 봇 Brief는 `BotDriver.Tick`(1.5초, `BotBriefDelay`) — 호스트가 조종 중인 봇도 자동. HUD 타이머 "준비 중 · 시작! n/4"(`GameLogic.BriefCount`). | (1) 타이머 시작 조건은 "사람 슬롯 전원"이 아니라 **present 슬롯 전원**의 briefDone — 봇은 `botsAuto`일 때만(BotDriver가 돌 때만) 자동 Brief 하므로, 봇 수동 테스트(botsAuto=false)는 4슬롯 Brief를 직접 보낸다(`Flow.BriefAll`). 사람만 세면 AutoSkip 화면의 자동 Brief 때문에 "Brief 전 TimeLeft==timeLimit" 검증이 불가능해서 이렇게 정했다. (2) **재시도(Retry)는 튜토리얼이 없으므로 타이머를 즉시 시작**한다(`StartTimer(quiet)`). (3) `startClock`은 타이머 시작 순간 다시 찍는다 → `BotBrain.TrackStep`의 새 스테이지 판정을 startClock 대신 seed·retries 로 바꿨다. (4) 브리핑 전에도 이동·명령은 막지 않는다(팝업 잠금만 튜토리얼이 쥔다). |
| D2 | 현황판 팝업에 "비상 전원 라인 1~8" 셀(○/×), 내비 역할 패널에도 "안전 라인: n번" 한 줄. 배전함은 단자 라벨 번호만 + `boardOpened`면 안전 라인 아래 ○, 판독 문구 "n번 단자로 이어진다" + "안전 라인: n번 (현황판에서 확인)" / "미확인 — 침수 현황판을 보고 오세요". `Stage1Logic.SafeLine(st) = ladderSafe+1`. | `terminalZone`은 생성·`ValidateLadder`에는 남겨 두고 UI에서만 쓰지 않는다. 글자는 ✕ 대신 허용 기호 ×. 배전함에서 %는 더 이상 표시하지 않는다. |
| D3 | `Gate1Penalty=10`, `Gate2Penalty=15`, `Gate1Lock=3`. 오답 시 `lockUntil1 = Clock+3`; 서버가 `Digit`/`DigitConfirm`을 거부(토스트 "키패드 잠금 중 — n초"). 제어반 팝업은 키패드 비활성 + 카운트다운, 키보드 입력도 무시. | 잠금 중 `DigitClear`는 허용(입력이 비어 있어 의미 없음). |
| D4 | `StageScreen.OpenDonut`/`Donut.Open`이 조작자면 토스트만. 제어반 팝업 [홀수라고 말하기]/[짝수라고 말하기] → `Chat a=1 b=1/0`, 레버반 팝업 [통역에게 말하기] → `Chat a=2 b=점 비트마스크`(점 칩의 채팅 초안도 유지). `GameEvent.b` 추가, `GameLogic.Chat(slot,text,a,b)`, `BotDriver.OnChat(slot,text,a,b)`는 a/b 우선, 없으면 텍스트 파서(`ParseDots`). | 액션 열의 "수신 로그 Q" 아이콘은 그대로 두고 눌렀을 때 토스트. 코치마크 4단계 본문에 "조작자는 도넛 대신 T 채팅" 한 줄 추가. 투어 `11_donut_op_none`(조작자 토스트) / `11_donut_ob`(관측자 도넛). |
| D5 | `AvatarNet` Ping 말풍선: 내비 자신 화면만 "→ 방 이름", 나머지는 "→ 여기로!"(부제 "입구의 표시를 따라가요"). `Comms.LogText` Ping도 내비만 방 이름. 월드 비콘·미니맵 핀은 원래대로 모두에게. | — |
| D6 | (a) 레버반: 방향 버튼은 선택만, [레버 당기기]가 `Lever` 전송. 방향 카드가 오면 `_selLever`가 그 방향으로 미리 선택. (b) 색상등 팝업 기록을 라운드별(같은 라운드는 최신으로 교체, 오래된 것부터 "1R 빨강 · 2R 초록"). (c) `Popups.OnState`가 `MissionDone`이면 즉시 닫지 않고 "완료 ✓" 본문으로 바꾸고 `Popups.DoneCloseDelay=3`초 뒤 `Tick`이 닫는다(이동 잠금은 닫힐 때 해제, × 로 바로 닫기 가능). 이미 끝난 미션의 팝업을 열어도 바로 "완료 ✓". 실패(failed)는 여전히 즉시 닫는다. 테스트 훅 `StageScreen.IsPopupDone`. | 1차 코드는 완료 즉시 닫았다(확인). `Flow.Gate1Manual`이 제어반 팝업을 연 채 정답을 넣어 "완료 ✓ ≥1.2초 유지 → 3초 안에 닫힘"을 검증하고 투어가 `24_popup_panel_done_op`를 찍는다. 봇은 `Lever`를 직접 보내므로 영향 없음. |
| D7 | `StageMaps.ShortcutDoors` 2개를 `open`에 추가. **좌표를 (−6,−6.5,−2,−5)·(−6,5,−2,6.5)로 조정** — 명세의 x −2.5는 수족관방·연못정원방(xMin −3)과 0.5유닛만 겹쳐 1유닛 규칙에 어긋난다. `MapData.FindPath(from,to,r,IList<Rect> avoid,maxExpand)` 오버로드(목표 칸만 avoid 예외). `BotBrain.DangerRects`(침수 ≥70 방, 내 방·목표 방 제외)를 `AvatarNet.StepAlongPath`가 넘기고, 못 찾으면 avoid 없이 재탐색. 미니맵·월드는 `open` 전부를 문으로 그리므로 자동 반영. | FullFlow가 수족관방→관리실 최단 경로가 지름길을 지나고 avoid 시 우회하는지 검증. |
| D8 | README 첫머리에 "PC · 같은 네트워크(LAN) 전용, 웹 브라우저 빌드는 범위 밖(UDP 트랜스포트)" + v2 규칙(브리핑 게이트·페널티·안전 라인·지름길·조작자 도넛 없음) 반영. | — |
| D9 | 고정 카메라 중심 (1.2, −0.6), orthographicSize 10.0(`StageWorld.ApplyCamera`, `CameraFollow` 기본값). 투어 `10_stage_*`에서 모자이크방 오른쪽 벽(x 10.5 → 화면 약 1220px)이 미니맵(1224px~) 왼쪽에 들어오고 방 이름이 온전히 보이는 것을 확인. | 관리실(방 2) 이름 라벨은 왼쪽 아래가 액션 열(250px)에 가려 **오른쪽 위(현황판 아이콘과 겹쳐 이동)**로 옮겼다(맵 폭 21.5유닛은 액션 열과 미니맵 사이에 다 들어가지 않는다). 방 이름 라벨은 내비게이터·통역 화면에만 보인다(QA orch-E1). 모자이크방 이름을 가리던 표지판 `sign2`를 (7.8,−2.6) → (6.2,4.0)으로 옮겼다. |
| D10 | 스폰 x −3.3/−1.1/1.1/3.3(y −2.4), 배지 폭 0.7+0.2×글자수. 투어 `10_stage_*`·`12_log`·`40_gather`에서 배지·이름표가 겹치지 않는 것을 확인. | 추가로 홀수 슬롯의 말풍선·카드를 0.7유닛 위로 올려(`AvatarView._lift`) 이웃 말풍선과 엇갈리게 했고, 말풍선 글자 크기 0.075→0.062, 배경 폭 0.32→0.27/글자(최대 4.5→3.8)로 줄였다. 긴 채팅 두 개가 이웃하면 가로로는 여전히 겹칠 수 있으나 높이가 달라 읽힌다. |

테스트 보강: `Flow.BriefAll`(Brief 전 TimeLeft==timeLimit·침수 정지 확인 → 4슬롯 Brief → timerRunning → 감소), `Gate1Manual`(페널티 정확히 10, 잠금 중 Digit 거부, 3초 뒤 해제), `Gate2Manual`(오판 페널티 정확히 15), FullFlow(안전 라인 번호, 지름길·avoid 경로). BotFlow는 `briefAll:false`로 봇의 자동 Brief에 기댄다. 투어는 10_stage_* 촬영 뒤 `BriefAll`.

### 14-2. QA 수정 기록

QA 검수(투어 v3 스크린샷·코드 리뷰) 발견 id 별 수정. 담당 1/3(규칙·코드·화면).

- ui-V1: 관리실 방 이름 라벨을 오른쪽 아래 → 오른쪽 위(`yMax−0.45`, UpperRight)로 옮겨 침수 현황판 아이콘에 가리지 않게 함(`StageWorld.Rebuild`).
- ui-V2: 발광 중 담당 오브젝트 이름 라벨을 내 아바타 거리로 페이드 — Reach(1.6) 안이면 숨기고 Reach+0.6 까지 서서히 켠다(`StageWorld.Tick`). 앞에 선 내 역할 배지와 겹치지 않는다.
- ui-V3: 집결 게이지 패널을 380×118(y −70)로 줄여 아래 변(188px)이 셔터 라벨(≈204px) 위에 오게 하고, 클리어 1초 뒤 패널을 내려 셔터 개방·CLEAR 배너만 남긴다(`HudLayout.GatherSize`, `GatherPanel`). `StageWorld.ShutterX0/X1/Bottom/TopWithLabel` 상수 + `Flow.AssertGatherPanelClearOfShutter`(FullFlow·투어)로 회귀 방지.
- ui-V4: 내비 패널 침수 8줄을 2열×4행 격자(66px)로 접어 현황판을 연 뒤에도 조종 전환 줄을 덮지 않게 함(`RolePanel.BuildNavigator`, `_floodGrid`). `RolePanel.Root`/`StageScreen.RolePanelRect` 훅 + `Flow.AssertRolePanelFits`(≤466px)를 역할별 샷·팝업 샷·관문 1 현황판 열림 시점에 검사.
- rule-F1: 라운드·관문 전환 시 색 신고 초기화 — `Stage1Logic.ClearColorReport`를 `DigitConfirm` 성공·`Lever`·`ResetRounds`에서 호출. 통역 벽화 팝업 미확인 문구에 "이번 라운드 관측자 신고를 기다린다". `BotDriver`의 `StaleColorAfter` 우회 제거(`colorFresh = colorReport ≥ 0`). Flow 에 관문 1 해제 뒤·레버 뒤 `colorReport == −1` 단언.
- rule-F3: `StageScreen.OnEvent`에서 `e.target == LocalSlot`인 Color/NumberCard/DirCard 이벤트에 수신자 토스트("관측자: ■노랑", "통역이 보낸 숫자 카드 n — 제어반에 입력!", "통역이 보낸 방향 카드 ▶ — 레버를 그 방향으로!"). 클라 표시만, 로그 중복 없음.
- code-F1: `SessionNet.SpawnAvatars`가 `Session.Avatars`를 사본(List)으로 돌고 딕셔너리를 먼저 비운 뒤 Despawn — 열거 중 Remove(InvalidOperationException) 제거. FullFlow 끝에 "로비로 한 번 더 → 게임 시작" 재진입을 추가해 옛 아바타 디스폰·새 아바타 4개 스폰을 검증.
- code-F2: `TutorialOverlay.Build`가 `retries>0`이면 튜토리얼을 띄우지 않는다(서버는 이미 `StartTimer(quiet)`). 호스트 테스트 명령 `ForceFail` 추가, `Flow.RetrySkipsTutorial`(AutoSkip=false 로 재시도 → 오버레이 없음·입력 잠금 없음·타이머 즉시)을 FullFlow 관문 1 뒤에 호출.
- code-F3: `ScreenManager.OnState` 재진입 가드(`_building`/`_pending`) + `_key` 선갱신 — Build 도중 호스트 루프백으로 같은 화면이 두 번 만들어지지 않는다. `StageScreen.Build`는 `LocalSlotChanged` 구독을 맨 앞으로. `TutorialOverlay`는 Build 중 `Send("Brief")` 대신 첫 Tick 에서 보낸다(`_briefPending`).
- code-F4: `AvatarNet.Update`가 조종하지 않는 아바타 중 `players[slot].isBot`인 슬롯만 `BotBrain.Decide`로 움직이고, `BotBrain.Decide`도 사람 슬롯이면 정지. BotFlow 에 `SoloWithBots_HumanAvatarStaysStillWhileHostControlsBot`(호스트가 통역 봇을 조종하는 6초 동안 슬롯 0 이동 < 0.5) 추가.
- orch-E1: 바닥 방 이름 라벨은 `LocalSlot`이 내비게이터·통역일 때만 표시(`StageWorld.RefreshProps`, 슬롯 전환 시 갱신). 조작자·관측자는 미니맵 "?"·핑 D5 와 같은 규칙.
- orch-E2: `GameLogic.Apply`가 `timerRunning=false`(브리핑 게이트) 동안 Digit/DigitClear/DigitConfirm/Color/Card/DirCard/Lever/Judge 를 거부하고 토스트 "아직 준비 중 — 모두 [시작]을 누르면 시작해요"(`IsGateCommand`). Chat/Signal/Ping/OpenBoard/Brief/이동 허용. `BotDriver.Tick`은 자동 Brief 뒤 timerRunning 전이면 return. `Flow.BriefAll`에 게이트 중 Digit 거부 → 해제 뒤 허용 단언.
- ui-V5(사소): 조종 슬롯 전환 시 토스트를 지우고 모든 아바타의 말풍선·카드를 지운다(`StageScreen.OnLocalSlot`, `AvatarView.ClearSpeech`).
- ui-V6(사소): 집결 원 라벨(PHL/도착)을 원 아래 가장자리(`c.y − GatherRadius − 0.15`, UpperCenter)로 내림.
- ui-V8(사소): 비주얼노벨 상단 장면 태그 뒤에 반투명 검정 띠(360×28, α0.6)를 깔고 글자를 앰버로.
- ui-V9(사소): 아트리움 방 이름 라벨을 왼쪽 위(`yMax−0.45`)로 올려 스폰 줄과 겹치지 않게.
- ui-V11(사소): 내비 패널 휠 버튼을 "go 가자!"처럼 영문 + 한국어(`Comms.SignalKorean`)로 통일.
- ui-V12(사소): 조작자 화면의 액션 열 첫 줄을 "말하기 T · 통역만 읽어요"로 바꾸고(`ActionBar.SetOperatorMode`), 누르면 토스트 + 채팅 입력칸 포커스.
- ui-V14(사소): 도넛 캡션 뒤에 크림색 반투명 패널(620×30, α0.85)을 깔고 글자를 어두운 색으로.
- rule-F5(사소): 서버 `GameLogic.Apply`가 조작자(슬롯 0)의 Signal 을 거부하고 토스트 "조작자는 휠을 쓸 수 없어요 — 채팅으로".
- rule-F6(사소): `Popups.Open`과 `StageScreen.OpenPopup`이 `Comms.PropSlot(id) != LocalSlot`이면 열지 않는다(담당자 한정 이중 방어).
- (추가 발견, code-F1 검증 중) 게임 끝에 호스트가 봇 자리를 보고 있으면 로비 복귀 뒤에도 `LocalSlot`이 그 봇 자리에 남아 다음 판의 이름 설정(`Name`)이 봇 슬롯으로 가고 서버 `hostControlSlot`도 남아 그 봇이 다음 판에서 멈춘다 → `Session.ApplyState`가 로비·이름·준비 단계에서 `LocalSlot=MySlot`으로 되돌리고(`LocalSlotChanged`), `GameLogic.BackToLobby`가 `hostControlSlot=-1`. FullFlow 로비 복귀 단언 추가.
- 고치지 않음: ui-V7(말풍선 기준점·두 줄 접기 — AvatarView 배치 전반 조정 필요), ui-V10(비담당 회색 텍스처 — 스프라이트 작업 필요), ui-V13(로그 패널 α는 이미 0.96·페이드 없음, 촬영 시점 문제로 판단).
- 검증: 빠른 컴파일·Unity 컴파일(qa1_c1) 무오류, FullFlowTests(qa1_ff3) Passed 18.9초, BotFlowTests 2건(qa1_bf2) Passed 54.4초. ScreenshotTour 는 담당 3/3 이 재촬영한다.

담당 2/3(문구·UX). 용어 통일: 플레이어에게 보이는 문구는 "도넛"(6칸 신호 UI — 액션 열 "Q · 도넛 열기"·코치마크·D4 토스트가 이미 도넛이고, 기획서의 "상호작용 휠"은 액션 열 "상호작용 E"와 헷갈려 쓰지 않음; 코드 주석·식별자의 "휠 신호"는 그대로), 최종 목적지는 "집결 지점(중앙 분수)"(기획서 "최종 집결"), 위험 신호는 어디서나 "위험", 감점은 "−n초", 이름 설정 버튼은 [확인]/[뒤로]/[준비 취소].
- copy-C01: "휠"→"도넛" — `TutorialOverlay.Can`(내비·관측자 3번째 줄), `LogPanel` 빈 상태 문구, `Popups` 현황판 안내, `StageGuide`(관문 1 내비 3번째·관문 2 내비 힌트), `GameLogic` 조작자 Signal 거부 토스트("조작자는 도넛을 쓸 수 없어요 — 채팅으로"). 내비 첫 힌트는 실제 해제 조건(핑)에 맞춰 "[여기로!] 버튼으로 친구들에게 알려주세요".
- copy-C02: 관측자·내비게이터 패널 머리글 우측 "휠 전용" → "도넛 · 채팅(통역만 읽음)"(`RolePanel.BuildObserver/BuildNavigator`, 위 7-2 RolePanel 항목 갱신). 기획서 3절(전원 채팅 입력 가능)·13-3과 일치.
- copy-C03: 위험 신호 표기를 한 이름으로 — `Comms.SignalWord("DANGER")`="위험", `SignalKorean("DANGER")`=""(부제 생략), `Comms.SignalCaption(id, subSize, bold)` 추가해 도넛 칸·내비 패널 버튼이 같은 문자열을 쓰고, `LogText` Signal 분기도 빈 부제를 생략. `AvatarView.CardWord`는 `Comms.SignalWord`에 위임(표를 한곳에).
- copy-C04: 문구는 그대로("수신 로그  Q · 도넛 열기"), `ActionBar.Item` 라벨 버튼 폭 150→176 + `horizontalOverflow=Overflow`로 한 줄 고정(열 오른쪽 끝 244px < ActionSize.x 250).
- copy-C08(사소): 배전함 판정 안내를 "'안전'이 틀리면 −15초를 잃고 라운드를 처음부터 다시 해요. '위험'은 시간을 잃지 않고 처음부터. 도착한 라인 번호가 현황판의 안전 라인 번호와 같은지 비교하자."로(리셋·감점·대조 제거).
- copy-C09(사소): "탈출 지점"→"집결 지점(중앙 분수)" — 미니맵 라벨·범례("○ 집결 지점"), 역할 팝업 마지막 줄, 오프닝 시스템 줄; 준비 화면 "4명 전원이 중앙 분수에 함께 모여야 클리어됩니다"(위 화면 표·8-1 갱신).
- copy-C10(사소): 관문 1 클리어 토스트 "…이제부터는 스스로 해보세요!"(기획서 원문, 8-1 갱신).
- copy-C11(사소): 수신 로그 빈 상태 문구를 역할별로 — 조작자 "아직 받은 신호가 없어요. T를 눌러 채팅으로 말해 보세요 (통역만 읽어요).", 나머지 "…Q를 눌러 도넛으로 알리거나, 아래 채팅으로 말해 보세요."
- copy-C12(사소): 벽화 매뉴얼 점 번호 설명 10px→12px(높이 30) "점 번호: 왼쪽 줄 위→아래 1·2·3, 오른쪽 줄 4·5·6. …".
- copy-C13(사소): 코치마크 5단계 "네 명 모두 [시작]을 누르면(늦어도 30초 뒤에는 저절로) 물이 차오르기 시작한다…"(D1 자동 시작 안내).
- copy-C14(사소): 오프닝 시스템 줄 "…물이 차오르고 있어요 — …클리어됩니다.", 스테이지 선택 잠금 토스트 "…Stage 1을 먼저 클리어해요"(존댓말 통일).
- copy-C15(사소): 재시도 토스트 "(재시도 n회)".
- copy-C16(사소): 감점 토스트 `GameLogic.Penalty` "(−n초)"(수학 마이너스, 팝업 문구와 동일).
- copy-C17(사소): 이름 설정 버튼 "CONFIRM"/"BACK"/"취소" → "확인"/"뒤로"/"준비 취소", 안내 "이름을 정하고 [확인]을 누르세요", 서버 토스트 "이름을 정하고 [확인]을 눌러주세요 — 국적과 역할은 이미 정해져 있어요"(위 화면 표 Name 행 갱신).
- copy-C18(사소): 로비 시작 실패 토스트 "4명이 모여야 시작할 수 있어요 — [빈 자리 봇으로 채우기]를 눌러도 돼요".
- copy-C19(사소): 조작자 힌트 "통역이 보낸 숫자 n — 제어반에 입력하고 확인!"; 담당 오브젝트 접근 힌트(3역할 공통 `StageGuide.Approach`) "이 오브젝트는 당신에게만 빛나요. 가까이 가서 E를 누르거나 클릭해 보세요."
- copy-C20(사소): ui-V1 에서 이미 오른쪽 위로 옮겨짐 — 투어 `10_stage_*`로 재확인만.
- 검증(담당 2/3): 빠른 컴파일·Unity 컴파일(qa2_c1) 무오류, ScreenshotTour(qa2_tour3) Passed 2건, FullFlowTests(qa2_ff1) Passed 18.5초, BotFlowTests(qa2_bf1) Passed 54.9초. 06_names·07_ready·09_coachmark_1·10_stage_2na/3ob/0op·11_donut_ob·12_log·23_popup_board_na·24_popup_panel_done_op·31_gate2_mural_in·33_gate2_junction_na 에서 글자 넘침·잘림 없음(배전함 안내 Note 는 4줄이라 높이 56→70).

담당 3/3(테스트 보강). 새 EditMode 어셈블리 `Assets/Tests/EditMode/Paradox.EditModeTests`(순수 함수 + `GameLogic` 헤드리스 하네스 `GameLogicHeadlessTests.Headless`: Emit 으로 이벤트 수집, GetPos 를 Teleport 이벤트로 갱신, `Clock` 을 직접 진행 — 30초·12분을 실제로 기다리지 않는다). 실행 `run_tests.ps1 -Platform EditMode -Filter Paradox.EditModeTests`.
- test-T1: 시간 초과 → 실패 → 재시도 경로. PlayMode `Flow.RetrySkipsTutorial`이 ForceFail 대신 호스트 권한 `penalty=timeLimit`로 `GameLogic.Tick`의 실제 시간 초과 판정(Fail)을 태우고, 실패 중 Ping 거부·Retry 뒤 체크포인트 1(step 1·gate1Done·gate2 초기화·round/gate2Attempts 0)·시드 +17·Stats.retries==St.retries·penalty 0·moveMode 1·타이머 즉시·아트리움 스폰·다시 12분을 단언. EditMode `Timeout_Fails_ThenRetryResumesFromCheckpoint1_WithNewSeedAndImmediateTimer`(실패 전 Retry 무시, playSeconds 누적, Teleport 4건, 체크포인트 1 맵 문 개방) + `ForceFail_BeforeGate1_RetryRestartsFromCheckpoint0`(ForceFail 훅은 여기서 쓴다).
- test-T2: 위험 구역. `AvatarNet.CurrentSpeed` 테스트 훅 추가(이번 프레임 속도). FullFlow 관문 1 뒤 수족관방 floodBase=95 → 관측자 텔레포트 → InDanger·속도 ×0.5·다른 방은 아님·경고 토스트 1초 안에 1회, 두 번째는 5초 간격(서버 클록, 히치 여유 0.5초)·감점 없음·나오면 해제. EditMode `Stage1RuleTests.IsDanger_OnlyRooms4And5_AtOrAbove70`(방 −1~7 진리표, 경계 70, 타이머 전 고정) + `Flood_ClampsAndFreezesBeforeTimer`.
- test-T3: `Flow.Gate2Rounds`(FullFlow 에서만, shot==null) 라운드 1 시작 시 카드 없는 레버 거부, 라운드 2 시작 시 4라운드 전 '안전' 판정 무시(페널티·리셋·chosenDir 불변); `Gate2Manual` 4라운드 뒤 DirCard·Lever 거부(경로 불변). EditMode `Gate2_LeverNeedsCard_JudgeNeedsFourRounds_DangerResetIsFree_AfterFourRoundsCardsRejected`.
- test-T4: `Gate2Rounds(stopAfter)` 인자 추가. `Gate2Manual(wrongFirst)`가 오판 리셋 뒤 1라운드만 진행하고 Judge 0 → 감점 0·gate2Attempts+1·RoundReset 1회·Judge(a=0) 1회·카드/색 신고/chosenDir 초기화·새 시드(퍼즐 다름)·ValidateLadder, round==0 에서 Judge 0 무시. EditMode 동일 검증 + "위험 재시도는 오답이 아니다".
- test-T5: `Gate1Manual` 앞부분에 역할·근접·단계 부정 검증 — 조작자 Color/Card/Ping, 멀리서 Color/Card/OpenBoard(투어는 이미 열려 있어 '변하지 않음'), 범위 밖 Ping, 통역 Digit, 관문 1 전 DirCard/Lever/Judge(페널티 없음), 빈 입력 DigitConfirm(오답·페널티 아님), DigitClear; 끝부분에 관문 1 뒤 Card·경고등 Color 거부. FullFlow·투어 공용.
- test-T6: FullFlow 관문 1 전 `LockedDoor3` 가운데·위아래 Walkable=false, 스폰→레버반 FindPath=null; 관문 1 뒤 맵 재생성(Walkable) → 경로가 모자이크방 문을 지남; 재시도(체크포인트 1) 맵도 개방. EditMode 는 체크포인트 0/1 맵의 문 상태를 함께 확인.
- test-T7: EditMode `CommsTests`(CanUnderstand 16조합, Garble 원문 글자 없음/빈·null·공백 → "???", ChatText, LogText Chat/Color/NumberCard/DirCard/Ping(D5), 위험 신호 표기, `Comms` 표시용 표 == `Stage1Logic` 표). `AvatarNet` 말풍선의 private Garble 사본을 삭제하고 `Comms.Garble` 사용. PlayMode `Gate1Manual`이 수신 로그의 실제 Color/Chat 이벤트로 `Comms.LogText` 를 viewer 별 검증(`Flow.LastLogged`).
- test-T8: `Flow.PingAndVerify(room, what)` — Ping 이벤트 slot=2·a=room·pos=`StageMaps.RoomDoor(room)`·lastPingClock 갱신을 관문 1(방 1)·관문 2(방 3)에서 검증, 관문 1 해제 뒤 lastPingRoom==−1 단언.
- test-T9: `Flow.StatValue(label)`(결과 화면 Row 라벨 → 값). FullFlow 결과: chats 10·cards 10·wrongInputs 2·signals 0·retries 1(St==Stats)·playSeconds = 실패 구간 + 재시도 구간·화면 "2회/10장/10줄/1회/총 시간". 투어: signals 2·chats 7·cards 5·wrongInputs 1·retries 0 + 화면 표시.
- test-T12: `Flow.BriefAll` 루프에서 n/4(n<4) 상태 타이머 정지·`BriefCount`, 시작 뒤 중복 Brief 로 startClock·인원 불변. EditMode `BriefGate_StopsUntilAllPresent_AutoStartsAfter30s_IgnoresDuplicates`(3/4 정지·게이트 중 Digit 거부/Ping 허용·29.5초 정지·30초 자동 시작·TimerStart 1회·늦은 Brief 가 startClock 을 안 바꿈).
- test-T13: `Flow.GatherNegative`(FullFlow, GatherAll 앞) — 3명 1.2초 클리어·allInSince 없음·GatherCount 3 → 4명 allInSince 시작(호스트 권한 상태로 판정) → 1명 이탈 시 리셋. EditMode `Gather_NeedsAllFourInsideForHoldTime_LeavingResets`(0.3초 뒤 미클리어, 이탈 리셋, 0.6초 유지 → 클리어 → 3.2초 뒤 컷씬 → Result → Again).
- test-T14: BotFlow 상한을 `MaxPasses=15`((4라운드 + 판정 1) × 3시도)로 두고 문구를 맞춤.
- 추가(EditMode `Stage1RuleTests`): 시드 1~300 사다리 규칙·정답 경로 제자리 없음·표 일치, 점자 왕복(`DigitOfDots`), 키패드 잠금·집결 원 도우미.

담당(2차 QA · 서버 규칙·팝업). 새 필드: `StageState.lastWrongDigit`(−1) · `StageState.pingNudged` · `StageState.timeWarned`(비트 1/2/4 = 6:00/3:00/1:00). `SessionState.hostSlot`은 기존(SessionNet 접속 시 채움). 문구는 `Stage1Logic` 상수/함수(`RoundStartText`·`WrongDigitText`·`ResetOperatorText`·`ResetObserverText`·`PingNudgeNavText`·`PingNudgeOthersText`·`TimeWarnText`)로 팝업·테스트가 같은 문자열을 쓴다.
- A2: 벽보·벽화 팝업 "지금 받은 정보" 칸에 `Session.Log`의 최신 조작자 Chat 원문 + "n초 전"(`Popups.OpChatLine`) — 구조화 a=1 이면 "홀수"/"짝수"를 굵게, 그 외 원문을 굵게. 새 채팅은 `OnEvent`의 기존 `_refresh`, 시간 표기는 `_tick`(30프레임마다). 벽보 Info 70→92, 벽화 Info 92→104.
- B1: `Stage1Logic.Lever`가 round++ 뒤 다음 라운드가 있으면 관측자 타깃 토스트 "라운드 n 시작 — 색이 같아도 다시 신고!"; 색상등 팝업 문구 "라운드가 바뀌면(색이 같아도) 다시 신고해야 한다."
- B2: 관문 1 오답 시 `lastWrongDigit=v` + 통역 타깃 토스트 "조작자가 n을 넣었는데 틀렸어요 — 색·홀짝을 다시 확인"; 정답·`Card` 갱신 시 −1. 벽보 팝업 status(높이 30→44)에 같은 문구(빨강).
- B6: `ResetRounds`(Judge 0·안전 오판 공통)에서 조작자 "새 패턴이에요 — 점자를 다시 보내세요", 관측자 "색을 다시 신고" 타깃 토스트.
- A3: `Stage1Logic.Tick` step 0 — 타이머 시작 40초(`PingNudgeAfter`) 뒤 `lastPingRoom<0`이면 한 번만(`pingNudged`) 내비 "친구들이 기다려요 — [여기로!]로 안내데스크를 알려주세요", 나머지 "안내데스크 쪽으로 가 보세요".
- C4: `Stage1Logic.TickTimeWarnings` — 남은 시간이 6:00/3:00/1:00 아래로 내려가면 전원 "남은 시간 n분" + `Flash(ToneWarn)` 1회씩(`timeWarned` 비트). 한 틱에 여럿을 지나치면 가장 작은 것만 알린다.
- B-05: `Stage1Logic.Start` 침수 생성 뒤 방 4·5 base/rate 50% 스왑 + 관문 방 1·2·3 rate 시드 순열(아트리움 보정 유지). `Stage1RuleTests.Flood_Order_VariesBySeed_AtriumAlwaysSafest`(시드 1·3 의 70% 도달 순서 다름, 60시드 중 순서 ≥5종, 스왑 양쪽 존재).
- C1: 벽화 표 머리 점 번호 8px→12px — 셀 84→87(간격 2, 오른쪽 열 280→260), 점자 그림은 위 왼쪽, 숫자 쌍은 위 오른쪽, 점 번호는 아래 전체 폭(Overflow).
- C2: 배전함 배선·노드·카드를 앰버 단색으로, 카드엔 방향(18px)·단자 이동만(램프 색은 내비가 모르는 정보).
- S-05: 팝업 딤 알파 0.55→0.85.
- G1-02: 보류 — 제어반 팝업 오른쪽 열(입력 60 + 키패드 208 + 행 44 + 잠금 18 = 360/390)에 장식 블록을 넣을 여유가 없다.
- 테스트: EditMode `GameLogicHeadlessTests` +5(`Gate1Wrong_RecordsLastWrongDigit…`, `Lever_ToastsObserverEachNewRound_ResetToasts…`, `PingNudge_After40sWithoutPing…`, `TimeWarnings_At6_3_1Minutes…`, `TimeWarnings_SkippingSeveralThresholds…`), `Headless.Toasts(target, contains)` 도우미. PlayMode `Flow.Gate1Manual` lastWrongDigit 기록/초기화, `Flow.Gate2Rounds` 레버마다 관측자 라운드 토스트(4라운드 뒤 없음), FullFlow 관문 1 전후·재시도 뒤 `pingNudged=false`·`timeWarned=0`. 기존 단언(signals 2 등) 유지.
- 검증(담당 3/3): 빠른 컴파일 무오류, EditMode(qa3_em1) Passed 27건 0.1초, FullFlowTests(qa3_ff1) Passed 35.3초, BotFlowTests(qa3_bf1) Passed 2건 54.3초, ScreenshotTour(qa3_tour1, -Shots) Passed 2건 36.6초(31장). 테스트 중 드러난 게임 결함 없음(게임 코드 변경은 테스트 훅 `AvatarNet.CurrentSpeed` 와 Garble 중복 제거뿐).

2차 QA(2026-09-18) 담당 "규칙·팝업".
- D-A4: 현황판 "가서 봐야 아는"(기획 6절) — `StageState.boardClock`(마지막 OpenBoard 시각, 기본 −99)을 `Stage1Logic.OpenBoard`가 다시 열 때마다 갱신(early return 제거, 재확인 토스트). 정적 도우미 `Stage1Logic.FloodSnapshot(st, zone)`(= boardOpened ? Flood(st, zone, boardClock) : 0), `IsDangerSnapshot(st, room)`, `SecondsSinceBoard(st, clock)`(열기 전 −1) 추가 — 역할 패널(StagePanels)·미니맵은 이 스냅샷만 본다. 미니맵 위험 방 물결은 `IsDangerSnapshot`(열기 전 물결 없음). 현황판 팝업은 실시간 그대로(열어서 보는 것이 곧 확인)이고 우측 하단에 "확인한 시각: 지금 / n초 전 — 역할 패널·미니맵은 이 시각의 값, 다시 열면 갱신" 표기. 봇의 침수 회피(`BotBrain.DangerRects`)는 실제 값 유지(규칙 준수 검증 대상 아님). EditMode `BoardSnapshot_FrozenAtBoardClock_RefreshesOnReopen_ZeroBeforeOpen`(멀리서·다른 역할 거부, 첫 열기 스냅샷 = 그 시각 실시간, 20초 뒤 실시간↑·스냅샷 고정, 재열기 갱신, 실시간 위험이어도 스냅샷은 안전 → 다시 보면 위험, 관문 통과가 스냅샷을 바꾸지 않음) + `Stage1RuleTests.FloodSnapshot_UsesBoardClock_ZeroBeforeOpen_BaseIfOpenedBeforeTimer`(null/열기 전/범위 밖/브리핑 게이트 중 열면 기본값).
- D-A5: 사람 내비의 배전함 "위험 — 처음부터"도 팀에게 몸짓 카드로 — `Stage1Logic.Judge(a=0)` 성공 경로가 `GameLogic.Signal`(private → public, 통계·이벤트·봇 훅 동일 경로)로 `Signal DANGER` → `Signal NO`(내비 슬롯)를 순서대로 발행한 뒤 Judge/RoundReset. `BotDriver`의 Judge 0 전 DANGER/NO 2단계(`_navJudgeStep`)를 제거해 중복 방지. 통계: FullFlow signals 0 → 2(`Flow.Gate2Manual`이 카드 순서·signals+2·round==0 에서는 카드 없음을 단언), 투어는 wrongFirst:false 라 Judge 0 이 없어 2 그대로. EditMode `Judge0_ServerEmitsDangerThenNoSignal_ForTeam_CountedOnce`(멀리서·4라운드 전 안전은 신호 없음, DANGER→NO→Judge→RoundReset 순서, signals 2, round==0 무시).
- P-A2: `Popups.BuildMural` 우측 "지금 받은 정보" 블록 높이 70 → 92(머리글 + 램프 + 점자 3줄이 넘쳤다).
- P-A3: 팝업의 가로 행이 열(VStack)의 남는 높이를 받아 세로로 늘어나던 것 — `Popups.Row`(HudKit.Row + `UI.LE(rt, -1, h, -1, 0f)`, LayoutElement 우선순위로 flexibleHeight 0)로 모든 행(제어반 홀짝·지우기/확인, 현황판 8줄·라인·go/위험, 레버반 칩, 벽화 방향, 배전함 라운드 카드)을 고정 높이로. 현황판 go/위험은 PDF s26 처럼 큰 버튼 두 개 나란히 100px(글자 24).
- P-A10: 팝업 카드 780×470 고정 → `Popups.CardHeight(id)`: lamp/colorlamp 380, 나머지(board/panel/lever/poster/mural/junction) 470, "완료 ✓" 상태 300(`DoneCardH`, 완료 박스 160). `Open`/`ShowDone`에서 `_card.sizeDelta` 갱신 — 카드 피벗이 중앙이라 위치는 그대로.

사용자 보고(2026-09-18) — 비주얼노벨(프롤로그·오프닝·클리어) 화면에서 클릭·SKIP 이 안 되고 Enter/Space 만 됨:
- 원인: `VnView.Tick` 장면 전환 페이드의 마지막 프레임에서 알파가 정확히 0이 되면 "알파 > 0 이면 끄기" 분기에 못 들어가 투명한 `Fade` 이미지가 `raycastTarget=true` 로 남아 모든 클릭(대사창·SKIP·다음)을 가로챘다. 키보드는 레이캐스트와 무관해 정상.
- 수정: 페이드 종료 조건에 `raycastTarget` 을 포함해 반드시 해제. 추가로 화면 클릭 진행을 레이캐스트(ClickCatcher 버튼)에 기대지 않고 `Mouse.current.leftButton` 을 직접 읽어 버튼 위가 아니면 `Advance()` 하도록 바꿈(대사창 위 클릭도 진행).
- 검증: `Assets/Tests/PlayMode/InputTests.cs` — 가상 마우스 이벤트(Input System `QueueStateEvent`)로 타이틀 [시작하기] 클릭 → 프롤로그, 프롤로그 화면 가운데·대사창 클릭 → 다음 줄, SKIP 클릭 → 스테이지 선택. 배치 모드에서는 `editorInputBehaviorInPlayMode=AllDeviceInputAlwaysGoesToGameView`·`backgroundBehavior=IgnoreFocus` 로 장치 이벤트가 게임에 닿게 한다. 테스트 훅 `PrologueScreen.Instance/Index/Typing`.

감사(2026-09-18) 반영 — 담당 "스토리·오디오·VN 화면". `StoryLine` 새 필드: `blackout`(정전 암전), `sfx`(줄 진입 효과음, 쉼표로 차례 재생), `portraitRight`(우측 초상 슬롯, -1 없음), `icon`(SpriteLib 아이콘 오버레이), `direction`(지문).
- D-A1: `Core/Sfx.cs` — 소리 파일 없이 프로시저럴 `AudioClip`(alarm 0.5초 사각파 440/330 버저 2회 · siren 2.5초 사인 600↔900 · wind 2초 노이즈+저역+느린 진폭 변조 · thud 0.35초 60Hz 감쇠+클릭 · clear 도미솔도 0.9초 · click 0.05초 · chime 0.25초). 전용 GameObject(DontDestroyOnLoad)에 AudioSource 2개(효과/루프), `Sfx.Play(id, volume)`·`Sfx.PlayLoop`·`Sfx.Stop`, 전부 try/catch 무음 가드. 음량은 기존 `Progress.volume → AudioListener.volume` 그대로(설정 슬라이더가 이제 들린다 — 움직일 때 "click", 0.08초 간격). 훅: `ScreenManager.OnEvent` — "Alarm"·Flash(ToneBad) → alarm(0.3초 안 중복 1회), "Clear" → clear, NumberCard/DirCard/Color 수신자(target==LocalSlot) → chime, "Signal" → click. 버튼 클릭음 없음. `Resources/Sfx/{id}.wav` 가 있으면 우선(10절).
- D-A2: 오프닝 씬 3 — 강풍·사이렌 줄은 정전 전이므로 배경 `camp` 유지 + `sfx="wind,siren"`; '쿵' 줄은 `blackout=true, bg=museum_dark, sfx="thud"` → `VnView.Show` 가 1.1초 완전 암전(`_holdT`, 자막 없음) 뒤 `museum_dark` 를 그리고 걷힘(`_fadeT` 재사용), 같은 순간 `FeedbackFX.Shake(0.6)`. 타자는 배경이 드러난 뒤 시작, 암전 중 입력 무시. 다음 줄("아무것도 안 보이는데")은 dark 오버레이 대신 `museum_dark` 유지. 훅 `CutsceneScreen.Instance.BlackoutActive`.
- D-A3: 씬 4 두 줄(마리아가 안내문을 읽는 내레이션 · "쟤가 뭘 읽고 있는 것 같은데")에 `portraitRight=1`(마리아·통역 픽셀 초상) + `icon="poster"`(우측 초상 위 120×120 슬롯에 `SpriteLib.Icon("poster")`), 배경 `museum_dark`. 씬 1 "손을 흔들며 인사" 줄은 `group=true` → 4인 `SetTeamRow`(줄 단위로 켜고 끔). 클리어 컷씬은 `SetTeamRow(true, sticky:true)` 로 내내 유지(변경 없음).
- D-A6: 스태프 자막을 기획서 원문 "오늘부터 팀별로 미션을 수행합니다." 로 되돌리고, "첫 목적지는 박물관입니다. 팀원끼리 서로 도우세요." 는 같은 `MaskAllButOp` 의 다음 줄로 분리(오프닝 줄 수 +1, 투어는 `Flow.BarrierLineIndex`·`StoryData.FindLine` 으로 찾으므로 영향 없음).
- D-A9: 프롤로그 씬 2 태그 "조별 과제" → "다음 날 · 조별 과제".
- D-A10: 지호 줄 "(어색하게 웃으며)" 를 본문에서 빼 `direction` 으로 — `VnView` 가 이름표 아래 14px 회색 이탤릭 "(어색하게 웃으며)" 로 그리고 본문을 16px 내린다(장벽 줄에서는 숨김). 결과 화면 내레이션은 `StoryData.ClearInnerText(1)`(s1_clear 의 속마음 줄)에서 가져와 단일 출처.
- P-A11: `VnView` 초상 슬롯 — 화자(플레이어·NPC 모두)는 좌측 슬롯(PDF s07/s08), 대사창(1060px 가운데)은 좌측 슬롯 오른쪽(262px)부터. 우측 슬롯은 `portraitRight` 가 있을 때만(D-A3). 플레이어·NPC 동시 등장 없음.
- P-A4: 준비 화면 "✓ 완료" 배지 92×30 알약 고정 — `UI.LE(badgeRow, -1, 32, 0, 0, -1, 32)` + HStack `childForceExpandHeight=false`.
- P-A5: 커튼 금색 띠(위에서 60~68px)와 겹치지 않게 이름 설정 제목 y −30→−76(부제 −72→−118), 준비 화면 제목 −34→−80.
- P-A13: `ScreenManager.ShowToast` 큐 상한 `MaxToasts=2`(넘치면 가장 오래된 것 제거). ToneGood 이면서 "잠금 해제" 를 담은 토스트가 오면 남아 있는 "키패드 잠금 중" 토스트를 즉시 제거. 훅 `ToastCount`.
- P-A7: 투어 `CaptureCoachmark` — 첫 오버레이는 역할 팝업이므로 `09_role_popup`, 이어서 `TutorialOverlay.Instance.ShowCoachmark(0)`/`(4)` → `09_coachmark_1`/`09_coachmark_5`(마지막에 AutoSkip=true 로 닫음). 본 투어에 오프닝 `08_opening_3_blackout`(정전 줄까지 `NextUntilLine` → 암전 종료 대기 1.4초)·`08_opening_4_poster`(초상+벽보 줄) 추가. 줄 번호는 `StoryData.FindLine` 으로 찾는다.
- 검증(담당 스토리·오디오·VN): 빠른 컴파일(main·tests) 무오류. Unity 실행은 통합 담당이 한다(투어 재촬영으로 08_opening_3/4·09_* 3장·07_ready 배지·06_names 제목 확인 필요).

담당 HUD·월드·아이콘(아트 A). 파일: `Core/SpriteLib.cs`, `Client/StageWorld.cs`, `Client/StagePanels.cs`, `Client/Donut.cs`, `Client/AvatarView.cs`, `Client/Comms.cs`, `Client/LogPanel.cs`. 새 `SpriteLib.Icon` 키: `unknown`(비담당 "?" 상자) · `danger` · `you` · `go` · `where` · `yes` · `no`(휠 신호) · `donut` · `list` · `hand` · `zoom` · `chat`(액션 열).
- P-A1: 비담당 오브젝트는 회색 틴트 대신 슬레이트 "?" 상자(`SpriteLib.Icon("unknown")`, 크기 `StageWorld.UnknownScale` 1.1 — 종류를 드러내지 않게 모두 같은 크기). `StageWorld.UpdateGlow` 가 `prop.slot == LocalSlot` 이면 원래 아이콘(발광 흰 / 비발광 회색), 아니면 "?" 상자. `StageWorld` 가 `Session.LocalSlotChanged` 를 직접 구독(Build/Dispose 짝)해 호스트 조종 슬롯 전환 시 갱신. 표지판·분수·셔터·비콘은 그대로. 훅 `StageWorld.IsUnknownBox(id)`.
- D-A7/P-A8: `Comms.SignalIconKind(id)`(DANGER→"danger", YOU→"you", 나머지 null) 한 곳에서 판단. 도넛 배지(`Donut.BuildSlot`, 24px `HudKit.IconImage`), 내비 패널 위험·you 버튼 오른쪽 위 14px 아이콘, 아바타 신호 카드(`AvatarView._cardIcon` SpriteRenderer, 글리프 TextMesh 대신), 수신 로그 항목(18px `HudKit.IconCell` + `Comms.LogText(e, viewer, withGlyph:false)` 단어만). yes/no/where/go 는 글리프 유지(스프라이트는 준비만).
- P-A9: `ActionBar.Item` 의 ○ ■ ! □ 글자 대신 44px 버튼 가운데 28px 픽셀 픽토그램(donut/list/hand/zoom, `HudKit.IconImage`). 조작자 모드(`SetOperatorMode`)는 첫 줄을 chat 아이콘으로, 열림 강조(`SetActive`)는 앰버 버튼 위 어두운 잉크로.
- D-A8: 상단 바 "현재 관문" 캡션 줄 오른쪽(x 452~604)에 라운드 4칸 서브 게이지(`TopBar._roundBars`, 18×7) + "라운드 r/4"/"판독 대기" — `gate1Done && !gate2Done` 동안 항상 표시(끝난 라운드 앰버, 진행 중 깜빡임, 남은 칸 테두리색). 진행 문구("이동 구간 — 침수 현황판" / 모자이크방 안 "· 라운드 r/4")는 그대로. 훅 `TopBar.RoundGaugeVisible`.
- D-A4: 내비 역할 패널 침수 8칸은 매 프레임 실시간이 아니라 `Stage1Logic.FloodSnapshot(st, z)`(boardClock 시점) — `boardClock` 이 바뀔 때만 다시 칠하고(`_floodShownClock`), 캡션은 "침수 현황판 · n초 전 확인  최신은 현황판에서"(`Stage1Logic.SecondsSinceBoard` + `Comms.Ago`). 열기 전엔 기존 힌트 "현황판(관리실)에서 확인해야 값이 보여요". 팝업(`Popups`)·월드 물 오버레이(`StageWorld`)는 실시간 유지.
- P-A12: 집결 원 라벨을 원 위쪽(`c.y + GatherRadius + 0.15`, LowerCenter) 한 줄("<b>PHL</b> 도착")로 — 원 아래에 두면 옆 원 아바타의 이름 배지와 겹쳤다. 아바타가 원 한가운데 서면 자기 배지(y +1.25~1.6)가 라벨을 가릴 수 있음(같은 역할색이라 허용, 정렬 −989 < 배지 39).
- P-A6: 수신 로그 패널을 `HudKit.PanelSolid` 불투명으로. 항목은 코드 배지 28px + "역할 · n초 전"(10px) + 본문(13px, 줄바꿈) 세로 묶음으로 높이를 내용에 맞춤. 70px 색 막대의 원인: `HudKit.Row`(HStack, `childForceExpandHeight=true`)가 flexibleHeight ≥ 1 을 보고해 세로 목록(`_list`)의 남는 330px 를 4줄이 나눠 가졌고 배지가 줄 높이만큼 늘어난 것 → 항목 `LayoutElement.flexibleHeight=0` + HStack `childForceExpandHeight=false`.
- 검증(담당 아트 A): 빠른 컴파일(main·tests) 무오류. Unity 실행·투어 재촬영은 통합 담당(10_stage_*·11_donut_ob·12_log·40_gather·상단 바 관문 2 구간 확인 필요).

담당 안내·HUD·월드(초등 고학년 4인 첫 플레이 검수 반영). `StageGuide`·`StagePanels`·`StageWorld`·`MinimapView`·`StageScreen`·`TutorialOverlay`·`LogPanel`만 수정.
- A1: "지금 할 일"이 관문 1 뒤 사라지던 조건(`!gate1Done || failed || cleared`) 제거 — 항상 표시. `StageGuide.Move()`를 살려 이동 구간·관문 2·집결에서 역할당 "· " 접두 한 줄(조작자 "점자 번호를 채팅 → 통역 카드 기다리기 → 레버", 통역 "조작자 채팅 + 관측자 색 → 벽화 표 → 방향 카드", 관측자 "라운드가 바뀌면(색이 같아도) 다시 신고", 내비 "배전함 앞에서 대기"/"배전함 판독 — 안전/위험", 집결 "내 색 원을 따라가 안에 머물기"). 리셋(`gate2Attempts` 증가) 뒤 12초·1라운드 전에는 조작자 "새 패턴 — 점자를 다시 보내세요", 관측자 "색을 다시 신고"(`JustReset`).
- A3: `StageGuide.Gate1` 우선순위를 팝업 열림 > 내 방 안(발광 중) > 핑 여부로 — 핑 전에 방에 들어가도 "이 오브젝트는 당신에게만 빛나요…". 내비는 현황판을 연 뒤 "[여기로!] 버튼으로 안내데스크로 친구들을 보내요". 핑 없이 40초(`st.pingNudged` 또는 클록)면 세 명에게 "안내데스크 쪽으로 가 보세요", 내비에게 "친구들이 기다려요 — [여기로!]…".
- B2: 조작자 안내 — `calcFails>0 && numberCard>=0 && lastWrongDigit==numberCard`면 "그 숫자는 틀렸어요 — 홀짝을 다시 확인하고 통역에게 말하세요"(`StageGuide.WrongCard`).
- B3/S-03: `StageWorld.UpdateGlow` — 내 담당 오브젝트도 내가 그 방에 있거나·그 방이 핑됐거나·발광/완료가 아니면 "?" 상자. 한 번 드러나면 유지(`PropView.revealed`), 완료는 회색 실제 아이콘.
- B4: 상단 바 팀 4칸 — 남의 칸은 이름+역할+"담당 ?", 내 칸만 오브젝트 이름(`TopBar.OnState`).
- B5: 실패 문구 "호스트가 [다시 시도]" → `StageGuide.HostRetryHint()`(호스트 자신 "친구들이 기다려요 — [다시 시도]…", 나머지 "이름(역할)이(가) [다시 시도]…", `hostSlot` 을 모르면 "방장"). 실패 패널 힌트는 `OnState`에서 갱신. 실패 중 Esc→Retry 단축키 제거(버튼만, 라벨의 "(Esc)" 삭제).
- B6: 관문 1 해제 `GateOpen a=1` — 버스트 + 문 위치 비콘 5초(`ShowBeacon(pos, GateOpenBeacon, "문이 열렸어요!")`, 핑 비콘 재사용).
- B7: 통역 카드 패널 — 단계에 맞지 않는 세트 비활성·회색(`interactable=false`, 선택도 해제) + 캡션 "지금은 숫자 카드 차례"/"지금은 방향 카드 차례"/"지금은 보낼 카드가 없어요"(`RolePanel.RefreshCards`, `_cardCaption`).
- B1(클라): 관측자 패널 관문 2 배지 "라운드 n · 신고 필요(깜빡, 색이 같아도 다시)/신고 완료 ✓"(`RolePanel.RefreshObserverBadge`, `colorReport<0` 이면 필요, 판독 대기는 "신고 없음").
- A2(클라): 팝업 열림 중 C 로 수신 로그 토글 허용(`StageScreen.HandleKeys`), 열 때 `LogPanel.BringToFront()`(SetAsLastSibling)로 팝업 위에. 푸터에 "팝업 중에도 C".
- C3: 관측자 관문 1 "색이 바뀌면 다시 신고해요" 제거.
- C4(클라): 타이머 자리 "준비 중 · 시작! n/4" → "모두 [시작] 누르면 / 시작 (n/4)"(11px 두 줄).
- C5: 미니맵 내비 step 0 — 관리실(현황판) 속 빈 ○ 앰버 마커(관문 ! 와 구분, 현황판을 열면 사라짐) + 범례 "○ 현황판".
- S-05/B-01(클라): 팝업 열림 동안 미니맵 숨김(`MinimapView.SetVisible`, `StageScreen.Tick`).
- I-02: 통역 역할 팝업 Can 에 "모두의 채팅 해독 — 다른 사람 채팅은 나에게만 원문", 카드 하단 "내가 쓴 채팅은 모두가 읽어요 — 다른 사람끼리는 서로 못 읽어요". 목록 상자 124→140px.
- G1-12: 발광 중 담당 오브젝트에 내 아바타가 Reach 안이면 프롭 위에 `SpriteLib.Icon("hand")` 작은 손 아이콘이 까딱인다(이름 라벨과 자리를 나눔).
- T-01: 13절 16번 결정 추가.


감사(2026-09-18) 반영 — 담당 "멀티플레이(LAN) 결함". 파일: `Net/NetHub.cs`, `Net/SessionNet.cs`(진단 1줄), `Client/ScreenManager.cs`, `Client/MatchScreen.cs`, `Client/LobbyScreen.cs`, `Client/GameApp.cs`, 새 `Assets/Editor/BuildClient.cs`, 새 `Tests/PlayMode/LanClientTests.cs`, `Docs/README_STRANGER.md`(LAN 준비 절).
- C-1(치명): 런타임 템플릿(세션·아바타)이 `SetActive(false)` 라 클라이언트에서 NGO 의 `Object.Instantiate(prefab)` 복제본도 비활성 → `OnNetworkSpawn` 건너뜀(SessionNet.Instance null·아바타 비활성). `NetHub.TemplateHandler : INetworkPrefabInstanceHandler`(Instantiate = 복제 + `SetActive(true)`, Destroy = `Object.Destroy`)를 두 해시(PAXS/PAXA)에 `NM.PrefabHandler.AddHandler` — 호스트의 직접 생성(`StartHost` 세션, `SpawnAvatar`)도 같은 핸들러 Instantiate 를 거친 뒤 `Spawn`/`SpawnWithOwnership`; `SpawnAvatars` 의 `Despawn(true)` 는 핸들러 Destroy 로 흐른다(동작 동일).
- C-2: `Transport.MaxPayloadSize = NetHub.PayloadSize(64KB)`(호스트·클라 공통, Setup). 진단: `SessionNet.Broadcast` 가 호스트 첫 브로드캐스트 1회 `[SessionNet] StateRpc JSON n 문자 ≈ 2n 바이트 (MaxPayloadSize …)` 로그.
- C-3: `Transport.MaxConnectAttempts = 10`(≈10초). 클라 끊김 처리에서 "접속 콜백을 못 받은 채 끊김 + 이유 없음" 이면 `ConnectFailedMessage`("호스트를 찾지 못했어요 — IP·같은 Wi-Fi·방화벽(UDP 7777)을 확인하세요") + `LastDisconnectWasConnectFailure`; 서버가 보낸 `NM.DisconnectReason` 이 있으면 그 문구. 호스트 `Shutdown` 은 먼저 접속 중인 모든 클라에게 `DisconnectClient(id, "호스트가 방을 닫았어요")` 뒤 `NM.Shutdown`. `ScreenManager.OnDisconnected` 는 접속 실패이고 매칭 화면이면 스테이지 선택으로 튕기지 않고 매칭 화면에 남는다(문구 빨강, `MatchScreen.OnNetStatus` 가 StatusChanged 즉시 반영, 대기 중 "(Esc · 뒤로 = 취소)").
- C-6: `ScreenManager.HandleDevKeys` 첫 줄 `if (!Session.IsHost) return;` — 클라이언트에서 1~4 키가 경고 토스트를 띄우지 않는다.
- C-5: `NearReach`(Game 파일) 와 `Popups`(HUD 담당) 는 이 담당 범위 밖 — 보고만.
- 부수: `LobbyScreen.LocalIPs` 가 `NetworkInterface` 로 켜진 어댑터의 IPv4 를 모으고 가상 어댑터(VMware·VirtualBox·Hyper-V vEthernet·WSL·Tailscale·ZeroTier·Hamachi·Docker·Bluetooth)는 뒤에 "(가상)" 표시(169.254 링크로컬 제외, 실패 시 DNS 방식). 매칭 화면 안내에 "UDP 7777, 호스트 PC 방화벽 허용 필요".
- 검증 준비(코드만): `GameApp` 자동 참가 드라이버(환경변수 `PARADOX_AUTOJOIN=<ip>` 있을 때만 — Match 로 이동 → `StartClient`, 실패 시 재시도, NameSetup 에서 Name("클라")+Ready, StagePlay 에서 Brief + 3초마다 아트리움 안 소유 아바타 이동(`Map.Move`, 방 밖 금지), `PARADOX_AUTOJOIN_LOG` 에 5초마다 phase/MySlot/LocalSlot/SessionNet/아바타 수·활성/Clock). `Assets/Editor/BuildClient.cs`(`Paradox.Editor.BuildClient.Build`, `BuildPipeline.BuildPlayer` Windows64 Development, `PARADOX_BUILD_DIR` 또는 프로젝트 옆 `Build/Client/Paradox.exe`) + 스크래치 `build_client.ps1`. `LanClientTests.RealClient_Joins_SpawnsAvatar_SyncsMovement_AndReadies`(`PARADOX_CLIENT_EXE` 없으면 Ignore, Timeout 180초). 아바타는 `OnGameStarted`(오프닝 진입) 에 스폰되므로 아바타·이동 단언은 StartGame 뒤에 둔다.
- 남김(통합 담당이 직접): `GameLogic.Join` 의 phase 검사(Lobby 외 참가는 −2 → SessionNet 이 "게임이 진행 중이에요" 로 끊음), `Session.ApplyState` 의 version 가드(StateRpc 와 StateToRpc 가 뒤바뀌어 도착해도 옛 상태가 새 상태를 덮지 않게).
