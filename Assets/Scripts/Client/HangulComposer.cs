using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Client
{
    /// <summary>
    /// 한글 입력 대체 경로(두벌식). OS 입력기(IME)가 유니티까지 전달되지 않을 때도 키 입력만으로 한글을 조합한다.
    /// Key(알파벳, shift) → 확정된 글자(있으면), Current → 지금 조합 중인 글자, Back() → 한 단계 지우기.
    /// </summary>
    public sealed class HangulComposer
    {
        private const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        private const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
        private static readonly string[] Jong = { "", "ㄱ", "ㄲ", "ㄳ", "ㄴ", "ㄵ", "ㄶ", "ㄷ", "ㄹ", "ㄺ", "ㄻ", "ㄼ", "ㄽ", "ㄾ", "ㄿ", "ㅀ", "ㅁ", "ㅂ", "ㅄ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" };

        // 알파벳 → 자모 (shift 일 때 바뀌는 것: q w e r t o p)
        private const string Lower = "ㅁㅠㅊㅇㄷㄹㅎㅗㅑㅓㅏㅣㅡㅜㅐㅔㅂㄱㄴㅅㅕㅍㅈㅌㅛㅋ";   // a..z
        private const string Upper = "ㅁㅠㅊㅇㄸㄹㅎㅗㅑㅓㅏㅣㅡㅜㅒㅖㅃㄲㄴㅆㅕㅍㅉㅌㅛㅋ";

        private int _l = -1, _v = -1, _t;   // 초성·중성·종성(0 = 없음)

        public bool Active => _l >= 0 || _v >= 0;

        public static bool IsConsonant(char j) => Cho.IndexOf(j) >= 0 || j == 'ㄳ' || j == 'ㄵ';

        public static char JamoOf(char letter, bool shift)
        {
            int i = char.ToLowerInvariant(letter) - 'a';
            if (i < 0 || i >= 26) return '\0';
            return (shift ? Upper : Lower)[i];
        }

        public void Reset() { _l = -1; _v = -1; _t = 0; }

        public string Current
        {
            get
            {
                if (_l < 0 && _v < 0) return "";
                if (_v < 0) return Cho[_l].ToString();
                if (_l < 0) return Jung[_v].ToString();
                return ((char)(0xAC00 + (_l * 21 + _v) * 28 + _t)).ToString();
            }
        }

        private string Flush() { string s = Current; Reset(); return s; }

        private static int JongOf(char c) { for (int i = 1; i < Jong.Length; i++) if (Jong[i][0] == c) return i; return 0; }

        private static int CompoundVowel(int v, int add)
        {
            // ㅗ(8)+ㅏ(0)=ㅘ(9), ㅗ+ㅐ(1)=ㅙ(10), ㅗ+ㅣ(20)=ㅚ(11), ㅜ(13)+ㅓ(4)=ㅝ(14), ㅜ+ㅔ(5)=ㅞ(15), ㅜ+ㅣ=ㅟ(16), ㅡ(18)+ㅣ=ㅢ(19)
            if (v == 8) { if (add == 0) return 9; if (add == 1) return 10; if (add == 20) return 11; }
            if (v == 13) { if (add == 4) return 14; if (add == 5) return 15; if (add == 20) return 16; }
            if (v == 18 && add == 20) return 19;
            return -1;
        }

        private static int CompoundTail(int t, char add)
        {
            string a = Jong[t];
            string pair = a + add;
            for (int i = 1; i < Jong.Length; i++) if (Jong[i].Length == 1 && "ㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄ".IndexOf(Jong[i][0]) >= 0 && Decompose(Jong[i][0]) == pair) return i;
            return 0;
        }

        private static string Decompose(char c)
        {
            switch (c)
            {
                case 'ㄳ': return "ㄱㅅ"; case 'ㄵ': return "ㄴㅈ"; case 'ㄶ': return "ㄴㅎ"; case 'ㄺ': return "ㄹㄱ"; case 'ㄻ': return "ㄹㅁ";
                case 'ㄼ': return "ㄹㅂ"; case 'ㄽ': return "ㄹㅅ"; case 'ㄾ': return "ㄹㅌ"; case 'ㄿ': return "ㄹㅍ"; case 'ㅀ': return "ㄹㅎ"; case 'ㅄ': return "ㅂㅅ";
            }
            return c.ToString();
        }

        /// <summary>자모 하나를 넣는다. 확정된 글자들을 돌려준다(없으면 빈 문자열).</summary>
        public string Key(char j)
        {
            var done = new StringBuilder();
            int ci = Cho.IndexOf(j), vi = Jung.IndexOf(j);
            if (ci >= 0)   // 자음
            {
                if (_l < 0 && _v < 0) { _l = ci; return ""; }
                if (_v < 0) { if (_l >= 0) { done.Append(Flush()); _l = ci; } return done.ToString(); }
                if (_l < 0) { done.Append(Flush()); _l = ci; return done.ToString(); }
                if (_t == 0)
                {
                    int t = JongOf(j);
                    if (t > 0) { _t = t; return ""; }
                    done.Append(Flush()); _l = ci; return done.ToString();
                }
                int ct = CompoundTail(_t, j);
                if (ct > 0) { _t = ct; return ""; }
                done.Append(Flush()); _l = ci; return done.ToString();
            }
            if (vi >= 0)   // 모음
            {
                if (_l < 0 && _v < 0) { _v = vi; return ""; }
                if (_v < 0) { _v = vi; return ""; }
                if (_l < 0) { int cv0 = CompoundVowel(_v, vi); if (cv0 >= 0) { _v = cv0; return ""; } done.Append(Flush()); _v = vi; return done.ToString(); }
                if (_t == 0)
                {
                    int cv = CompoundVowel(_v, vi);
                    if (cv >= 0) { _v = cv; return ""; }
                    done.Append(Flush()); _v = vi; return done.ToString();
                }
                // 받침이 다음 글자의 초성으로 넘어간다
                string tail = Jong[_t];
                char moved = tail[tail.Length - 1];
                if (Decompose(Jong[_t][0]).Length == 2) { moved = Decompose(Jong[_t][0])[1]; _t = JongOf(Decompose(Jong[_t][0])[0]); }
                else _t = 0;
                done.Append(Flush());
                _l = Cho.IndexOf(moved); _v = vi;
                return done.ToString();
            }
            return "";
        }

        /// <summary>조합 중인 글자를 한 단계 지운다. 지울 게 없으면 false(바깥에서 확정된 글자를 지운다).</summary>
        public bool Back()
        {
            if (!Active) return false;
            if (_t > 0)
            {
                string d = Decompose(Jong[_t][0]);
                _t = d.Length == 2 ? JongOf(d[0]) : 0;
            }
            else if (_v >= 0)
            {
                // 겹모음이면 한 단계만
                if (_v == 9 || _v == 10 || _v == 11) _v = 8;
                else if (_v == 14 || _v == 15 || _v == 16) _v = 13;
                else if (_v == 19) _v = 18;
                else _v = -1;
            }
            else _l = -1;
            return true;
        }
    }
}
