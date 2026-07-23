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
// VHDL -> Relay Compiler - Factorizer (代数的因数分解)
// ============================================================

namespace RelayCompiler;

public class Factorizer
{
    public Expr Factor(Expr expr)
    {
        var cubes = ToCubes(expr);
        if (cubes == null) return expr;
        return FactorCubes(cubes);
    }

    private List<HashSet<string>>? ToCubes(Expr expr)
    {
        var orTerms = new List<Expr>();
        FlattenUtils.FlattenOr(expr, orTerms);
        var cubes = new List<HashSet<string>>();
        foreach (var term in orTerms)
        {
            var andTerms = new List<Expr>();
            FlattenUtils.FlattenAnd(term, andTerms);
            var cube = new HashSet<string>();
            bool contradictory = false;
            foreach (var lit in andTerms)
            {
                if (lit is IdExpr ie)
                {
                    if (cube.Contains('!' + ie.Name)) { contradictory = true; break; }
                    cube.Add(ie.Name);
                }
                else if (lit is NotExpr ne && ne.Expr is IdExpr nie)
                {
                    if (cube.Contains(nie.Name)) { contradictory = true; break; }
                    cube.Add('!' + nie.Name);
                }
                else return null;
            }
            if (!contradictory) cubes.Add(cube);
        }
        if (!cubes.Any()) return null;
        return cubes;
    }

    private Expr FactorCubes(List<HashSet<string>> cubes)
    {
        cubes = Absorb(cubes);

        if (!cubes.Any()) return ExprHelpers.LIT("'0'");
        if (cubes.Any(c => c.Count == 0)) return ExprHelpers.LIT("'1'");
        if (cubes.Count == 1) return CubeToExpr(cubes[0]);

        // 最頻出リテラルを検出
        var counts = new Dictionary<string, int>();
        foreach (var c in cubes) foreach (var l in c) counts[l] = counts.GetValueOrDefault(l) + 1;
        string? bestLit = null;
        int bestCount = 1;
        foreach (var l in counts.Keys.OrderByDescending(k => counts[k]).ThenBy(k => k))
        {
            var n = counts[l];
            if (n > bestCount) { bestCount = n; bestLit = l; }
        }

        if (bestLit == null)
        {
            var cubeExprs = cubes.Select(c => CubeToExpr(c)).ToList();
            if (cubeExprs.Count == 1) return cubeExprs[0];
            return cubeExprs.Aggregate((a, b) => ExprHelpers.BIN(a, "OR", b));
        }

        // bestLitを含む立方体の共通因子(約数)を検出
        var withLit = cubes.Where(c => c.Contains(bestLit)).ToList();
        var rest = cubes.Where(c => !c.Contains(bestLit)).ToList();
        var divisor = new HashSet<string>(withLit[0]);
        foreach (var c in withLit)
            foreach (var l in divisor.ToList())
                if (!c.Contains(l)) divisor.Remove(l);

        // 商 = 各立方体から約数を除いたもの
        var quotient = withLit.Select(c => new HashSet<string>(c.Where(l => !divisor.Contains(l)))).ToList();

        var result = AndExpr(CubeToExpr(divisor), FactorCubes(quotient));
        if (rest.Any()) result = ExprHelpers.BIN(result, "OR", FactorCubes(rest));
        return result;
    }

    private List<HashSet<string>> Absorb(List<HashSet<string>> cubes)
    {
        var kept = new List<HashSet<string>>();
        for (int i = 0; i < cubes.Count; i++)
        {
            bool absorbed = false;
            for (int j = 0; j < cubes.Count; j++)
            {
                if (i == j) continue;
                if (cubes[j].Count <= cubes[i].Count && cubes[j].ContainsAll(cubes[i]))
                {
                    if (cubes[j].Count < cubes[i].Count || j < i) { absorbed = true; break; }
                }
            }
            if (!absorbed) kept.Add(cubes[i]);
        }
        return kept;
    }

    private Expr CubeToExpr(HashSet<string> cube)
    {
        var lits = cube.OrderBy(x => x).ToList();
        Expr? result = null;
        foreach (var l in lits)
        {
            Expr e = l.StartsWith('!') ? ExprHelpers.NOT(ExprHelpers.ID(l.Substring(1))) : ExprHelpers.ID(l);
            result = result == null ? e : ExprHelpers.BIN(result, "AND", e);
        }
        return result ?? ExprHelpers.LIT("'1'");
    }

    private Expr AndExpr(Expr a, Expr b)
    {
        if (ExprHelpers.IsTrueLit(a)) return b;
        if (ExprHelpers.IsTrueLit(b)) return a;
        if (ExprHelpers.IsFalseLit(a) || ExprHelpers.IsFalseLit(b)) return ExprHelpers.LIT("'0'");
        return ExprHelpers.BIN(a, "AND", b);
    }
}

public static class HashSetExtensions
{
    public static bool ContainsAll<T>(this HashSet<T> set, IEnumerable<T> other)
    {
        foreach (var item in other)
            if (!set.Contains(item)) return false;
        return true;
    }
    public static Expr Aggregate(this IEnumerable<Expr> items, Func<Expr, Expr, Expr> func)
    {
        var e = items.GetEnumerator();
        e.MoveNext();
        var result = e.Current;
        while (e.MoveNext()) result = func(result, e.Current);
        return result;
    }
}
