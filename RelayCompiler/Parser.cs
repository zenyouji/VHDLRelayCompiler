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
// VHDL -> Relay Compiler - Parser (再帰下降構文解析)
// ============================================================

namespace RelayCompiler;

public class Parser
{
    private readonly Token[] _tokens;
    private int _pos = 0;

    public Parser(Token[] tokens) { _tokens = tokens; }

    private Token Peek(int offset = 0)
    {
        var i = Math.Min(_pos + offset, _tokens.Length - 1);
        return _tokens[i];
    }
    private Token Consume() => _tokens[_pos++];
    private void Expect(string value)
    {
        var t = Consume();
        if (t.Value.ToLowerInvariant() != value.ToLowerInvariant())
            throw new Exception($"'{value}'を期待したが、'{t.Value}'が見つかった");
    }
    private bool Match(string value)
    {
        if (Peek().Value.ToLowerInvariant() == value.ToLowerInvariant()) { Consume(); return true; }
        return false;
    }

    public DesignUnit Parse()
    {
        // library/use節をスキップ
        while (Match("library") || Match("use"))
        {
            while (Peek().Value != ";" && Peek().Type != "EOF") Consume();
            Consume();
        }
        var entity = ParseEntity();
        var architecture = ParseArchitecture();
        return new DesignUnit(entity, architecture);
    }

    private EntityData ParseEntity()
    {
        Expect("entity");
        var name = Consume().Value;
        Expect("is");
        var ports = new List<PortDef>();

        if (Match("port"))
        {
            Expect("(");
            while (Peek().Value != ")")
            {
                var names = new List<string> { Consume().Value };
                while (Peek().Value == ",") { Consume(); names.Add(Consume().Value); }
                Expect(":");
                var dir = Consume().Value;
                var typeTokens = new List<string>();
                int parenDepth = 0;
                while (true)
                {
                    var p = Peek().Value;
                    if (parenDepth == 0 && (p == ";" || p == ")")) break;
                    if (p == "(") parenDepth++;
                    if (p == ")") parenDepth--;
                    typeTokens.Add(Consume().Value);
                }
                foreach (var n in names)
                    ports.Add(new PortDef(n, dir, string.Join(" ", typeTokens)));
                if (Peek().Value == ";") Consume();
            }
            Expect(")");
            Expect(";");
        }
        Expect("end");
        Match("entity");
        if (Peek().Value.ToLowerInvariant() == name.ToLowerInvariant()) Consume();
        Expect(";");
        return new EntityData(name, ports);
    }

    private ArchData ParseArchitecture()
    {
        Expect("architecture");
        var archName = Consume().Value;
        Expect("of");
        var entityName = Consume().Value;
        Expect("is");

        var signals = new List<SignalDef>();
        while (Match("signal"))
        {
            var names = new List<string> { Consume().Value };
            while (Peek().Value == ",") { Consume(); names.Add(Consume().Value); }
            Expect(":");
            var typeTokens = new List<string>();
            while (Peek().Value != ";" && Peek().Type != "EOF") typeTokens.Add(Consume().Value);
            Consume(); // ';'
            foreach (var n in names)
                signals.Add(new SignalDef(n, string.Join(" ", typeTokens)));
        }

        Expect("begin");
        var statements = new List<Statement>();
        while (!Match("end"))
        {
            if (Peek().Type == "EOF") throw new Exception("architecture体で予期せぬEOF");
            statements.Add(ParseStatement());
        }

        if (Peek().Type != "EOF")
        {
            Match("architecture");
            var archLower = archName.ToLowerInvariant();
            if (Peek().Value.ToLowerInvariant() == archLower) Consume();
            if (Peek().Value == ";") Consume();
        }
        return new ArchData(archName, entityName, signals, statements);
    }

    private Statement ParseStatement()
    {
        if (Match("process")) return ParseProcess();
        if (Peek().Type == "Identifier" && Peek(1).Value == "<=") return ParseAssignment();
        throw new Exception($"文の開始で予期せぬトークン: {Peek().Value}");
    }

    private Statement ParseProcess()
    {
        var sens = new List<string>();
        if (Match("("))
        {
            while (Peek().Value != ")")
            {
                sens.Add(Consume().Value);
                if (Peek().Value == ",") Consume();
            }
            Expect(")");
        }
        Expect("begin");
        var body = new List<Statement>();
        while (!Match("end"))
        {
            if (Match("process")) break;
            if (Peek().Type == "EOF") throw new Exception("process体で予期せぬEOF");
            body.Add(ParseProcessBodyStatement());
        }
        if (Peek().Value == "process") Consume();
        Expect(";");
        return new ProcessStmt { Sens = sens, Body = body };
    }

    private Statement ParseProcessBodyStatement()
    {
        if (Match("if")) return ParseIf();
        if (Match("case")) return ParseCase();
        if (Peek().Type == "Identifier" && Peek(1).Value == "<=") return ParseAssignment();
        throw new Exception($"process文で予期せぬトークン: {Peek().Value}");
    }

