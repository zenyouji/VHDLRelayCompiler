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
// VHDL -> Relay Compiler - BitBlaster (ビット展開)
// ============================================================

namespace RelayCompiler;

public class BitBlaster
{
    private readonly Dictionary<string, int> _widthMap;

    public BitBlaster(Dictionary<string, int> widthMap) { _widthMap = widthMap; }

    private int WidthOf(string name) => _widthMap.TryGetValue(name.ToLowerInvariant(), out var w) ? w : 1;

    private int InferCompareWidth(Expr left, Expr right)
    {
        int w = 1;
        if (left is IdExpr il) w = Math.Max(w, WidthOf(il.Name));
        if (right is IdExpr rl) w = Math.Max(w, WidthOf(rl.Name));
        if (right is LitExpr rlt) w = Math.Max(w, LiteralBits(rlt.Value, w).Length);
        if (left is LitExpr llt) w = Math.Max(w, LiteralBits(llt.Value, w).Length);
        return w;
    }

    // リテラル値を2進数文字列に変換
    private string LiteralBits(string literalValue, int minWidth)
    {
        if (literalValue.Length >= 2 && char.ToLower(literalValue[0]) == 'x' && literalValue[1] == '"')
        {
            var end = literalValue.IndexOf('"', 2);
            if (end < 0) end = literalValue.Length - 1;
            var hexStr = literalValue.Substring(2, end - 2);
            var hexBits = "";
            foreach (var hc in hexStr) hexBits += int.Parse(hc.ToString(), System.Globalization.NumberStyles.HexNumber).ToString("d4");
            return hexBits;
        }
        var bits = literalValue.Trim('\'', '"');
        var isStringLiteral = literalValue.StartsWith("\"");
        if (!isStringLiteral && int.TryParse(bits, out var num))
        {
            var bin = Convert.ToString(num, 2);
            return bin.Length >= minWidth ? bin : new string('0', minWidth - bin.Length) + bin;
        }
        return bits;
    }

    // exprのbitIndexビットの値を導出
    public Expr ExtractBit(Expr expr, int bitIndex)
    {
        if (expr is BinExpr bin && "=/!=</>/<=/>=".Contains(bin.Op))
        {
            if (bitIndex > 0) return ExprHelpers.LIT("'0'");
            var width = InferCompareWidth(bin.Left!, bin.Right!);
            return bin.Op switch
            {
                "=" => GenerateEqualsLogic(bin.Left!, bin.Right!, width),
                "/=" => ExprHelpers.NOT(GenerateEqualsLogic(bin.Left!, bin.Right!, width)),
                "<" => GenerateLessThanLogic(bin.Left!, bin.Right!, width),
                ">" => GenerateLessThanLogic(bin.Right!, bin.Left!, width),
                "<=" => ExprHelpers.BIN(GenerateLessThanLogic(bin.Left!, bin.Right!, width), "OR", GenerateEqualsLogic(bin.Left!, bin.Right!, width)),
                _ => ExprHelpers.NOT(GenerateLessThanLogic(bin.Left!, bin.Right!, width)) // >=
            };
        }

        return expr switch
        {
            LitExpr le => ExprHelpers.LIT($"'{GetBitChar(le.Value, bitIndex)}'"),
            IdExpr ie => WidthOf(ie.Name) > 1 ? ExprHelpers.ID($"{ie.Name}[{bitIndex}]") : (bitIndex == 0 ? ExprHelpers.ID(ie.Name) : ExprHelpers.LIT("'0'")),
            MuxExpr me => ExprHelpers.MUX(ExpandCondition(me.Cond!), ExtractBit(me.T!, bitIndex), ExtractBit(me.F!, bitIndex)),
            NotExpr ne => ExprHelpers.NOT(ExtractBit(ne.Expr!, bitIndex)),
            BinExpr be when be.Op == "+" || be.Op == "-" => GenerateAdderLogic(be.Left!, be.Right!, be.Op == "-", bitIndex),
            BinExpr be when be.Op == "AND" || be.Op == "OR" => ExprHelpers.BIN(ExtractBit(be.Left!, bitIndex), be.Op, ExtractBit(be.Right!, bitIndex)),
            BinExpr be when be.Op == "XOR" => XOR(ExtractBit(be.Left!, bitIndex), ExtractBit(be.Right!, bitIndex)),
            CallExpr ce => ce.Func.ToLowerInvariant() == "rising_edge" ? ExprHelpers.ID($"posedge({LogicPrinter.ToString(ce.Arg!)})")
            : throw new Exception($"予期せぬ関数: {ce.Func}"),
            _ => throw new Exception($"予期せぬ式: {LogicPrinter.ToString(expr)}")
        };
    }

