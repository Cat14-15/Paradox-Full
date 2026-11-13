컷씬 배경 그림 넣는 자리 (STRANGER)

이 폴더에 아래 이름으로 PNG 를 넣으면 게임이 자동으로 그 그림을 배경으로 씁니다.
(2026-09-18: classroom / school / camp 3장 반영됨. 비율 유지 + 화면 꽉 채움.)
그림이 없으면 코드로 그린 단순한 배경(책상 격자 교실, 횡단보도, 캠프장과 버스)이 대신 나옵니다.

  classroom.png      프롤로그 씬 1·2 — 교실 (탑다운 픽셀아트)
  school.png         프롤로그 씬 3 — 학교 정문 앞 횡단보도
  camp.png           Stage 1 오프닝 씬 1·2 — BAYANI CAMP 캠프장, 버스
  museum_dark.png    (선택) 오프닝 씬 3·4 — 정전된 박물관 로비, 손전등 불빛
  museum_relief.png  (선택) 클리어 컷씬 — 분수 아트리움

권장: 1920x1080 또는 1600x900. 임포트 설정은 Texture Type = Default(또는 Sprite) 그대로 두어도 됩니다.
픽셀아트라면 Filter Mode = Point, Compression = None 으로 두면 더 선명합니다.

코드에서 읽는 곳: Assets/Scripts/Client/Backgrounds.cs (Resources.Load<Texture2D>("Backgrounds/" + 키)).
