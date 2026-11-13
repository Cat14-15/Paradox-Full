using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;

namespace Paradox.Tests
{
    /// <summary>소통 표시 규칙(명세 6-2, 6-3, 14절 규칙 10, D5)의 순수 함수 검증(QA test-T7). 씬·네트워크 없이 EditMode 에서 돈다.</summary>
    public class CommsTests
    {
        [Test]
        public void CanUnderstand_TruthTable()
        {
            for (int v = 0; v < 4; v++)
                for (int s = 0; s < 4; s++)
                    Assert.AreEqual(v == s || v == Comms.In || s == Comms.In, Comms.CanUnderstand(v, s), $"viewer {v} sender {s}");
        }

        [Test]
        public void Garble_LeavesNoOriginalChars()
        {
            const string src = "경고등 빨강이야 7";
            string g = Comms.Garble(src);
            foreach (char c in g) Assert.IsTrue(c == '?' || c == ' ', $"Garble 출력에 '?'와 공백만 허용: '{c}'");
            Assert.IsTrue(g.Contains("?"), "??? 덩어리가 있어야 함");
            Assert.AreEqual("???", Comms.Garble(""));
            Assert.AreEqual("???", Comms.Garble(null));
            Assert.AreEqual("???", Comms.Garble("   "));
        }

        [Test]
        public void ChatText_ForeignGetsSuffix_InterpreterAndSelfReadOriginal()
        {
            const string src = "경고등 빨강이야";
            string t = Comms.ChatText(Comms.Op, Comms.Ob, src);
            StringAssert.EndsWith("(외국어)", t);
            Assert.IsFalse(t.Contains("빨강") || t.Contains("경고등"), "원문이 남으면 안 됨");
            Assert.AreEqual(src, Comms.ChatText(Comms.In, Comms.Ob, src), "통역은 원문");
            Assert.AreEqual(src, Comms.ChatText(Comms.Ob, Comms.Ob, src), "자기 글은 원문");
            Assert.AreEqual(src, Comms.ChatText(Comms.Na, Comms.In, src), "통역이 쓴 글은 모두 원문");
        }

        [Test]
        public void LogText_Chat_Visibility()
        {
            var e = new GameEvent { type = "Chat", slot = Comms.Op, s = "결과 홀수야" };
            Assert.AreEqual("결과 홀수야", Comms.LogText(e, Comms.Op));
            Assert.AreEqual("결과 홀수야", Comms.LogText(e, Comms.In));
            foreach (int v in new[] { Comms.Na, Comms.Ob })
            {
                string t = Comms.LogText(e, v);
                StringAssert.Contains("(외국어)", t);
                Assert.IsFalse(t.Contains("홀수"), $"viewer {v} 에게 원문 누설");
            }
        }

        [Test]
        public void LogText_Color_OnlyInterpreterAndSenderSeeName()
        {
            var e = new GameEvent { type = "Color", slot = Comms.Ob, a = 1, target = Comms.In };
            StringAssert.Contains(Comms.ColorName(1), Comms.LogText(e, Comms.In));
            StringAssert.Contains(Comms.ColorName(1), Comms.LogText(e, Comms.Ob));
            foreach (int v in new[] { Comms.Op, Comms.Na })
            {
                string t = Comms.LogText(e, v);
                StringAssert.Contains("[색 신고]", t);
                Assert.IsFalse(t.Contains(Comms.ColorName(1)), $"viewer {v} 에게 색 이름 누설");
            }
        }

