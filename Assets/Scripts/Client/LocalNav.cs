using System;

namespace Paradox.Client
{
    /// <summary>접속 전(로컬) 화면 목록. 로고 → 타이틀 → 프롤로그 → 스테이지 선택 → 매칭, 그리고 설정.</summary>
    public enum LocalScreen { Logo, Title, Prologue, StageSelect, Match, Settings }

    /// <summary>
    /// 접속 전 화면 전환기. 서버 상태가 없을 때 ScreenManager는 "Local_" + Screen 을 화면 키로 쓴다.
    /// Go()는 화면을 바꾸고 Changed 를 발행한다(같은 화면이어도 발행 — ScreenManager가 OnState만 다시 부른다).
    /// </summary>
    public static class LocalNav
    {
        public static LocalScreen Screen = LocalScreen.Logo;
        public static event Action Changed;

        /// <summary>테스트/이어하기용: true면 로고 화면이 첫 프레임에 바로 타이틀로 넘어간다.</summary>
        public static bool SkipLogo;

        public static void Go(LocalScreen s)
        {
            Screen = s;
            Changed?.Invoke();
        }
    }
}
