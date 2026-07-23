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

using RelayCompiler;

namespace RelayCompiler;

public static class Program
{
    // ---- ヘルプ表示 ----
    private static void ShowHelp()
    {
        Console.WriteLine(@"
----------------------------------------------------------------
VHDL -> Relay Compiler    
- by Kaoru Zenyouji (Discretek Inc.) - 2026
----------------------------------------------------------------

用法:
  RelayCompiler [オプション] VHDLファイル

オプション:
  -i, --input <file>     VHDL入力ファイルの指定 (デフォルト: 未設定)
  -o, --netlist <file>   KiCad netlistをファイルに出力
  -v, --verbose          各処理ステージの詳細を出力
  -h, --help             このヘルプを表示

例:
  RelayCompiler -i counter.vhdl -o circuit.net
  RelayCompiler --input mydesign.vhdl --verbose
  RelayCompiler --help
");
    }

    public static int Main(string[] args)
    {
        if (args.Length == 1 || (args.Length >= 2 && args[1].Equals("-h", StringComparison.OrdinalIgnoreCase)) || (args.Length >= 2 && args[1].Equals("--help", StringComparison.OrdinalIgnoreCase)))
        {
            ShowHelp();
            return 0;
        }

        var vhdlFile = GetArgValue(args, "-i", "--input") ?? GetPositionalArg(args, 0);
        var netlistFile = GetArgValue(args, "-o", "--netlist");
        var verbose = args.Contains("-v") || args.Contains("--verbose");

        if (vhdlFile == null || !File.Exists(vhdlFile))
        {
            Console.Error.WriteLine($"エラー: VHDLファイル '{vhdlFile}' が見つかりません。");
            ShowHelp();
            return 1;
        }

        var vhdlSource = File.ReadAllText(vhdlFile);
        var runner = new ProgramRunner(verbose, netlistFile);
        runner.Run(vhdlSource);
        return 0;
    }

    // ---- CLI パースヘルパ (ローカルで定義して Main からキャプチャ) ----
    private static string? GetArgValue(string[] a, params string[] shortFlags)
    {
        for (int i = 0; i < a.Length; i++)
        {
            var lower = a[i].ToLowerInvariant();
            foreach (var flag in shortFlags)
            {
                if (lower == flag && i + 1 < a.Length) return a[i + 1];
                if (lower.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase) && lower.Length > flag.Length + 1)
                    return lower.Substring(flag.Length + 1);
            }
        }
        return null;
    }

    private static string? GetPositionalArg(string[] a, int index)
    {
        var skip = new HashSet<string>(new[] { "-h", "--help", "-v", "--verbose", "-i", "--input", "-o", "--netlist", "-i=", "--input=", "-o=", "--netlist=" }, StringComparer.OrdinalIgnoreCase);
        var collected = new List<string>();
        foreach (var arg in a.Skip(1))
        {
            if (!skip.Contains(arg.ToLowerInvariant())) collected.Add(arg);
        }
        return collected.Count > index ? collected[index] : null;
    }
}

class ProgramRunner
{
    private readonly bool _verbose;
    private readonly string? _netlistFile;

    // ---- インライン埋め込み counter VHDL (サンプル) ----
    private const string InlineCounter =
        @"-- 4-bit counter with enable and synchronous reset
entity SampleCounter is
    Port (
        clk    : in STD_LOGIC;
        rst    : in STD_LOGIC;
        enable : in STD_LOGIC;
        q      : out STD_LOGIC_VECTOR(3 downto 0)
    );
end SampleCounter;

architecture RTL of SampleCounter is
    signal count : STD_LOGIC_VECTOR(3 downto 0);
begin
    process(clk)
    begin
        if rising_edge(clk) then
            if rst = '1' then
                count <= X""0"";
            elsif count = 9 then
                count <= ""0000"";
            else
                count <= count + 1;
            end if;
        end if;
    end process;

    q <= count when enable = '1' else ""0000"";
end RTL;
";

    public ProgramRunner(bool verbose, string? netlistFile)
    {
        _verbose = verbose;
        _netlistFile = netlistFile;
    }

