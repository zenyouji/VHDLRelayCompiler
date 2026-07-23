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
// VHDL -> Relay Compiler - RelayMapper
// ============================================================

using System.Text;

namespace RelayCompiler;

public record RelayMapResult(
    Dictionary<string, Expr> ConstrainedEquations,
    Dictionary<string, List<string>> Repeaters,
    (int LogicCoils, int InputCoils, int RepeaterCoils, int Total) Stats,
    List<LadderRung> LadderRungs,
    string KiCadText,
    List<string> Warnings);

public class RelayMapper
{
    public RelayMapResult MapToRelays(Dictionary<string, Expr> equations, List<string> inPorts, List<string> outPorts)
    {
        var optimizer = new RelayOptimizer();
        var compressor = new LogicCompressor();
        var factorizer = new Factorizer();
        var finalEquations = new Dictionary<string, Expr>();
        var totalUsageCounts = new Dictionary<string, int>();
        var repeaters = new Dictionary<string, List<string>>();

        foreach (var (key, value) in equations)
        {
            var normalized = optimizer.NormalizeBooleans(value);
            var simplified = optimizer.Simplify(normalized);
            var compressed = compressor.Compress(simplified);
            var pushDown = optimizer.PushNotDown(compressed);
            var factored = factorizer.Factor(pushDown);
            finalEquations[key] = factored;
            optimizer.CountContacts(factored, totalUsageCounts);
        }

        var constrainedEquations = new Dictionary<string, Expr>();
        var currentUsage = new Dictionary<string, int>();
        foreach (var (key, value) in finalEquations)
            constrainedEquations[key] = optimizer.ApplyRelayConstraints(value, currentUsage, totalUsageCounts, repeaters);

        int logicCoils = 0;
        foreach (var (key, value) in constrainedEquations)
            if (!(value is IdExpr ie && ie.Name == key)) logicCoils++;
        var inputCoils = totalUsageCounts.Keys.Count(k => !constrainedEquations.ContainsKey(k));
        var repeaterCoils = repeaters.Values.Sum(l => l.Count);
        var total = logicCoils + inputCoils + repeaterCoils;

        var ladderRungs = LadderDiagram.BuildLadderModel(constrainedEquations, repeaters);
        var warnings = new List<string>();

        string kicadText;
        try
        {
            kicadText = KiCadNetlistGenerator.Generate(constrainedEquations, repeaters, inPorts, outPorts);
        }
        catch (Exception ex)
        {
            warnings.Add($"KiCad netlist生成に失敗: {ex.Message}");
            kicadText = "; ERROR: " + ex.Message;
        }

        return new RelayMapResult(constrainedEquations, repeaters, (logicCoils, inputCoils, repeaterCoils, total), ladderRungs, kicadText, warnings);
    }
}
