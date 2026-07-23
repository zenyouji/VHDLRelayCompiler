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
// VHDL -> Relay Compiler - OptimizeUtils
// ============================================================

namespace RelayCompiler;

public static class OptimizeUtils
{
    public static void OptimizeLogic(Dictionary<string, Expr> equations)
    {
        var optimizer = new Optimizer();
        foreach (var kv in equations.ToList())
            equations[kv.Key] = optimizer.Optimize(kv.Value);
    }

    public static void LogicComp(Dictionary<string, Expr> equations, List<string> allPorts)
    {
        var compressor = new LogicCompressor();
        foreach (var kv in equations.ToList())
            equations[kv.Key] = compressor.Compress(kv.Value);
    }

    public static void LogicFactor(Dictionary<string, Expr> equations, List<string> allPorts)
    {
        var factorizer = new Factorizer();
        foreach (var (key, value) in equations)
            equations[key] = factorizer.Factor(value);
    }
}
