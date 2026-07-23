/**
 * ============================================================================
 * 
 *  [ COPYRIGHT & LICENSE ]
 *  All rights reserved by Kaoru Zenyouji (Discretek Inc.). 
 *  Unauthorized copying, distribution, or commercial sale of this software 
 *  is strictly prohibited without explicit prior permission from the author.
 * 
 *  [ DISCLAIMER ]
 *  This software is provided "AS IS", without warranty of any kind. 
 *  The author shall not be held liable for any claims, damages, or other 
 *  liabilities arising from the use or inability to use this software.
 * 
 *  [ ACADEMIC & LEARNER SUPPORT ]
 *  To all students, Kosen (National Institutes of Technology) students, 
 *  university scholars, and passionate self-learners: 
 *  I wholeheartedly support your educational journey. If you use this code 
 *  for learning purposes, I will provide the maximum possible support and 
 *  guidance within my personal capacity. Feel free to reach out!
 * 
 *  [ DONATIONS ]
 *  If you find this code helpful or valuable, your support is deeply 
 *  appreciated! Contributions help keep development going. 
 *  Check out my Amazon Wishlist here:
 *  => https://www.amazon.jp/hz/wishlist/ls/CUS2OELICET0?ref_=wl_share
 * 
 * ============================================================================
 */

// ============================================================
// VHDL -> Relay Compiler - Lexer (字句解析)
// ============================================================

namespace RelayCompiler;

public struct Token { public string Type; public string Value; }

public class Lexer
{
    private readonly string _src;
    private int _pos = 0;
    private readonly string[] _symbols =
        ["<=", "=>", "/=", ">=", ":", ";", "(", ")", "=", "+", "-", ",", "<", ">", "&"];
    private readonly HashSet<string> _keywords = new(
        ["library", "use", "entity", "is", "port", "in", "out", "inout",
         "architecture", "of", "begin", "end", "process", "if", "then",
         "elsif", "else", "case", "when", "others", "signal",
         "and", "or", "not", "xor", "nand", "nor", "xnor"],
        StringComparer.OrdinalIgnoreCase);

    public Lexer(string src) { _src = src; }

    // トークン列を生成
    public Token[] Tokenize()
    {
        var tokens = new List<Token>();
        while (_pos < _src.Length)
        {
            var c = _src[_pos];
            if (char.IsWhiteSpace(c)) { _pos++; continue; }
            // コメント
            if (c == '-' && _pos + 1 < _src.Length && _src[_pos + 1] == '-')
            {
                while (_pos < _src.Length && _src[_pos] != '\n') _pos++;
                continue;
            }
            // 16進リテラル X"..."
            if ((c == 'x' || c == 'X') && _pos + 1 < _src.Length && _src[_pos + 1] == '"')
            {
                var start = _pos;
                _pos += 2;
                while (_pos < _src.Length && _src[_pos] != '"') _pos++;
                if (_pos < _src.Length) _pos++;
                tokens.Add(new Token { Type = "Literal", Value = _src.Substring(start, _pos - start) });
                continue;
            }
            // 引用リテラル '...' または "..."
            if (c == '"' || c == '\'')
            {
                var quote = c;
                var start = _pos++;
                while (_pos < _src.Length && _src[_pos] != quote) _pos++;
                if (_pos < _src.Length) _pos++;
                tokens.Add(new Token { Type = "Literal", Value = _src.Substring(start, _pos - start) });
                continue;
            }
            // 識別子またはキーワード
            if (char.IsLetter(c))
            {
                var start = _pos;
                while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_')) _pos++;
                var word = _src.Substring(start, _pos - start);
                var lower = word.ToLowerInvariant();
                if (_keywords.Contains(lower))
                    tokens.Add(new Token { Type = "Keyword", Value = lower });
                else
                    tokens.Add(new Token { Type = "Identifier", Value = word.ToLowerInvariant() });
                continue;
            }
            // 数字
            if (char.IsDigit(c))
            {
                var start = _pos;
                while (_pos < _src.Length && char.IsDigit(_src[_pos])) _pos++;
                tokens.Add(new Token { Type = "Literal", Value = _src.Substring(start, _pos - start) });
                continue;
            }
            // 記号
            bool symFound = false;
            foreach (var sym in _symbols)
            {
                if (_pos + sym.Length <= _src.Length && _src.Substring(_pos, sym.Length) == sym)
                {
                    tokens.Add(new Token { Type = "Symbol", Value = sym });
                    _pos += sym.Length;
                    symFound = true;
                    break;
                }
            }
            if (!symFound) _pos++; // 未知文字をスキップ
        }
        tokens.Add(new Token { Type = "EOF", Value = "" });
        return tokens.ToArray();
    }
}
