namespace Paradox.Core
{
    /// <summary>
    /// 월드 입력(이동·상호작용)을 잠글지 여부. 채팅 입력 중이거나 오버레이가 떠 있으면 잠근다.
    /// 여러 UI가 겹칠 수 있으므로 카운터로 관리한다.
    /// </summary>
    public static class InteractionState
    {
        private static int _lockCount;

        public static bool InputLocked => _lockCount > 0;

        public static void PushLock() => _lockCount++;

        public static void PopLock()
        {
            _lockCount--;
            if (_lockCount < 0) _lockCount = 0;
        }

        public static void Reset() => _lockCount = 0;
    }
}