    // 全ビットのXNOR連鎖 (=比較) の生成
    private Expr GenerateEqualsLogic(Expr left, Expr right, int width)
    {
        Expr? result = null;
        for (int i = 0; i < width; i++)
        {
            var a = ExtractBit(left, i);
            var b = ExtractBit(right, i);
            var xnor = ExprHelpers.NOT(XOR(a, b));
            result = result == null ? xnor : ExprHelpers.BIN(result, "AND", xnor);
        }
        return result ?? ExprHelpers.LIT("'1'");
    }

    // <比較の生成
    private Expr GenerateLessThanLogic(Expr left, Expr right, int width)
    {
        Expr lessThan = ExprHelpers.LIT("'0'");
        for (int i = 0; i < width; i++)
        {
            var a = ExtractBit(left, i);
            var b = ExtractBit(right, i);
            var xnor = ExprHelpers.NOT(XOR(a, b));
            lessThan = ExprHelpers.BIN(
                ExprHelpers.BIN(ExprHelpers.NOT(a), "AND", b),
                "OR",
                ExprHelpers.BIN(xnor, "AND", lessThan));
        }
        return lessThan;
    }

    // 加算・減算回路のbitIndexビット目を生成
    private Expr GenerateAdderLogic(Expr leftExpr, Expr rightExpr, bool isSubtraction, int bitIndex)
    {
        Expr carry = isSubtraction ? ExprHelpers.LIT("'1'") : ExprHelpers.LIT("'0'");
        Expr? sum = null;

        for (int i = 0; i <= bitIndex; i++)
        {
            var a = ExtractBit(leftExpr, i);
            var b = isSubtraction ? ExprHelpers.NOT(ExtractBit(rightExpr, i)) : ExtractBit(rightExpr, i);

            if (i == bitIndex)
            {
                sum = XOR(XOR(a, b), carry);
                break;
            }
            carry = ExprHelpers.BIN(
                ExprHelpers.BIN(a, "AND", b),
                "OR",
                ExprHelpers.BIN(carry, "AND", XOR(a, b)));
        }
        return sum!;
    }

    private Expr XOR(Expr a, Expr b)
    {
        return ExprHelpers.BIN(
            ExprHelpers.BIN(a, "AND", ExprHelpers.NOT(b)),
            "OR",
            ExprHelpers.BIN(ExprHelpers.NOT(a), "AND", b));
    }

    // リテラルのbitIndexビット目を取得
    private char GetBitChar(string literalValue, int bitIndex)
    {
        var bits = LiteralBits(literalValue, 4);
        var idx = bits.Length - 1 - bitIndex;
        if (idx < 0 || idx >= bits.Length) return '0';
        return bits[idx];
    }

    // 条件式をビットレベルに展開
    public Expr ExpandCondition(Expr expr)
    {
        if (expr is BinExpr be && (be.Op == "=" || be.Op == "/=") && be.Left is IdExpr ie && be.Right is LitExpr le)
        {
            var id = ie;
            var originalValue = le.Value;
            var isSingleBit = originalValue.StartsWith("'") || WidthOf(id.Name) <= 1;
            string bits;
            if (isSingleBit)
                bits = originalValue.Trim('\'', '"');
            else
            {
                bits = LiteralBits(originalValue, WidthOf(id.Name));
                if (bits.Length < WidthOf(id.Name)) bits = new string('0', WidthOf(id.Name) - bits.Length) + bits;
            }

            Expr? result = null;
            for (int i = 0; i < (isSingleBit ? 1 : bits.Length); i++)
            {
                Expr bitId = isSingleBit ? ExprHelpers.ID(id.Name) : ExprHelpers.ID($"{id.Name}[{i}]");
                char bitVal = isSingleBit ? bits[0] : bits[bits.Length - 1 - i];
                Expr bitCompare = ExprHelpers.BIN(bitId, "=", ExprHelpers.LIT($"'{bitVal}'"));
                result = result == null ? bitCompare : ExprHelpers.BIN(result, "AND", bitCompare);
            }
            var eq = result ?? ExprHelpers.LIT("true");
            return be.Op == "=" ? eq : ExprHelpers.NOT(eq);
        }

        if (expr is BinExpr be2 && (be2.Op == "AND" || be2.Op == "OR"))
            return ExprHelpers.BIN(ExpandCondition(be2.Left!), be2.Op, ExpandCondition(be2.Right!));

        if (expr is BinExpr be3 && be3.Op == "XOR")
            return XOR(ExpandCondition(be3.Left!), ExpandCondition(be3.Right!));

        if (expr is NotExpr ne) return ExprHelpers.NOT(ExpandCondition(ne.Expr!));

        if (expr is CallExpr ce && ce.Func.ToLowerInvariant() == "rising_edge")
            return ExprHelpers.ID($"posedge({LogicPrinter.ToString(ce.Arg!)})");

        return expr;
    }
}
