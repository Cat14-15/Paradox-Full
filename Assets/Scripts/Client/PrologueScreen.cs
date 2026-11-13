using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 프롤로그(PDF s03) — 접속 전에 각자 재생하는 로컬 비주얼노벨. StoryData.Get("prologue") 를 VnView 로 넘긴다.
    /// 끝까지 보거나 SKIP 하면 Progress.prologueSeen = true → 스테이지 선택으로.
    /// </summary>
    public class PrologueScreen : ScreenBase
    {
        private VnView _vn;
        private Cutscene _cut;
        private int _index;
        private bool _finished;

        /// <summary>테스트 훅 — 현재 프롤로그 화면과 대사 줄 번호.</summary>
        public static PrologueScreen Instance { get; private set; }
        public int Index => _index;
        public bool Typing => _vn != null && _vn.Typing;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            Instance = this;
            _cut = StoryData.Get("prologue");
            _vn = new VnView();
            _vn.Build(Root);
            _vn.CanAdvance = true;
            _vn.OnNext = Next;
            _vn.OnSkip = Finish;
            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 1f), new Vector2(0f, -70f), false);

            if (_cut == null || _cut.Count == 0) { Finish(); return; }
            _index = 0;
            _vn.Show(_cut, _index, -1, Backgrounds.Classroom);
        }

        private void Next()
        {
            if (_finished || _cut == null) return;
            if (_index >= _cut.Count - 1) { Finish(); return; }
            _index++;
            _vn.Show(_cut, _index, -1, Backgrounds.Classroom);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Progress.prologueSeen = true;
            LocalNav.Go(LocalScreen.StageSelect);
        }

        public override void Tick()
        {
            if (_finished) return;
            _vn?.Tick();
        }

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            _vn?.Dispose();
            _vn = null;
            base.Dispose();
        }
    }
}
