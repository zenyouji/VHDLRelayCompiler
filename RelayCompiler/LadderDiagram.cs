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
// VHDL -> Relay Compiler - LadderDiagram model & テキストレンダラ
// ============================================================

namespace RelayCompiler;

public record LadderContact(string Name, bool Negated, string? IsConst);
public abstract class LadderElem { public abstract string Type { get; } }
public class ContactElem : LadderElem { public override string Type => "contact"; public LadderContact Contact { get; set; } = null!; }
public class ParallelElem : LadderElem { public override string Type => "parallel"; public List<List<LadderElem>> Branches { get; set; } = new(); }

public record LadderRung(string Coil, List<List<LadderElem>> Branches, Expr Expr, string? Comment);

public static class LadderDiagram
{
    public static List<LadderRung> BuildLadderModel(Dictionary<string, Expr> equations, Dictionary<string, List<string>> repeaters)
    {
        var rungs = new List<LadderRung>();
        var contactOf = (string name) => new List<List<LadderElem>> { new() { new ContactElem { Contact = new LadderContact(name, false, null) } } };

        // 入力ポートのリピータ
        foreach (var (master, reps) in repeaters)
        {
            if (!equations.ContainsKey(master))
                foreach (var rep in reps)
                    rungs.Add(new LadderRung(rep, contactOf(master), ExprHelpers.ID(master), $"{master} のリピータ (入力)"));
        }

        foreach (var (coil, expr) in equations)
        {
            if (expr is IdExpr ie && ie.Name == coil) continue;
            rungs.Add(new LadderRung(coil, ExprToBranches(expr), expr, null));
            if (repeaters.ContainsKey(coil))
                foreach (var rep in repeaters[coil])
                    rungs.Add(new LadderRung(rep, contactOf(coil), ExprHelpers.ID(coil), $"{coil} のリピータ"));
        }
        return rungs;
    }

    public static List<LadderElem> ExprToSeries(Expr term)
    {
        var andTerms = new List<Expr>();
        FlattenUtils.FlattenAnd(term, andTerms);
        var elems = new List<LadderElem>();
        foreach (var c in andTerms)
        {
            if (c is IdExpr ie) elems.Add(new ContactElem { Contact = new LadderContact(ie.Name, false, null) });
            else if (c is NotExpr ne && ne.Expr is IdExpr nie)
                elems.Add(new ContactElem { Contact = new LadderContact(nie.Name, true, null) });
            else if (c is LitExpr le)
                elems.Add(new ContactElem
                {
                    Contact = new LadderContact("", false,
                        ExprHelpers.IsTrueLit(le) ? "short" : "open")
                });
            else if (c is BinExpr be && be.Op == "OR")
                elems.Add(new ParallelElem { Branches = ExprToBranches(be) });
            else elems.Add(new ContactElem { Contact = new LadderContact("?", false, null) });
        }
        return elems;
    }

    public static List<List<LadderElem>> ExprToBranches(Expr expr)
    {
        var orTerms = new List<Expr>();
        FlattenUtils.FlattenOr(expr, orTerms);
        return orTerms.Select(t => ExprToSeries(t)).ToList();
    }

    public static string LadderToText(List<LadderRung> rungs)
    {
        var lines = new List<string>();
        foreach (var rung in rungs)
        {
            lines.Add($"// {rung.Coil}用の段{(rung.Comment != null ? $"  ({rung.Comment})" : "")}");
            var branchLines = rung.Branches.Select(branch => "--" + SeriesToText(branch) + "--").ToList();
            var maxWidth = branchLines.Max(b => b.Length) + 4;
            for (int i = 0; i < branchLines.Count; i++)
            {
                var branch = branchLines[i];
                var padding = new string('-', Math.Max(0, maxWidth - branch.Length));
                if (i == 0) lines.Add($"|{branch}{padding}( {rung.Coil} )");
                else { lines.Add("|"); lines.Add($"|{branch}{padding}|"); }
            }
            lines.Add("");
        }
        return string.Join("\n", lines);
    }

    private static string SeriesToText(List<LadderElem> elems)
    {
        return string.Join("--", elems.Select(e =>
        {
            if (e is ContactElem ce)
            {
                var c = ce.Contact;
                if (c.IsConst == "short") return "[ SHORT ]";
                if (c.IsConst == "open") return "[ OPEN  ]";
                return c.Negated ? $"[ /{c.Name} ]" : $"[  {c.Name}  ]";
            }
            if (e is ParallelElem pe)
                return $"<{{ {string.Join("  ||  ", pe.Branches.Select(b => SeriesToText(b)))} }}>";
            return "";
        }));
    }
}
