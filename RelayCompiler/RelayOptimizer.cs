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
// VHDL -> Relay Compiler - RelayOptimizer
// ============================================================

namespace RelayCompiler;

public class RelayOptimizer
{
    // 比較式(A = '1', A = '0')を単純な論理式(A, NOT A)に正規化
    public Expr NormalizeBooleans(Expr expr)
    {
        if (expr is BinExpr be)
        {
            if (be.Op == "=" && be.Right is LitExpr rel)
            {
                var leftNorm = NormalizeBooleans(be.Left!);
                if (rel.Value == "'1'" || rel.Value.ToLowerInvariant() == "true") return leftNorm;
                if (rel.Value == "'0'" || rel.Value.ToLowerInvariant() == "false") return ExprHelpers.NOT(leftNorm);
            }
            if (be.Op == "/=" && be.Right is LitExpr rel2)
            {
                var leftNorm = NormalizeBooleans(be.Left!);
                if (rel2.Value == "'1'" || rel2.Value.ToLowerInvariant() == "true") return ExprHelpers.NOT(leftNorm);
                if (rel2.Value == "'0'" || rel2.Value.ToLowerInvariant() == "false") return leftNorm;
            }
            return ExprHelpers.BIN(NormalizeBooleans(be.Left!), be.Op, NormalizeBooleans(be.Right!));
        }
        if (expr is NotExpr ne) return ExprHelpers.NOT(NormalizeBooleans(ne.Expr!));
        if (expr is MuxExpr me) return ExprHelpers.MUX(NormalizeBooleans(me.Cond!), NormalizeBooleans(me.T!), NormalizeBooleans(me.F!));
        return expr;
    }

    // ド・モルガンの法則でNOTを葉まで押し下げ (接点列を平坦化)
    public Expr PushNotDown(Expr expr, bool isNot = false)
    {
        if (expr is IdExpr ie) return isNot ? ExprHelpers.NOT(ie) : ie;
        if (expr is LitExpr le)
        {
            if (isNot)
            {
                if (ExprHelpers.IsTrueLit(le)) return ExprHelpers.LIT("'0'");
                if (ExprHelpers.IsFalseLit(le)) return ExprHelpers.LIT("'1'");
            }
            return le;
        }
        if (expr is NotExpr ne) return PushNotDown(ne.Expr!, !isNot);
        if (expr is BinExpr be)
        {
            if (be.Op == "AND" || be.Op == "OR")
            {
                var newOp = be.Op == "AND" ? "OR" : "AND";
                if (isNot)
                    return ExprHelpers.BIN(PushNotDown(be.Left!, true), newOp, PushNotDown(be.Right!, true));
                return ExprHelpers.BIN(PushNotDown(be.Left!, false), be.Op, PushNotDown(be.Right!, false));
            }
            if (be.Op == "XOR")
            {
                var expanded = ExprHelpers.BIN(
                    ExprHelpers.BIN(be.Left!, "AND", ExprHelpers.NOT(be.Right!)),
                    "OR",
                    ExprHelpers.BIN(ExprHelpers.NOT(be.Left!), "AND", be.Right!));
                return PushNotDown(expanded, isNot);
            }
        }
        if (expr is MuxExpr me)
        {
            var expanded = ExprHelpers.BIN(
                ExprHelpers.BIN(me.Cond!, "AND", me.T!),
                "OR",
                ExprHelpers.BIN(ExprHelpers.NOT(me.Cond!), "AND", me.F!));
            return PushNotDown(expanded, isNot);
        }
        return expr;
    }

    // 変数の使用頻度を数え上げ
    public void CountContacts(Expr expr, Dictionary<string, int> counts)
    {
        if (expr is IdExpr ie)
            counts[ie.Name] = counts.GetValueOrDefault(ie.Name) + 1;
        else if (expr is NotExpr ne && ne.Expr is IdExpr nie)
            counts[nie.Name] = counts.GetValueOrDefault(nie.Name) + 1;
        else if (expr is BinExpr be)
        {
            CountContacts(be.Left!, counts);
            CountContacts(be.Right!, counts);
        }
    }

    // 2Cリレー制約適合のためリピータリレーを挿入
    public Expr ApplyRelayConstraints(Expr expr, Dictionary<string, int> currentUsage, Dictionary<string, int> totalUsage, Dictionary<string, List<string>> repeaters)
    {
        if (expr is IdExpr ie) return AssignRepeater(ie.Name, currentUsage, totalUsage, repeaters);
        if (expr is NotExpr ne && ne.Expr is IdExpr nie) return ExprHelpers.NOT(AssignRepeater(nie.Name, currentUsage, totalUsage, repeaters));
        if (expr is BinExpr be)
            return ExprHelpers.BIN(
                ApplyRelayConstraints(be.Left!, currentUsage, totalUsage, repeaters),
                be.Op,
                ApplyRelayConstraints(be.Right!, currentUsage, totalUsage, repeaters));
        return expr;
    }

    // 定数伝播による不要枝の削除
    public Expr Simplify(Expr expr)
    {
        if (expr is NotExpr ne)
        {
            var inner = Simplify(ne.Expr!);
            if (inner is LitExpr l)
            {
                if (ExprHelpers.IsTrueLit(l)) return ExprHelpers.LIT("'0'");
                if (ExprHelpers.IsFalseLit(l)) return ExprHelpers.LIT("'1'");
            }
            return ExprHelpers.NOT(inner);
        }
        if (expr is BinExpr be)
        {
            var left = Simplify(be.Left!);
            var right = Simplify(be.Right!);
            if (be.Op == "AND")
            {
                if (ExprHelpers.IsFalseLit(left) || ExprHelpers.IsFalseLit(right)) return ExprHelpers.LIT("'0'");
                if (ExprHelpers.IsTrueLit(left)) return right;
                if (ExprHelpers.IsTrueLit(right)) return left;
            }
            else if (be.Op == "OR")
            {
                if (ExprHelpers.IsTrueLit(left) || ExprHelpers.IsTrueLit(right)) return ExprHelpers.LIT("'1'");
                if (ExprHelpers.IsFalseLit(left)) return right;
                if (ExprHelpers.IsFalseLit(right)) return left;
            }
            return ExprHelpers.BIN(left, be.Op, right);
        }
        if (expr is MuxExpr me) return ExprHelpers.MUX(Simplify(me.Cond!), Simplify(me.T!), Simplify(me.F!));
        return expr;
    }

    private Expr AssignRepeater(string originalName, Dictionary<string, int> currentUsage, Dictionary<string, int> totalUsage, Dictionary<string, List<string>> repeaters)
    {
        if (!currentUsage.ContainsKey(originalName)) currentUsage[originalName] = 0;
        var usageIndex = currentUsage[originalName]++;
        var total = totalUsage.GetValueOrDefault(originalName, 0);

        if (total <= 2) return ExprHelpers.ID(originalName);
        if (usageIndex == 0) return ExprHelpers.ID(originalName);

        var repIndex = (usageIndex - 1) / 2 + 1;
        var repName = $"{originalName}_rep{repIndex}";

        if (!repeaters.ContainsKey(originalName)) repeaters[originalName] = new();
        if (!repeaters[originalName].Contains(repName)) repeaters[originalName].Add(repName);
        return ExprHelpers.ID(repName);
    }
}
