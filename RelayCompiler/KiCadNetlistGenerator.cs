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
// VHDL -> Relay Compiler - KiCadNetlistGenerator
// ============================================================

namespace RelayCompiler;

public record Net(string Name, List<(string Comp, string Pin)> Nodes);

public class ContactAllocator
{
    private readonly Dictionary<string, int> _usage = new();
    private int _coilIdx = 0;

    public (string C, string A, string B) Allocate(string relayName)
    {
        if (!_usage.ContainsKey(relayName)) _usage[relayName] = 0;
        _usage[relayName]++;
        _coilIdx += 1;
        var count = _usage[relayName];
        string c = _coilIdx == 1 || _coilIdx == 2 ? "C1" : "C2";
        string a = count <= 2 ? "A1" : "A2";
        string b = count <= 2 ? "B1" : "B2";
        if (count > 2) throw new Exception($"リレー {relayName} の接点が不足!");
        return (c, a, b);
    }
}

public static class KiCadNetlistGenerator
{
    public static string Generate(Dictionary<string, Expr> equations, Dictionary<string, List<string>> repeaters, List<string> inPorts, List<string> outPorts)
    {
        var nets = new Dictionary<string, Net>();
        void AddNode(string netName, string comp, string pin)
        {
            if (!nets.ContainsKey(netName)) nets[netName] = new Net(netName, new List<(string, string)>());
            nets[netName] = new Net(netName, nets[netName].Nodes.Append((comp, pin)).ToList());
        }

        var allRelays = new HashSet<string>();

        void CollectIds(Expr e, HashSet<string> outSet)
        {
            if (e is IdExpr ie) outSet.Add(ie.Name);
            else if (e is NotExpr ne) CollectIds(ne.Expr!, outSet);
            else if (e is BinExpr be) { CollectIds(be.Left!, outSet); CollectIds(be.Right!, outSet); }
        }

        // 必要なリレーの一覧作成
        foreach (var (key, value) in equations)
        {
            if (!outPorts.Contains(key)) allRelays.Add(key);
            var ids = new HashSet<string>();
            CollectIds(value, ids);
            foreach (var name in ids)
                if (inPorts.Contains(name)) allRelays.Add(name);
        }
        foreach (var (master, reps) in repeaters)
        {
            allRelays.Add(master);
            foreach (var rep in reps) allRelays.Add(rep);
        }

        // 電源コネクタ
        AddNode("POW_P", "J_PWR", "POW_P");
        AddNode("POW_N", "J_PWR", "POW_N");

        // 入力コネクタおよび入力リレー駆動
        for (int i = 0; i < inPorts.Count; i++)
        {
            var inNet = $"IN_{inPorts[i]}";
            AddNode(inNet, "J_IN", (i + 1).ToString());
            if (allRelays.Contains(inPorts[i]))
            {
                AddNode(inNet, $"RLY_{inPorts[i]}", "L1");
                AddNode("POW_N", $"RLY_{inPorts[i]}", "L2");
            }
        }

        // 全リレーコイル(L2)をGNDに接続
        foreach (var r in allRelays)
            if (!inPorts.Contains(r)) AddNode("POW_N", $"RLY_{r}", "L2");

        // リピータリレー駆動回路
        var allocator = new ContactAllocator();
        foreach (var (master, reps) in repeaters)
        {
            var (c, a, b) = allocator.Allocate(master);
            var driveNet = $"Net_Drive_{master}_REPs";
            AddNode("POW_P", $"RLY_{master}", c);
            AddNode(driveNet, $"RLY_{master}", a);
            foreach (var rep in reps) AddNode(driveNet, $"RLY_{rep}", "L1");
        }

        // ロジック回路の接線配線
        int netCount = 0;
        void WireExpr(Expr e, string fromNet, string toNet)
        {
            if (e is BinExpr be && be.Op == "OR")
            {
                var orTerms = new List<Expr>();
                FlattenUtils.FlattenOr(e, orTerms);
                foreach (var t in orTerms) WireExpr(t, fromNet, toNet);
                return;
            }
            if (e is BinExpr be2 && be2.Op == "AND")
            {
                var andTerms = new List<Expr>();
                FlattenUtils.FlattenAnd(e, andTerms);
                var curr = fromNet;
                for (int i = 0; i < andTerms.Count; i++)
                {
                    var next = (i == andTerms.Count - 1) ? toNet : $"Net_Logic_{++netCount}";
                    WireExpr(andTerms[i], curr, next);
                    curr = next;
                }
                return;
            }
            string rlyName = "";
            bool isA = true;
            if (e is IdExpr ie2) { rlyName = ie2.Name; isA = true; }
            else if (e is NotExpr ne2 && ne2.Expr is IdExpr nie2) { rlyName = nie2.Name; isA = false; }
            else return;

            var (c, a, b) = allocator.Allocate(rlyName);
            AddNode(fromNet, $"RLY_{rlyName}", c);
            AddNode(toNet, $"RLY_{rlyName}", isA ? a : b);
        }

        // 式を接点に配線
        foreach (var (target, value) in equations)
        {
            if (value is IdExpr ie3 && ie3.Name == target) continue;

            var isOut = outPorts.Contains(target);
            var destNet = isOut ? $"OUT_{target}" : $"Net_Coil_{target}_L1";

            if (isOut) AddNode(destNet, "J_OUT", (outPorts.IndexOf(target) + 1).ToString());
            else AddNode(destNet, $"RLY_{target}", "L1");

            if (value is LitExpr le && ExprHelpers.IsTrueLit(le))
            {
                if (isOut) AddNode("POW_P", "J_OUT", (outPorts.IndexOf(target) + 1).ToString());
                else AddNode("POW_P", $"RLY_{target}", "L1");
                continue;
            }
            if (value is LitExpr) continue;

            WireExpr(value, "POW_P", destNet);
        }

        // netlistテキスト生成
        var outLines = new List<string>();
        outLines.Add("(export (version D)");
        outLines.Add("  (design");
        outLines.Add("    (source \"LogicSynthesis.sch\")");
        outLines.Add("    (date \"Automated\")");
        outLines.Add("    (tool \"RelaySynth (C#)\")");
        outLines.Add("  )");
        outLines.Add("  (components");
        outLines.Add("    (comp (ref \"J_PWR\") (value \"POWER_CONN\") (libsource (lib \"Connector\") (part \"Conn_01x02\")) )");
        outLines.Add("    (comp (ref \"J_IN\") (value \"INPUT_CONN\") (libsource (lib \"Connector\") (part \"Conn_01x" + inPorts.Count + "\")))");
        outLines.Add("    (comp (ref \"J_OUT\") (value \"OUTPUT_CONN\") (libsource (lib \"Connector\") (part \"Conn_01x" + outPorts.Count + "\")))");
        foreach (var r in allRelays)
            outLines.Add("    (comp (ref \"RLY_" + r + "\") (value \"Relay_2C\") (libsource (lib \"Relay\") (part \"Relay_DPDT\")) )");
        outLines.Add("  )");
        outLines.Add("  (nets");
        int netIdx = 1;
        foreach (var net in nets.Values)
        {
            if (net.Nodes.Count == 0) continue;
            outLines.Add("    (net (code \"" + netIdx++ + "\") (name \"" + net.Name + "\")");
            foreach (var node in net.Nodes)
                outLines.Add("      (node (ref \"" + node.Comp + "\") (pin \"" + node.Pin + "\"))");
            outLines.Add("    )");
        }
        outLines.Add("  )");
        outLines.Add(")");
        return string.Join("\n", outLines);
    }
}
