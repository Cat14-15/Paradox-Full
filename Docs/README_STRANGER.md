# STRANGER — Stage 1 (침수된 박물관) 개발 안내

4인 온라인 협동 · 언어 장벽 게임. 이 문서는 현재 빌드(STRANGER v0.1 = Stage 1)의 실행·구조·테스트 안내다.

> **플랫폼: PC · 같은 네트워크(LAN) 전용.** Netcode for GameObjects의 UDP(Unity Transport)를 쓰므로 웹 브라우저(WebGL) 빌드는 범위 밖이다. PDF 유저플로우 타이틀의 "웹 브라우저" 문구와 다른 점.
기획 원문은 `STRANGER_Stage1_기획.md`, PDF 유저플로우 판독은 `STRANGER_UserFlow_노트.md`, 구현 계약은 `STRANGER_Stage1_구현명세.md`.

## 실행

1. Unity 6000.5 이상으로 프로젝트를 열고 `Assets/Scenes/Paradox.unity`(유일한 씬)를 Play.
2. 로고 → 타이틀 **시작하기** → 프롤로그(SKIP 가능) → 스테이지 선택 **PLAY** → 팀 모으기 화면.
   - **방 만들기**: 호스트. 로비에 뜨는 IP를 친구들에게 알려 준다(포트 7777, 같은 Wi-Fi).
   - **참가하기**: 호스트 IP 입력.
   - **혼자 테스트(봇 3명)**: 봇이 나머지 세 역할을 실제처럼 수행한다. 1~4 키(또는 미니맵 아래 조종 전환 버튼)로 봇 자리를 직접 조종해 볼 수 있다.
3. 로비(자리 이동·봇 채우기) → 이름 설정(CONFIRM) → 준비 완료 → Stage 1 오프닝(호스트가 진행) → 플레이 → 클리어 컷씬 → 결과 → 스테이지 선택으로.

### LAN 멀티플레이 준비 (호스트 PC)

- **빌드**: `File > Build Profiles`(Windows, 씬 `Assets/Scenes/Paradox.unity` 하나) → Build. 또는 배치 빌드 스크립트(스크래치 `build_client.ps1`, 아래 "테스트" 참고)가 `Assets/Editor/BuildClient.cs` 의 `Paradox.Editor.BuildClient.Build` 를 `-executeMethod` 로 불러 `Build\Client\Paradox.exe` 를 만든다(출력 폴더는 환경변수 `PARADOX_BUILD_DIR` 로 바꿀 수 있다).
- **방화벽**: 호스트 PC 가 UDP 7777 인바운드를 받아야 한다. 관리자 PowerShell 에서 한 번:
  ```
  netsh advfirewall firewall add rule name="STRANGER UDP 7777" dir=in action=allow protocol=UDP localport=7777
  ```
  (지우기: `netsh advfirewall firewall delete rule name="STRANGER UDP 7777"`). 에디터 Play 로 호스트할 때는 Unity.exe 에 대한 "네트워크 액세스 허용" 창이 뜨면 허용. 공용 네트워크 프로필에서는 규칙이 있어도 막힐 수 있으니 Wi-Fi 를 "개인" 네트워크로.
