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
// VHDL -> Relay Compiler - Optimizer (論理最適化)
// ============================================================

using System.Text.RegularExpressions;

namespace RelayCompiler;

public class Optimizer
{
    private static readonly Regex _stripRe = new Regex(@"^['""]+|['""]+$", RegexOptions.Compiled);

    public Expr Optimize(Expr expr)
    {
        return expr switch
        {
            NotExpr n => OptimizeNot(n),
            BinExpr b => OptimizeBinary(b),
            MuxExpr m => OptimizeMux(m),
            _ => expr
        };
    }

    private Expr OptimizeNot(NotExpr n)
    {
        var inner = Optimize(n.Expr!);
        if (inner is NotExpr ne) return ne.Expr!;
        if (inner is LitExpr le)
        {
            if (ExprHelpers.IsTrueLit(le)) return ExprHelpers.LIT("false");
            if (ExprHelpers.IsFalseLit(le)) return ExprHelpers.LIT("true");
            if (le.Value == "'1'") return ExprHelpers.LIT("'0'");
            if (le.Value == "'0'") return ExprHelpers.LIT("'1'");
        }
        return ExprHelpers.NOT(inner);
    }

    private Expr OptimizeBinary(BinExpr b)
    {
        var left = Optimize(b.Left!);
        var right = Optimize(b.Right!);

        // literal付き二項比較の定数畳み込み
        if ("=/!=</>/<=/>=".Contains(b.Op))
        {
            if (left is LitExpr l1 && right is LitExpr l2)
            {
                var v1 = _stripRe.Replace(l1.Value, "");
                var v2 = _stripRe.Replace(l2.Value, "");
                var isEq = v1.ToLowerInvariant() == v2.ToLowerInvariant();
                if (b.Op == "=") return ExprHelpers.LIT(isEq ? "true" : "false");
                if (b.Op == "/=") return ExprHelpers.LIT(!isEq ? "true" : "false");
                if (int.TryParse(v1, out var n1) && int.TryParse(v2, out var n2))
                {
                    if (b.Op == "<") return ExprHelpers.LIT(n1 < n2 ? "true" : "false");
                    if (b.Op == ">") return ExprHelpers.LIT(n1 > n2 ? "true" : "false");
                    if (b.Op == "<=") return ExprHelpers.LIT(n1 <= n2 ? "true" : "false");
                    if (b.Op == ">=") return ExprHelpers.LIT(n1 >= n2 ? "true" : "false");
                }
            }
        }

        if (b.Op == "AND")
        {
            if (ExprHelpers.IsTrueLit(left)) return right;
            if (ExprHelpers.IsTrueLit(right)) return left;
            if (ExprHelpers.IsFalseLit(left)) return left;
            if (ExprHelpers.IsFalseLit(right)) return right;
            return SimplifyAndChain(left, right);
        }
        else if (b.Op == "OR")
        {
            if (ExprHelpers.IsTrueLit(left) || ExprHelpers.IsTrueLit(right)) return ExprHelpers.LIT("true");
            if (ExprHelpers.IsFalseLit(left)) return right;
            if (ExprHelpers.IsFalseLit(right)) return left;
        }
        return ExprHelpers.BIN(left, b.Op, right);
    }

    private Expr OptimizeMux(MuxExpr m)
    {
        var cond = Optimize(m.Cond!);
        var t = Optimize(m.T!);
        var f = Optimize(m.F!);

        // MUX(NOT(C), A, MUX(C, B, D)) -> MUX(C, B, A)
        if (cond is NotExpr ne && f is MuxExpr fm &&
            LogicPrinter.ToString(ne.Expr!) == LogicPrinter.ToString(fm.Cond!))
            return ExprHelpers.MUX(ne.Expr!, fm.T!, t);

        if (ExprHelpers.IsTrueLit(cond)) return t;
        if (ExprHelpers.IsFalseLit(cond)) return f;
        if (LogicPrinter.ToString(t) == LogicPrinter.ToString(f)) return t;
        return ExprHelpers.MUX(cond, t, f);
    }

    private Expr SimplifyAndChain(Expr left, Expr right)
    {
        var conjuncts = new List<Expr>();
        FlattenAnd(left, conjuncts);
        FlattenAnd(right, conjuncts);

        var positiveEqs = conjuncts
            .OfType<BinExpr>()
            .Where(c => c.Op == "=" && c.Left is IdExpr && c.Right is LitExpr)
            .ToList();

        var toRemove = new HashSet<Expr>();
        foreach (var eq in positiveEqs)
        {
            var targetName = ((IdExpr)eq.Left!).Name;
            var targetVal = ((LitExpr)eq.Right!).Value;
            foreach (var c in conjuncts)
            {
                if (c is NotExpr ce && ce.Expr is BinExpr ceb && ceb.Op == "=" &&
                    ceb.Left is IdExpr cebId && cebId.Name == targetName &&
                    ceb.Right is LitExpr cebLit && cebLit.Value != targetVal)
                    toRemove.Add(c);
            }
        }

        var remaining = conjuncts.Where(c => !toRemove.Contains(c)).ToList();
        if (!remaining.Any()) return ExprHelpers.LIT("true");

        Expr result = remaining[0];
        foreach (var r in remaining.Skip(1)) result = ExprHelpers.BIN(result, "AND", r);
        return result;
    }

    private void FlattenAnd(Expr expr, List<Expr> list)
    {
        if (expr is BinExpr b && b.Op == "AND")
        {
            FlattenAnd(b.Left!, list);
            FlattenAnd(b.Right!, list);
        }
        else list.Add(expr);
    }
}
