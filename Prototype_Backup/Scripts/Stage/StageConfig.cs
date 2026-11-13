using Paradox.Roles;
using UnityEngine;

namespace Paradox.Stage
{
    /// <summary>
    /// 한 스테이지의 설정을 한곳에 모은다. 스테이지마다 이 값만 바꾸면
    /// 현지인 역할·배경 언어·정답 코드·제한시간이 정해진다.
    /// (스테이지 회전 구조를 데이터로 표현 — 각 씬에 하나씩 둔다)
    /// </summary>
    public class StageConfig : MonoBehaviour
    {
        public static StageConfig Current { get; private set; }

        [SerializeField] private string stageTitle = "Stage 1 · 필리핀 · 태풍";
        [Tooltip("이번 스테이지의 현지인(표지판을 읽는 역할)")]
        [SerializeField] private GameRole localRole = GameRole.Interpreter;
        [Tooltip("표지판/글자카드가 쓰인 배경 언어 id (LanguageDatabase의 언어)")]
        [SerializeField] private string stageLanguageId = "verda";
        [Tooltip("문을 여는 정답 색 조합: RED / BLUE / YELLOW / GREEN")]
        [SerializeField] private string[] requiredColorConcepts = { "RED", "GREEN" };
        [Tooltip("제한시간(초). 0이면 시간 압박 없음(튜토리얼).")]
        [SerializeField] private float timeLimit = 0f;

        [Header("배경/연출 (스테이지별로 다르게)")]
        [Tooltip("바닥 기본색")]
        [SerializeField] private Color groundColor = new Color(0.32f, 0.47f, 0.26f);
        [Tooltip("차오르는 물/재난 오버레이 색")]
        [SerializeField] private Color floodColor = new Color(0.22f, 0.42f, 0.85f);

        public string StageTitle => stageTitle;
        public GameRole LocalRole => localRole;
        public string StageLanguageId => stageLanguageId;
        public string[] RequiredColorConcepts => requiredColorConcepts;
        public float TimeLimit => timeLimit;
        public Color GroundColor => groundColor;
        public Color FloodColor => floodColor;

        private void Awake()
        {
            Current = this;
            // 이번 스테이지의 현지인 역할을 설정(표지판 ??? 규칙에 즉시 반영)
            RoleService.StageLocalRole = localRole;
        }
    }
}
