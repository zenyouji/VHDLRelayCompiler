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
// VHDL -> Relay Compiler - Synthesizer
// ============================================================

namespace RelayCompiler;

public class Synthesizer
{
    public Dictionary<string, Expr> Equations { get; } = new();

    public void Synthesize(DesignUnit design)
    {
        foreach (var sig in design.Architecture.Signals)
            Equations[sig.Name] = ExprHelpers.ID(sig.Name);
        foreach (var port in design.Entity.Ports)
            Equations[port.Name] = ExprHelpers.ID(port.Name);

        var rootCondition = ExprHelpers.LIT("true");
        foreach (var stmt in design.Architecture.Body)
            ProcessStatement(stmt, rootCondition);
    }

    private void ProcessStatement(Statement stmt, Expr currentCondition)
    {
        if (stmt is AssignStmt assign)
        {
            var target = assign.Target;
            var currentValue = Equations.TryGetValue(target, out var cur) ? cur : ExprHelpers.ID(target);
            if (ExprHelpers.IsTrueLit(currentCondition))
                Equations[target] = assign.Value!;
            else
                Equations[target] = ExprHelpers.MUX(currentCondition, assign.Value!, currentValue);
        }
        else if (stmt is ProcessStmt proc)
        {
            foreach (var s in proc.Body) ProcessStatement(s, currentCondition);
        }
        else if (stmt is IfStmt iff)
        {
            var trueCond = And(currentCondition, iff.Cond!);
            foreach (var s in iff.TrueBlock) ProcessStatement(s, trueCond);

            var falseCond = And(currentCondition, ExprHelpers.NOT(iff.Cond!));

            foreach (var (eCond, block) in iff.Elsifs)
            {
                var elsifCond = And(falseCond, eCond);
                foreach (var s in block) ProcessStatement(s, elsifCond);
                falseCond = And(falseCond, ExprHelpers.NOT(eCond));
            }

            foreach (var s in iff.ElseBlock) ProcessStatement(s, falseCond);
        }
        else if (stmt is CaseStmt cas)
        {
            Expr remainingCond = currentCondition;
            foreach (var (conds, block) in cas.Whens)
            {
                var first = conds[0];
                if (first is IdExpr fe && fe.Name.ToLowerInvariant() == "others")
                {
                    foreach (var s in block) ProcessStatement(s, remainingCond);
                }
                else
                {
                    var matchCond = ExprHelpers.BIN(cas.Target!, "=", first);
                    var activeCond = And(remainingCond, matchCond);
                    foreach (var s in block) ProcessStatement(s, activeCond);
                    remainingCond = And(remainingCond, ExprHelpers.NOT(matchCond));
                }
            }
        }
    }

    private Expr And(Expr a, Expr b)
    {
        if (ExprHelpers.IsTrueLit(a)) return b;
        if (ExprHelpers.IsTrueLit(b)) return a;
        return ExprHelpers.BIN(a, "AND", b);
    }

    private Expr Not(Expr a)
    {
        if (a is NotExpr ne) return ne.Expr!;
        return ExprHelpers.NOT(a);
    }
}
