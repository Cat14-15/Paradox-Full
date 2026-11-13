namespace Paradox.Language
{
    /// <summary>
    /// 소통에 쓰이는 개념(Concept)의 분류.
    /// 메시지 조합 UI에서 탭/그룹으로 묶는 데 사용한다.
    /// </summary>
    public enum ConceptCategory
    {
        Direction, // 방향: 위/아래/왼쪽/오른쪽
        Color,     // 색: 빨강/파랑/노랑/초록
        Number,    // 수: 하나/둘/셋
        Object,    // 사물: 문/열쇠/레버/버튼/다리
        Action,    // 동작·의사: 가/멈춰/기다려/도와줘/위험/응/아니
        Emote      // 감정 이모트: 기쁨/당황/미안 등 (언어 장벽 없이 항상 통함)
    }
}
