using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 네트워크 컷씬(Stage 1 오프닝 s1_intro · 클리어 s1_clear). VnView 네트워크 모드: 호스트만 Next/Skip 을 보내고 모두 같은 줄을 본다.
    /// 시점 슬롯(LocalSlot)에 따라 ??? 장벽이 달라진다(오프닝의 스태프 대사는 조작자만 읽힌다).
    /// </summary>
    public class CutsceneScreen : ScreenBase
    {
        private VnView _vn;
        private Cutscene _cut;
        private string _cutId = "";

        /// <summary>테스트 훅(투어) — 지금 떠 있는 컷씬 화면과 정전 암전 진행 여부.</summary>
        public static CutsceneScreen Instance { get; private set; }
        public bool BlackoutActive => _vn != null && _vn.BlackoutActive;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            Instance = this;
            _vn = new VnView();
            _vn.Build(Root);
            _vn.OnNext = () => Session.Send("Next");
            _vn.OnSkip = () => Session.Send("Skip");
            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 1f), new Vector2(0f, -70f), false);
            OnState();
        }

        private static string DefaultBg(string cutId, Phase phase)
        {
            if (cutId == "s1_clear" || phase == Phase.StageClear) return Backgrounds.MuseumRelief;
            if (cutId == "prologue") return Backgrounds.Classroom;
            return Backgrounds.Camp;
        }

        public override void OnState()
        {
            if (_vn == null) return;
            var S = Session.State;
            if (_cut == null || _cutId != S.cutsceneId)
            {
                _cutId = S.cutsceneId ?? "";
                _cut = StoryData.Get(_cutId);
                _vn.SetTeamRow(_cutId == "s1_clear", sticky: true);   // 클리어 컷씬은 내내 유지, 다른 컷씬은 줄의 group 값에 따름(QA D-A3)
            }
            _vn.CanAdvance = Session.IsHost;
            if (_cut == null || _cut.Count == 0) { _vn.RefreshChrome(); return; }
            _vn.Show(_cut, S.cutsceneIndex, Session.LocalSlot, DefaultBg(_cutId, S.phase));
            _vn.RefreshChrome();
        }

        public override void Tick()
        {
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
