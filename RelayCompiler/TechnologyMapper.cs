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
// VHDL -> Relay Compiler - TechnologyMapper
// ============================================================

using System.Text;

namespace RelayCompiler;

public class TechnologyMapper
{
    public static (Dictionary<string, Expr> MappedEquations, string ReportText) MapToHardware(Dictionary<string, Expr> bitBlastedEquations)
    {
        var flipFlops = new List<FlipFlop>();
        var combinationalLogics = new Dictionary<string, Expr>();
        var optimizer = new Optimizer();
        var compressor = new LogicCompressor();
        var clocks = new HashSet<string>();

        // 式から変数名を再帰的に抽出
        void ExtractVars(Expr e, HashSet<string> set)
        {
            if (e is IdExpr ie) set.Add(ie.Name);
            else if (e is NotExpr ne) ExtractVars(ne.Expr!, set);
            else if (e is BinExpr be) { ExtractVars(be.Left!, set); ExtractVars(be.Right!, set); }
            else if (e is MuxExpr me) { ExtractVars(me.Cond!, set); ExtractVars(me.T!, set); ExtractVars(me.F!, set); }
            else if (e is CallExpr ce) ExtractVars(ce.Arg!, set);
        }

        // Pass 1: FFと組み合わせLogicを分離
        foreach (var (sigName, expr) in bitBlastedEquations)
        {
            var exprStr = LogicPrinter.ToString(expr);

            if (exprStr.Contains("posedge"))
            {
                string clkName = "clk";
                var m = System.Text.RegularExpressions.Regex.Match(exprStr, @"posedge\(([^)]+)\)");
                if (m.Success) clkName = m.Groups[1].Value;
                clocks.Add(clkName);

                var targetPosedge = $"posedge({clkName})";
                var inactiveExpr = optimizer.Optimize(Substitute(expr, targetPosedge, "false"));
                var activeExpr = optimizer.Optimize(Substitute(expr, targetPosedge, "true"));

                Expr? resetCond = null;
                string resetVal = "'0'";
                bool isAsync = false;
                Expr dInput = activeExpr;

                var vars = new HashSet<string>();
                ExtractVars(expr, vars);

                var sortedVars = vars.OrderBy(v =>
                {
                    var aIsRst = System.Text.RegularExpressions.Regex.IsMatch(v, "(rst|reset|clr|clear)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    return aIsRst ? 0 : 1;
                }).ToList();

                foreach (var rstName in sortedVars)
                {
                    if (rstName.ToLowerInvariant() == clkName.ToLowerInvariant() ||
                        rstName.ToLowerInvariant() == clkName.ToLowerInvariant() + "_prev" ||
                        rstName.ToLowerInvariant() == clkName.ToLowerInvariant() + "_pls")
                        continue;

                    foreach (var testVal in new[] { "'1'", "'0'" })
                    {
                        var oppositeVal = testVal == "'1'" ? "'0'" : "'1'";

                        // 非同期リセット: クロック停止時に収束
                        var rstActiveInactive = optimizer.Optimize(Substitute(inactiveExpr, rstName, testVal));
                        if (rstActiveInactive is LitExpr rai && (rai.Value == "'0'" || rai.Value == "'1'"))
                        {
                            isAsync = true;
                            resetCond = ExprHelpers.BIN(ExprHelpers.ID(rstName), "=", ExprHelpers.LIT(testVal));
                            resetVal = rai.Value;
                            dInput = optimizer.Optimize(Substitute(activeExpr, rstName, oppositeVal));
                            break;
                        }

                        // 同期リセット: クロック立ち上がり時のみ収束
                        var rstActiveActive = optimizer.Optimize(Substitute(activeExpr, rstName, testVal));
                        if (rstActiveActive is LitExpr raa && (raa.Value == "'0'" || raa.Value == "'1'"))
                        {
                            resetCond = ExprHelpers.BIN(ExprHelpers.ID(rstName), "=", ExprHelpers.LIT(testVal));
                            resetVal = raa.Value;
                            dInput = optimizer.Optimize(Substitute(activeExpr, rstName, oppositeVal));
                            break;
                        }
                    }
                    if (resetCond != null) break;
                }

                var compressedDInput = compressor.Compress(dInput);
                flipFlops.Add(new FlipFlop(sigName, clkName, compressedDInput, resetCond, resetVal, isAsync));
            }
            else
            {
                combinationalLogics[sigName] = compressor.Compress(expr);
            }
        }

        // Pass 2: 実行順序を保証しつつmappedEquationsを構築
        var mappedEquations = new Dictionary<string, Expr>();

        // 1. クロック立ち上がり検出パルス回路 (ワンショット)
        foreach (var clk in clocks)
            mappedEquations[clk + "_PLS"] = ExprHelpers.BIN(ExprHelpers.ID(clk), "AND", ExprHelpers.NOT(ExprHelpers.ID(clk + "_PREV")));

        // 2. FFラッチ回路
        foreach (var ff in flipFlops)
        {
            var clkPulse = ExprHelpers.ID(ff.Clock + "_PLS");
            var hold = ExprHelpers.ID(ff.SignalName);
            Expr simExpr = ExprHelpers.BIN(
                ExprHelpers.BIN(clkPulse, "AND", ff.DInput),
                "OR",
                ExprHelpers.BIN(ExprHelpers.NOT(clkPulse), "AND", hold));
            simExpr = compressor.Compress(simExpr);

            if (ff.ResetCondition != null)
            {
                var rstNorm = ff.ResetCondition!;
                simExpr = ff.ResetValue == "'1'"
                    ? ExprHelpers.BIN(rstNorm, "OR", ExprHelpers.BIN(ExprHelpers.NOT(rstNorm), "AND", simExpr))
                    : ExprHelpers.BIN(ExprHelpers.NOT(rstNorm), "AND", simExpr);
                simExpr = compressor.Compress(simExpr);
            }
            mappedEquations[ff.SignalName] = simExpr;
        }

        // 3. 組み合わせLogic
        foreach (var (name, e) in combinationalLogics)
            mappedEquations[name] = e;

        // 4. クロック状態保持回路
        foreach (var clk in clocks)
            mappedEquations[clk + "_PREV"] = ExprHelpers.ID(clk);

        // リポートテキスト
        var lines = new List<string>();
        lines.Add("=============================================");
        lines.Add("===  LOGIC GATE LEVEL NETLIST (HARDWARE)  ===");
        lines.Add("=============================================");
        lines.Add("");
        lines.Add("[ Registers (D-FlipFlops) ]");
        foreach (var ff in flipFlops)
        {
            var typeStr = ff.IsAsync ? "非同期リセット" : "同期リセット";
            var rstStr = ff.ResetCondition != null
                ? $", {typeStr}: {LogicPrinter.ToString(ff.ResetCondition!)} (Value: {ff.ResetValue})"
                : "";
            lines.Add($"D-FF: {ff.SignalName}");
            lines.Add($"  Clock: {ff.Clock}{rstStr}");
            lines.Add($"  D-Input = \n{LogicPrinter.ToString(ff.DInput, 1)}");
            lines.Add("");
        }
        lines.Add("[ Combinational Wires (Logics) ]");
        foreach (var (name, e) in combinationalLogics)
        {
            lines.Add("");
            lines.Add($"{name} = \n{LogicPrinter.ToString(e, 1)}");
        }

        return (mappedEquations, string.Join("\n", lines));
    }

    private static Expr Substitute(Expr expr, string targetName, string val)
    {
        return expr switch
        {
            IdExpr ie => ie.Name.ToLowerInvariant() == targetName.ToLowerInvariant() ? ExprHelpers.LIT(val) : ie,
            NotExpr ne => ExprHelpers.NOT(Substitute(ne.Expr!, targetName, val)),
            BinExpr be => ExprHelpers.BIN(Substitute(be.Left!, targetName, val), be.Op, Substitute(be.Right!, targetName, val)),
            MuxExpr me => ExprHelpers.MUX(Substitute(me.Cond!, targetName, val), Substitute(me.T!, targetName, val), Substitute(me.F!, targetName, val)),
            CallExpr ce => ExprHelpers.CALL(ce.Func, Substitute(ce.Arg!, targetName, val)),
            _ => expr
        };
    }
}