- **IP**: 로비 상단의 "내 IP" 를 알려 준다. `LobbyScreen.LocalIPs` 는 켜져 있는 어댑터의 IPv4 를 모두 보여 주고, VMware·VirtualBox·Hyper-V(vEthernet)·WSL·Tailscale 같은 **가상 어댑터 IP 는 뒤에 "(가상)"** 을 붙인다 — 친구에게는 앞쪽(Wi-Fi/이더넷) 주소를 알려 줄 것. 목록이 비면 `ipconfig` 의 "무선 LAN 어댑터 Wi-Fi" IPv4 주소.
- **참가 실패**: 잘못된 IP·다른 네트워크·방화벽이면 약 10초 뒤 "호스트를 찾지 못했어요 — IP·같은 Wi-Fi·방화벽(UDP 7777)을 확인하세요" 가 매칭 화면에 남는다(Esc·뒤로 = 대기 취소). 호스트가 방을 닫으면 참가자에게 "호스트가 방을 닫았어요" 토스트와 함께 스테이지 선택으로 돌아간다.
- **한 PC 에서 두 인스턴스 테스트**: 호스트는 에디터 Play(방 만들기), 참가는 빌드 exe 를 창 모드로 띄워 `127.0.0.1` 로 참가:
  ```
  .\Paradox.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720
  ```
  자동 참가 드라이버로도 된다: `$env:PARADOX_AUTOJOIN="127.0.0.1"; .\Paradox.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720` — 시작하자마자 매칭 → 참가 → 이름 "클라"+준비 → 스테이지에서 Brief 뒤 3초마다 아트리움 안에서 조금씩 움직인다(`PARADOX_AUTOJOIN_LOG=<파일>` 이면 5초마다 상태 한 줄 기록). 환경변수가 없으면 아무것도 하지 않는다.
- **로그**: 빌드 플레이어 로그는 `%USERPROFILE%\AppData\LocalLow\<회사명>\Paradox\Player.log`(직전 실행은 `Player-prev.log`). `-logFile <경로>` 로 바꿀 수 있다. 에디터 쪽은 `%LOCALAPPDATA%\Unity\Editor\Editor.log`. 접속 관련 줄은 `[NetHub]`, 상태 크기 진단은 `[SessionNet] StateRpc JSON n 문자`(호스트 첫 브로드캐스트 1회), 자동 참가는 `[AutoJoin]`.

## 조작

| 키 | 동작 |
| --- | --- |
| W A S D / 방향키 | 이동 |
| E 또는 마우스 클릭 | 빛나는 내 담당 오브젝트 조사(팝업). 팝업 중에는 이동 불가 |
| Q | 6칸 도넛(몸짓: yes / no / where / go / you / 위험). **조작자에게는 없다** — 안내 토스트만 뜨고, 채팅(T)으로만 말한다(통역만 읽음) |
| C | 지난 신호(수신 로그) |
| TAB | 미니맵 확대 |
| T / Enter | 채팅 입력 · 전송 |
| Esc | 팝업·도넛 닫기 |
| ? (상단) | 조작 안내 다시 보기 |
| 1~4 | (호스트·혼자 테스트) 봇 자리 조종 전환 |

## 역할과 규칙 요약

| 역할 | 국적 | 담당 오브젝트 | 할 수 있는 것 |
| --- | --- | --- | --- |
| 조작자(도윤) | 한국 | 제어반(관문 1), 레버반(관문 2) | 숫자 입력·레버. 자유 채팅(통역만 읽음). 도넛 없음 — 팝업의 빠른 문구 버튼([홀수/짝수라고 말하기], [통역에게 말하기]) |
| 통역(마리아) | 필리핀 | 벽보 매뉴얼, 벽화 매뉴얼 | 모두의 채팅을 읽고 씀. 숫자 카드(0~9)·방향 카드(▲▼◀▶)를 조작자에게 |
| 내비게이터(린) | 베트남 | 침수 현황판, 배전함 | 전체 지도·침수율. "여기로!" 방 핑 + go/위험 신호. 배전함 판독 |
| 관측자(아이샤) | 남아공 | 경고등, 색상등 | 램프 색을 4버튼으로 통역에게 신고 |