    public void Run(string vhdlSource)
    {
        var warnings = new List<string>();

        // 外部ファイル指定なしの場合はサンプルを使用
        var source = vhdlSource == null ? InlineCounter : vhdlSource;

        // ---- bit-width 計算 ----
        int ParseBitWidth(string typeStr)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(typeStr, "vector", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                var m = System.Text.RegularExpressions.Regex.Match(typeStr, @"\(\s*(\d+)\s+downto\s+(\d+)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) return int.Parse(m.Groups[1].Value) - int.Parse(m.Groups[2].Value) + 1;
            }
            return 1;
        }

        // 1. Lex
        if (_verbose) Console.WriteLine("=== LEXER ===");
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        if (_verbose)
        {
            Console.WriteLine($"Tokens: {tokens.Length}");
            foreach (var t in tokens)
                Console.WriteLine($"  {t.Type,-10} {t.Value}");
        }

        // 2. Parse
        if (_verbose) Console.WriteLine("\n=== PARSER ===");
        var parser = new Parser(tokens);
        var ast = parser.Parse();
        if (_verbose)
        {
            Console.WriteLine($"Entity: {ast.Entity.Name}");
            Console.WriteLine("Ports:");
            foreach (var p in ast.Entity.Ports)
                Console.WriteLine($"  {p.Name} [{p.Direction}] ({p.DataType})");
            Console.WriteLine("Signals:");
            foreach (var s in ast.Architecture.Signals)
                Console.WriteLine($"  {s.Name} ({s.DataType})");
            Console.WriteLine($"Statements: {ast.Architecture.Body.Count}");
        }

        // 3. Build width map
        var widthMap = new Dictionary<string, int>();
        foreach (var p in ast.Entity.Ports)
            widthMap[p.Name.ToLowerInvariant()] = ParseBitWidth(p.DataType);
        foreach (var s in ast.Architecture.Signals)
            widthMap[s.Name.ToLowerInvariant()] = ParseBitWidth(s.DataType);
        if (_verbose)
        {
            Console.WriteLine("\nWidth map:");
            foreach (var (k, v) in widthMap)
                Console.WriteLine($"  {k} = {v}");
        }

        // 4. Synthesize
        if (_verbose) Console.WriteLine("\n=== SYNTHESIZER ===");
        var synth = new Synthesizer();
        synth.Synthesize(ast);
        var synthLines = new List<string>();
        foreach (var (key, value) in synth.Equations)
        {
            if (value is IdExpr id && id.Name == key) continue;
            synthLines.Add($"[{key}] = {LogicPrinter.ToString(value)}");
        }
        foreach (var line in synthLines) if (_verbose) Console.WriteLine(line);

        // 5. Optimize
        if (_verbose) Console.WriteLine("\n=== OPTIMIZER ===");
        var optimizer = new Optimizer();
        var optLines = new List<string>();
        foreach (var (key, value) in synth.Equations)
        {
            if (value is IdExpr id && id.Name == key) continue;
            optLines.Add($"[{key}] = {LogicPrinter.ToString(optimizer.Optimize(value))}");
        }
        foreach (var line in optLines) if (_verbose) Console.WriteLine(line);

        // 6. Bit blast
        if (_verbose) Console.WriteLine("\n=== BITBLASTER ===");
        var blaster = new BitBlaster(widthMap);
        var bitBlasted = new Dictionary<string, Expr>();
        foreach (var (signalName, rawEquation) in synth.Equations)
        {
            if (rawEquation is IdExpr ie && ie.Name == signalName) continue;
            var bitWidth = widthMap.TryGetValue(signalName.ToLowerInvariant(), out var w) ? w : 1;
            var optimizedBase = optimizer.Optimize(rawEquation);
            for (int i = 0; i < bitWidth; i++)
            {
                var bitExpr = blaster.ExtractBit(optimizedBase, i);
                var fullyOptimized = optimizer.Optimize(bitExpr);
                var lhs = bitWidth > 1 ? $"{signalName}[{i}]" : signalName;
                bitBlasted[lhs] = fullyOptimized;
            }
        }
        foreach (var (k, v) in bitBlasted) if (_verbose) Console.WriteLine($"  [{k}] = {LogicPrinter.ToString(v)}");

        // 7. Technology mapping (D-FF extraction + QM simplification)
        if (_verbose) Console.WriteLine("\n=== TECHNOLOGY MAPPING ===");
        var (mappedEquations, reportText) = TechnologyMapper.MapToHardware(bitBlasted);
        if (_verbose) Console.WriteLine(reportText);

        // 8. Relay mapping
        if (_verbose) Console.WriteLine("\n=== RELAY MAPPING ===");
        var inPorts = new List<string>();
        var outPorts = new List<string>();
        foreach (var port in ast.Entity.Ports)
        {
            var pw = ParseBitWidth(port.DataType);
            for (int i = 0; i < pw; i++)
            {
                var name = pw > 1 ? $"{port.Name}[{i}]" : port.Name;
                if (port.Direction.ToLowerInvariant() == "in") inPorts.Add(name);
                else if (port.Direction.ToLowerInvariant() == "out") outPorts.Add(name);
            }
        }
        if (_verbose)
        {
            Console.WriteLine($"In ports:  {string.Join(",", inPorts)}");
            Console.WriteLine($"Out ports: {string.Join(",", outPorts)}");
        }

        var relayMapper = new RelayMapper();
        var relayResult = relayMapper.MapToRelays(mappedEquations, inPorts, outPorts);
        warnings.AddRange(relayResult.Warnings);

        // Stats
        var stats = relayResult.Stats;
        Console.WriteLine($"\n=== RELAY USAGE STATISTICS === (Entity: {ast.Entity.Name})");
        Console.WriteLine($"Logic/State Relays : {stats.LogicCoils} coils (Registers & Outputs)");
        Console.WriteLine($"Input Buffer Relays: {stats.InputCoils} coils");
        Console.WriteLine($"Repeater Relays    : {stats.RepeaterCoils} coils (For 2C Constraints)");
        Console.WriteLine($"------------------------------------------------");
        Console.WriteLine($"TOTAL RELAYS NEEDED: {stats.Total} relays (All 2C type)");
        if (relayResult.Warnings.Any())
            Console.WriteLine($"Warnings: {string.Join("; ", relayResult.Warnings)}");

        // 9. Ladder text
        if (_verbose) Console.WriteLine("\n=== LADDER DIAGRAM ===");
        if (_verbose) Console.WriteLine(LadderDiagram.LadderToText(relayResult.LadderRungs));

        // 10. KiCad netlist (stdout + ファイル出力)
        if (_verbose) Console.WriteLine("\n=== KICAD NETLIST ===");
        Console.WriteLine(relayResult.KiCadText);

        // netlistファイル出力
        if (_netlistFile != null)
        {
            File.WriteAllText(_netlistFile, relayResult.KiCadText);
            Console.Error.WriteLine($"netlistをファイルに出力: {_netlistFile}");
        }

        // 11. Simulation equations
        if (_verbose) Console.WriteLine("\n=== SIMULATION EQUATIONS ===");
        if (_verbose)
            foreach (var rung in relayResult.LadderRungs)
                Console.WriteLine($"  {rung.Coil} = {LogicPrinter.ToString(rung.Expr)}");
    }
}
