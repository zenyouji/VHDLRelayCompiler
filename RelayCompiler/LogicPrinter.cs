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
// VHDL -> Relay Compiler - LogicPrinter
// ============================================================

namespace RelayCompiler;

public static class LogicPrinter
{
    public static string ToString(Expr expr, int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 4);
        var next = new string(' ', (indentLevel + 1) * 4);
        return expr switch
        {
            IdExpr e => e.Name,
            LitExpr e => e.Value,
            BinExpr e => $"({ToString(e.Left!)} {e.Op} {ToString(e.Right!)})",
            CallExpr e => $"{e.Func}({ToString(e.Arg!)})",
            NotExpr e => $"NOT({ToString(e.Expr!)})",
            MuxExpr e => $"MUX(\n{next}cond:  {ToString(e.Cond!)},\n{next}true:  {ToString(e.T!, indentLevel + 1)},\n{next}false: {ToString(e.F!, indentLevel + 1)}\n{indent})",
            _ => "UNKNOWN"
        };
    }
}
