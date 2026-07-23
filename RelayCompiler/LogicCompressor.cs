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
// VHDL -> Relay Compiler - LogicCompressor (Quine-McCluskey)
// ============================================================

namespace RelayCompiler;

public class LogicCompressor
{
    public Expr Compress(Expr expr)
    {
        var varsSet = new HashSet<string>();
        ExtractVars(expr, varsSet);
        var vars = varsSet.OrderBy(v => v).ToList();

        if (vars.Count == 0 || vars.Count > 10) return expr;

        var numVars = vars.Count;
        var numCombinations = 1 << numVars;
        var minterms = new List<string>();

        for (int i = 0; i < numCombinations; i++)
        {
            var env = new Dictionary<string, bool>();
            for (int v = 0; v < numVars; v++)
                env[vars[v]] = ((i >> v) & 1) == 1;
            if (Evaluate(expr, env))
            {
                var m = "";
                for (int v = 0; v < numVars; v++)
                    m += env[vars[v]] ? '1' : '0';
                minterms.Add(m);
            }
        }

        if (!minterms.Any()) return ExprHelpers.LIT("'0'");
        if (minterms.Count == numCombinations) return ExprHelpers.LIT("'1'");

        var primes = GetPrimeImplicants(minterms);
        var best = GetMinimumCoverage(primes, minterms);
        return BuildExpr(best, vars);
    }

    private void ExtractVars(Expr expr, HashSet<string> vars)
    {
        if (expr is IdExpr ie) vars.Add(ie.Name);
        else if (expr is NotExpr ne) ExtractVars(ne.Expr!, vars);
        else if (expr is BinExpr be) { ExtractVars(be.Left!, vars); ExtractVars(be.Right!, vars); }
        else if (expr is MuxExpr me) { ExtractVars(me.Cond!, vars); ExtractVars(me.T!, vars); ExtractVars(me.F!, vars); }
    }

    private bool Evaluate(Expr expr, Dictionary<string, bool> env)
    {
        if (expr is IdExpr ie) return env.TryGetValue(ie.Name, out var v) && v;
        if (expr is LitExpr le) return le.Value == "'1'" || le.Value.ToLowerInvariant() == "true";
        if (expr is NotExpr ne) return !Evaluate(ne.Expr!, env);
        if (expr is BinExpr be)
        {
            var lv = Evaluate(be.Left!, env);
            var rv = Evaluate(be.Right!, env);
            return be.Op switch
            {
                "AND" => lv && rv,
                "OR" => lv || rv,
                "XOR" => lv != rv,
                "=" => be.Right is LitExpr brel && Evaluate(be.Left!, env) == (brel.Value == "'1'" || brel.Value.ToLowerInvariant() == "true"),
                "/=" => be.Right is LitExpr brel2 && Evaluate(be.Left!, env) != (brel2.Value == "'1'" || brel2.Value.ToLowerInvariant() == "true"),
                _ => false
            };
        }
        if (expr is MuxExpr me) return Evaluate(me.Cond!, env) ? Evaluate(me.T!, env) : Evaluate(me.F!, env);
        return false;
    }

    private HashSet<string> GetPrimeImplicants(List<string> minterms)
    {
        var current = new HashSet<string>(minterms);
        var primes = new HashSet<string>();

        while (current.Count > 0)
        {
            var next = new HashSet<string>();
            var merged = new HashSet<string>();
            var list = current.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var m = Merge(list[i], list[j]);
                    if (m != null)
                    {
                        next.Add(m);
                        merged.Add(list[i]);
                        merged.Add(list[j]);
                    }
                }
            }
            foreach (var term in list)
                if (!merged.Contains(term)) primes.Add(term);
            current = next;
        }
        return primes;
    }

    private string? Merge(string a, string b)
    {
        int diffCount = 0;
        var result = "";
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) { diffCount++; result += '-'; }
            else result += a[i];
        }
        return diffCount == 1 ? result : null;
    }

    private List<string> GetMinimumCoverage(HashSet<string> primes, List<string> minterms)
    {
        var remainingMinterms = new HashSet<string>(minterms);
        var primeList = primes.ToList();
        var selected = new List<string>();

        // 主座標項の抽出
        foreach (var minterm in minterms)
        {
            var covering = primeList.Where(p => Covers(p, minterm)).ToList();
            if (covering.Count == 1)
            {
                var essential = covering[0];
                if (!selected.Contains(essential))
                {
                    selected.Add(essential);
                    foreach (var m in minterms)
                        if (Covers(essential, m)) remainingMinterms.Remove(m);
                }
            }
        }

        if (remainingMinterms.Count == 0) return selected;

        var remainingPrimeList = primeList.Where(p => !selected.Contains(p)).ToList();
        var best = new List<string>(primeList);
        FindMinCoverage(remainingPrimeList, remainingMinterms.ToList(), new(), 0, best);
        selected.AddRange(best);
        return selected;
    }

    private void FindMinCoverage(List<string> primes, List<string> remainingMinterms, List<string> currentSelection, int index, List<string> best)
    {
        if (!remainingMinterms.Any())
        {
            if (currentSelection.Count < best.Count) best.Clear();
            foreach (var s in currentSelection) best.Add(s);
            return;
        }
        if (index >= primes.Count || currentSelection.Count >= best.Count) return;

        // 現在の主座標項を含まない場合
        FindMinCoverage(primes, remainingMinterms, currentSelection, index + 1, best);

        // 現在の主座標項を含める場合
        var p = primes[index];
        var newlyCovered = remainingMinterms.Where(m => Covers(p, m)).ToList();
        if (newlyCovered.Any())
        {
            currentSelection.Add(p);
            var newRemaining = remainingMinterms.Where(m => !newlyCovered.Contains(m)).ToList();
            FindMinCoverage(primes, newRemaining, currentSelection, index + 1, best);
            currentSelection.RemoveAt(currentSelection.Count - 1);
        }
    }

    private bool Covers(string prime, string minterm)
    {
        for (int i = 0; i < prime.Length; i++)
            if (prime[i] != '-' && prime[i] != minterm[i]) return false;
        return true;
    }

    private Expr BuildExpr(List<string> primes, List<string> vars)
    {
        Expr? result = null;
        foreach (var prime in primes)
        {
            Expr? term = null;
            for (int i = 0; i < prime.Length; i++)
            {
                if (prime[i] == '-') continue;
                Expr v = prime[i] == '0' ? ExprHelpers.NOT(ExprHelpers.ID(vars[i])) : ExprHelpers.ID(vars[i]);
                term = term == null ? v : ExprHelpers.BIN(term, "AND", v);
            }
            if (term == null) term = ExprHelpers.LIT("'1'");
            result = result == null ? term : ExprHelpers.BIN(result, "OR", term);
        }
        return result ?? ExprHelpers.LIT("'0'");
    }
}
