using Paradox.Client;
using Paradox.Game;
using UnityEngine;

/// <summary>
/// AI 친구들의 공통 반응. 퍼즐별 행동은 각 Puzzle* 이 하고, 여기서는 "말이 안 통하는" 순간을 맡는다.
///  · 사람이 한국어 문장만 쓰면(숫자·화살표·카드가 없으면) AI 하나가 자기 나라 말로 "뭐?" 하고 갸웃한다.
///    처음 한 번은 화면에 요령을 알려 준다: "친구들은 한국어를 몰라요. 신호 카드로 말해 봐요".
///  · 고마움·좋아요 같은 그림을 보내면 웃으며 받아 준다.
/// </summary>
public sealed class MuseumBots : MonoBehaviour
{
    private float nextReply;
    private bool tipShown;
    private int turn;

    private void OnEnable() { Story1Net.ChatLine += OnChat; }
    private void OnDisable() { Story1Net.ChatLine -= OnChat; }

    private static int PickBot(int seed)
    {
        for (int k = 0; k < 4; k++) { int s = 1 + (seed + k) % 3; if (Story1Role.IsBot(s)) return s; }
        return -1;
    }

    private void OnChat(int slot, string text)
    {
        if (Story1Role.IsBot(slot) || MuseumStory.Busy) return;
        if (MuseumSignals.OnlyWords(text))
        {
            if (slot == Story1Role.Current && !tipShown && PickBot(0) >= 0)
            {
                tipShown = true;
                MuseumHud.Toast("동료들은 한국어를 모른다 · 숫자·화살표·모양 카드로", MuseumUI.Amber);
            }
            if (!MuseumNet.Authority || Time.time < nextReply) return;
            int b = PickBot(turn++);
            if (b < 0) return;
            nextReply = Time.time + 5f;
            MuseumNet.BotSay(b, MuseumVoice.WhatW(b));
            return;
        }
        if (!MuseumNet.Authority || Time.time < nextReply) return;
        bool warm = text.IndexOf(MuseumEmoji.Char(8)) >= 0 || text.IndexOf(MuseumEmoji.Char(9)) >= 0 || text.IndexOf(MuseumEmoji.Char(11)) >= 0 || text.Contains("♥");
        if (warm)
        {
            int b = PickBot(turn++);
            if (b < 0) return;
            nextReply = Time.time + 4f;
            MuseumNet.BotSay(b, MuseumVoice.YesW(b) + "! " + MuseumEmoji.Char(9));
        }
    }
}
