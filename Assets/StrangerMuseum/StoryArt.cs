using UnityEngine;

/// <summary>
/// 박물관 이야기 화면(MuseumStory)이 쓰는 초상·배경 그림 묶음(Resources/StoryUI/StoryArt).
/// 프롤로그·캠프 씬이 쓰는 원본 그림(Assets/Art/...)을 그대로 가리킨다 → 같은 그림이 빌드에 두 번 들어가지 않는다.
/// faces: 0 서하린 · 1 이안 · 2 린 · 3 카이 · 4 스태프 · 5 지호
/// </summary>
public sealed class StoryArt : ScriptableObject
{
    public Sprite[] faces = new Sprite[6];
    public Sprite bgCamp, bgClass;
    public Sprite[] cats = new Sprite[0];   // 이스터 에그 고양이(MuseumCats)
}