        [Test]
        public void LogText_Cards_OnlyOperatorAndSenderSeeValue()
        {
            var num = new GameEvent { type = "NumberCard", slot = Comms.In, a = 7, target = Comms.Op };
            var dir = new GameEvent { type = "DirCard", slot = Comms.In, a = 3, target = Comms.Op };
            StringAssert.Contains(Comms.NumberCardText(7), Comms.LogText(num, Comms.Op));
            StringAssert.Contains(Comms.NumberCardText(7), Comms.LogText(num, Comms.In));
            StringAssert.Contains(Comms.DirCardText(3), Comms.LogText(dir, Comms.Op));
            foreach (int v in new[] { Comms.Na, Comms.Ob })
            {
                // 주의: 보이는 쪽 문자열의 색 코드 #F2C572 에 '7'이 들어 있으므로 숫자 한 글자가 아니라 카드 문구로 판정한다
                Assert.IsFalse(Comms.LogText(num, v).Contains(Comms.NumberCardText(7)), $"viewer {v} 에게 숫자 카드 누설");
                Assert.IsFalse(Comms.LogText(dir, v).Contains(Comms.DirGlyph(3)), $"viewer {v} 에게 방향 카드 누설");
                StringAssert.Contains("[카드]", Comms.LogText(num, v));
                StringAssert.Contains("[카드]", Comms.LogText(dir, v));
            }
        }

        [Test]
        public void LogText_Ping_RoomNameOnlyForNavigator_D5()
        {
            var e = new GameEvent { type = "Ping", slot = Comms.Na, a = 3, s = Stage1Logic.RoomName(3) };
            StringAssert.Contains(Stage1Logic.ZoneName(3), Comms.LogText(e, Comms.Na));
            for (int v = 0; v < 4; v++)
            {
                if (v == Comms.Na) continue;
                string t = Comms.LogText(e, v);
                Assert.IsFalse(t.Contains(Stage1Logic.ZoneName(3)) || t.Contains(e.s), $"D5: 비내비 {v} 로그에 방 이름 금지");
                StringAssert.Contains("여기로!", t);
            }
        }

        [Test]
        public void Signal_DangerIsKoreanEverywhere_OthersHaveKoreanSubtitle()
        {
            Assert.AreEqual("위험", Comms.SignalWord("DANGER"), "위험 신호는 어디서나 '위험'(QA copy-C03)");
            Assert.AreEqual("", Comms.SignalKorean("DANGER"), "위험은 부제를 생략");
            Assert.AreEqual("위험", Comms.SignalCaption("DANGER", 11, false), "부제가 비면 단어만");
            StringAssert.Contains("가자!", Comms.SignalCaption("GO", 11, false));
            var e = new GameEvent { type = "Signal", slot = Comms.Ob, s = "DANGER" };
            string log = Comms.LogText(e, Comms.Op);
            StringAssert.Contains("위험", log);
            Assert.IsFalse(log.Contains("danger"), "로그에 영문 danger 가 남으면 안 됨");
            foreach (var id in Comms.SignalIds) Assert.IsTrue(GameLogic.IsSignalId(id), $"서버도 같은 신호 id 를 받는다: {id}");
        }

        [Test]
        public void DisplayTables_MatchServerTables()
        {
            // 클라 표시용 사본(Comms)과 서버 판정(Stage1Logic)의 매뉴얼 표가 어긋나면 통역이 정답을 못 찾는다
            for (int p = 0; p < 2; p++)
                for (int c = 0; c < 4; c++)
                    Assert.AreEqual(Stage1Logic.Table1[p][c], Comms.Table1[p, c], $"Table1[{p}][{c}]");
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    Assert.AreEqual(Stage1Logic.Table2[r][c], Comms.Table2[r, c], $"Table2[{r}][{c}]");
            for (int r = 0; r < 4; r++)
                CollectionAssert.AreEqual(Stage1Logic.RowDigits[r], Comms.Table2Digits[r], $"행 {r} 점자 숫자");
            foreach (int d in Stage1Logic.BrailleDigits)
                CollectionAssert.AreEqual(Stage1Logic.BrailleDots(d), Comms.BrailleDots(d), $"점자 {d} 점 번호");
            for (int c = 0; c < 4; c++) Assert.AreEqual(Stage1Logic.ColorName(c), Comms.ColorName(c), $"색 이름 {c}");
            for (int d = 0; d < 4; d++) Assert.AreEqual(Stage1Logic.DirGlyph(d), Comms.DirGlyph(d), $"방향 기호 {d}");
        }
    }
}
