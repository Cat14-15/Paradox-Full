namespace Paradox.Roles
{
    /// <summary>
    /// "내 역할"과 "이번 스테이지의 현지인 역할"을 담는 중앙 상태.
    /// 지금은 로컬 테스트용으로 이 값을 직접 바꾸지만,
    /// 2단계 네트워킹에서는 서버가 이 값을 배정·복제하게 된다.
    ///
    /// 표지판 가독성 규칙: 내 역할 == 이번 스테이지 현지인 역할 일 때만 표지판을 읽는다.
    /// </summary>
    public static class RoleService
    {
        public static event System.Action Changed;

        private static GameRole _localRole = GameRole.Operator;
        private static GameRole _stageLocalRole = GameRole.Interpreter; // Stage 1 기본: 통역이 현지인

        public static GameRole LocalRole
        {
            get => _localRole;
            set { if (_localRole != value) { _localRole = value; Changed?.Invoke(); } }
        }

        public static GameRole StageLocalRole
        {
            get => _stageLocalRole;
            set { if (_stageLocalRole != value) { _stageLocalRole = value; Changed?.Invoke(); } }
        }

        /// <summary>내가 이번 스테이지의 현지인인가? (표지판을 원래대로 읽을 수 있는가)</summary>
        public static bool CanReadSigns => _localRole == _stageLocalRole;
    }
}