- **채팅**: 전원 입력 가능. 통역이 아닌 사람끼리는 서로 `???`로 보인다. 통역이 쓴 글은 모두 읽는다.
- **오브젝트**: 담당자가 그 방에 들어갔을 때만 담당자 화면에서 발광한다. 팝업은 담당자 화면에만 뜬다.
- **브리핑 게이트**: 스테이지에 들어가면 타이머·침수가 멈춰 있다(상단 "준비 중 · 시작! n/4"). 네 명 모두 튜토리얼의 [시작](또는 건너뛰기·? 시트 닫기)을 누르거나 30초가 지나면 시작한다. 봇은 1.5초 뒤 자동. 재시도는 바로 시작.
- **관문 1 계산 자물쇠**: 조작자가 연산 결과의 홀짝을 채팅(또는 팝업의 [홀수/짝수라고 말하기]) → 관측자가 램프 색 신고 → 통역이 표(홀짝×색)에서 숫자를 찾아 카드 전송 → 조작자가 입력. 오답 −10초 + 키패드 3초 잠금.
- **이동 구간**: 내비게이터가 침수 현황판(관리실)을 보고 팀을 안내. 현황판은 **비상 전원 라인 1~8** 중 건전한 라인(○)도 알려준다 — 배전함 판독의 열쇠. 침수 70% 이상 방(수족관방·연못정원방)에서는 속도가 절반. 수족관방 ↔ 관리실, 연못정원방 ↔ 안내데스크 지름길 통로가 있다(빠르지만 물이 빨리 찬다). 봇은 침수 70% 이상인 방을 피해 간다.
- **관문 2 방향 자물쇠(4라운드)**: 조작자가 점자 셀의 켜진 점 번호를 채팅("점자 1,2,4", 팝업의 [통역에게 말하기]) → 관측자 색 신고 → 통역이 벽화 표(점자×색)에서 방향을 찾아 카드 → 조작자가 레버(카드가 오면 그 방향이 미리 선택된다). 4라운드 뒤 내비게이터가 배전함에서 도착 단자 번호를 읽고 현황판의 안전 라인 번호와 대조해 "안전"(문 개방) 또는 "위험"(처음부터)을 고른다. "안전" 오판은 −15초.
- **팝업**: 담당 미션이 끝나면 팝업이 "완료 ✓"로 바뀌고 3초 뒤 스스로 닫힌다(오답·리셋에는 닫히지 않는다). 내비게이터의 "여기로!" 핑은 다른 사람에게 방 이름 없이 "→ 여기로!" + 방 입구 비콘 + 미니맵 핀으로만 보인다.
- **최종 집결**: 분수 주위를 천천히 도는 자기 색 원 4개에 네 명이 동시에 들어가면 클리어.
- **시간**: 12분. 0이 되면 마지막 관문(체크포인트)부터 12분으로 다시.

## 아트 넣기

- 캐릭터: `Assets/Art/Characters/{korean,phil,vietnam,southafrica}.png` (160×256, 32×64칸, 걷기 5프레임 × 앞/뒤/좌/우). Pixilart `.pixil`을 변환해 넣는다. 씬의 `GameApp.roleSheets`에 배선되어 있다.
- 프롤로그·오프닝 배경: `Assets/Resources/Backgrounds/classroom.png`(교실), `school.png`(학교 정문), `camp.png`(BAYANI CAMP) — 2026-09-18 사용자 그림 반영. 비율을 지키며 화면을 꽉 채운다(넘치는 쪽은 잘림). `museum_dark.png`, `museum_relief.png` 는 선택(없으면 코드 배경).
- 로고: `Assets/Resources/Logo/stranger_logo.png`(투명 배경, 사용자 제작)가 있으면 로고·타이틀 화면이 그 그림을 쓴다. 랜턴 빛·발자국·부제는 코드로 그린다.
- NPC 초상·오브젝트·바닥은 코드로 그린다(`SpriteLib`, `UIPortrait.Npc`, `Backgrounds`).

## 코드 구조 (Assets/Scripts, 네임스페이스 Paradox.*)