    private Statement ParseIf()
    {
        var cond = ParseExpression();
        Expect("then");
        var trueBlock = ParseStatementsUntil(["elsif", "else", "end"]);

        var elsifs = new List<(Expr Cond, List<Statement> Block)>();
        while (Match("elsif"))
        {
            var eCond = ParseExpression();
            Expect("then");
            elsifs.Add((eCond, ParseStatementsUntil(["elsif", "else", "end"])));
        }

        List<Statement> elseBlock = new();
        if (Match("else")) elseBlock = ParseStatementsUntil(["end"]);

        Expect("end"); Expect("if"); Expect(";");
        return new IfStmt { Cond = cond, TrueBlock = trueBlock, Elsifs = elsifs, ElseBlock = elseBlock };
    }

    private Statement ParseCase()
    {
        var target = ParseExpression();
        Expect("is");
        var whens = new List<(List<Expr> Conds, List<Statement> Block)>();

        while (Match("when"))
        {
            var conds = new List<Expr> { ParseExpression() };
            Expect("=>");
            var block = ParseStatementsUntil(["when", "end"]);
            whens.Add((conds, block));
        }
        Expect("end"); Expect("case"); Expect(";");
        return new CaseStmt { Target = target, Whens = whens };
    }

    private Statement ParseAssignment()
    {
        var target = Consume().Value;
        Expect("<=");
        var value = ParseExpression();

        // when-else条件付き信号代入 (process外)
        if (Match("when"))
        {
            var cond = ParseExpression();
            Expect("else");
            var elseValue = ParseExpression();
            Expect(";");
            return new IfStmt
            {
                Cond = cond,
                TrueBlock = new List<Statement> { new AssignStmt { Target = target, Value = value } },
                Elsifs = new(),
                ElseBlock = new List<Statement> { new AssignStmt { Target = target, Value = elseValue } }
            };
        }

        Expect(";");
        return new AssignStmt { Target = target, Value = value };
    }

    // ---- 式解析 (演算子の優先順位) ----
    // 論理(and/or/xor/nand/nor/xnor) < 比較(=,/=,<,>,<=,>=) < 加減算(+,-) < 単項not < 基本
    private Expr ParseExpression() => ParseLogical();

    private Expr ParseLogical()
    {
        Expr left = ParseRelational();
        while (true)
        {
            var v = Peek().Value.ToLowerInvariant();
            if (Peek().Type == "Keyword" && IsLogicalOp(v))
            {
                Consume();
                var right = ParseRelational();
                if (v == "and") left = ExprHelpers.BIN(left, "AND", right);
                else if (v == "or") left = ExprHelpers.BIN(left, "OR", right);
                else if (v == "xor") left = ExprHelpers.BIN(left, "XOR", right);
                else if (v == "nand") left = ExprHelpers.NOT(ExprHelpers.BIN(left, "AND", right));
                else if (v == "nor") left = ExprHelpers.NOT(ExprHelpers.BIN(left, "OR", right));
                else left = ExprHelpers.NOT(ExprHelpers.BIN(left, "XOR", right)); // xnor
            }
            else break;
        }
        return left;
    }

    private bool IsLogicalOp(string v)
    {
        return v == "and" || v == "or" || v == "xor" || v == "nand" || v == "nor" || v == "xnor";
    }

    private Expr ParseRelational()
    {
        var left = ParseAdditive();
        var v = Peek().Value;
        if (Peek().Type == "Symbol" && IsRelationalOp(v))
        {
            Consume();
            var right = ParseAdditive();
            return ExprHelpers.BIN(left, v, right);
        }
        return left;
    }

    private bool IsRelationalOp(string v)
    {
        return v == "=" || v == "/=" || v == "<" || v == ">" || v == "<=" || v == ">=";
    }

    private Expr ParseAdditive()
    {
        Expr left = ParseUnary();
        while (Peek().Type == "Symbol" && (Peek().Value == "+" || Peek().Value == "-"))
        {
            var op = Consume().Value;
            var right = ParseUnary();
            left = ExprHelpers.BIN(left, op, right);
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (Peek().Type == "Keyword" && Peek().Value == "not")
        {
            Consume();
            return ExprHelpers.NOT(ParseUnary());
        }
        return ParsePrimary();
    }

    private Expr ParsePrimary()
    {
        if (Match("("))
        {
            var e = ParseExpression();
            Expect(")");
            return e;
        }
        if (Peek().Type == "Literal") return ExprHelpers.LIT(Consume().Value);
        if (Peek().Type == "Identifier")
        {
            var name = Consume().Value;
            if (Match("("))
            {
                var arg = ParseExpression();
                Expect(")");
                return ExprHelpers.CALL(name, arg);
            }
            return ExprHelpers.ID(name);
        }
        // "others"などのキーワードも identifiersとして扱う
        return ExprHelpers.ID(Consume().Value);
    }

    private List<Statement> ParseStatementsUntil(string[] stopWords)
    {
        var stmts = new List<Statement>();
        while (!stopWords.Any(w => w.ToLowerInvariant() == Peek().Value.ToLowerInvariant()))
        {
            if (Peek().Type == "EOF") throw new Exception("予期せぬEOF");
            stmts.Add(ParseProcessBodyStatement());
        }
        return stmts;
    }
}