| 폴더 | 내용 |
| --- | --- |
| Game | 서버(호스트) 규칙. `GameLogic`(흐름·명령), `Stage1Logic`(관문·침수·집결·정적 도우미), `StageMaps`(박물관 평면도), `GameState`(Phase/StageState/Cmd/GameEvent), `StoryData`(프롤로그·오프닝·클리어 대사), `BotDriver`(봇의 명령), `StageDefs`, `GameRole` |
| Net | `NetHub`(호스트/참가/봇), `SessionNet`(상태 JSON 브로드캐스트·RPC), `AvatarNet`(이동·상호작용 키·BotBrain 길찾기) |
| Client | `LocalNav`(접속 전 화면), `ScreenManager`(라우팅), 화면들(`LogoScreen`…`ResultScreen`), `VnView`(비주얼노벨), `StageScreen`+`StagePanels`+`Popups`+`Donut`+`LogPanel`+`TutorialOverlay`+`MinimapView`(HUD), `StageWorld`(월드), `Comms`(가시성 규칙), `Session`(상태 미러) |
| Core | `UI`(UGUI 팩토리), `SpriteLib`, `CameraFollow`, `FeedbackFX`, `InteractionState`, `WorldText`, `YDepthSort` |

옛 Paradox(4스테이지 프로토타입) 코드는 `Prototype_Backup/ParadoxV1/`에 보관(컴파일 대상 아님).

## 테스트

`Assets/Tests/EditMode`(순수 함수·헤드리스 규칙 테스트, Unity 실행 없이 0.1초):
- `Stage1RuleTests` — 침수·위험 구역·사다리 규칙(시드 300개)·점자 변환.
- `GameLogicHeadlessTests` — 네트워크 없이 `GameLogic`을 돌려 시간 초과→재시도, 브리핑 게이트 30초, 관문 2 명령 거부, 집결 유지 조건 검증.
- `CommsTests` — 채팅 ???·로그 가시성 진리표.

`Assets/Tests/PlayMode`:
- `FullFlowTests` — 봇 수동. 로컬 화면 → 매칭 → 이름/준비 → 오프닝 → 관문 1(오답 포함) → 관문 2(틀린 레버 → 오판 → 리셋 → 정답) → 집결 → 클리어 → 결과 → 로비.
- `BotFlowTests` — 사람은 조작자만, 봇 3명이 실제처럼 플레이.
- `ScreenshotTour` — 환경변수 `PARADOX_SHOTS`가 있으면 모든 화면·팝업을 PNG로 저장.
- `LanClientTests` — **실제 클라이언트 exe** 를 자동 참가 모드로 띄워(에디터 = 호스트, 봇 없이) 루프백 LAN 경로를 검증: 참가(present 2·clientId≠호스트) → FillBots+StartGame → 클라의 Name/Ready 수신 → 오프닝에서 클라 소유 아바타 스폰(활성·OwnerClientId) → 플레이에서 아트리움 텔레포트 → 7초 동안 위치가 0.3 이상 바뀜(OwnerNetworkTransform) → 클라 Brief(briefDone). 환경변수 `PARADOX_CLIENT_EXE` 가 없으면 `Assert.Ignore`. 로그는 `PARADOX_LOG_DIR`(없으면 exe 폴더)의 `client_player.log`·`client_autojoin.log`. Timeout 180초.

에디터가 프로젝트를 열고 있으면 같은 폴더에서 배치 실행이 안 되므로, 복사본(`C:\Users\user\PxCopy`)에 동기화해 실행하는 스크립트를 쓴다(스크래치 `run_tests.ps1 -Filter <클래스> -Tag <태그> [-Platform EditMode|PlayMode] [-Shots] [-CompileOnly]`).

LAN 테스트 순서(스크래치 폴더에서, PowerShell):
```
.\build_client.ps1                                   # PxCopy 동기화 → C:\Users\user\PxCopy\Build\Client\Paradox.exe (log_build.txt)
$env:PARADOX_CLIENT_EXE = "C:\Users\user\PxCopy\Build\Client\Paradox.exe"
$env:PARADOX_LOG_DIR = "<스크래치 폴더>"               # client_player.log / client_autojoin.log 위치
.\run_tests.ps1 -Filter LanClientTests -Tag lan      # 에디터(PxCopy)가 호스트, exe 가 -batchmode -nographics 로 참가
```
빌드 exe 는 빌드 시점의 코드다 — 스크립트를 고쳤으면 `build_client.ps1` 을 다시 돌린 뒤 테스트한다.
