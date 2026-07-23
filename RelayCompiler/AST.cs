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
// VHDL -> RelayCompiler (C# port) - AST & ユーティリティ
// ============================================================

namespace RelayCompiler;

// ---- AST 式型 ----
public abstract class Expr
{
    public abstract string Kind { get; }
}
public class IdExpr : Expr    { public override string Kind => "id";    public string Name { get; set; } = ""; }
public class LitExpr : Expr   { public override string Kind => "lit";   public string Value { get; set; } = ""; }
public class BinExpr : Expr   { public override string Kind => "bin";   public Expr? Left { get; set; } = null; public string Op { get; set; } = ""; public Expr? Right { get; set; } = null; }
public class CallExpr : Expr  { public override string Kind => "call";  public string Func { get; set; } = ""; public Expr? Arg { get; set; } = null; }
public class NotExpr : Expr   { public override string Kind => "not";   public Expr? Expr { get; set; } = null; }
public class MuxExpr : Expr   { public override string Kind => "mux";   public Expr? Cond { get; set; } = null; public Expr? T { get; set; } = null; public Expr? F { get; set; } = null; }

// ---- 文型 ----
public abstract class Statement { public abstract string Kind { get; } }
public class AssignStmt : Statement { public override string Kind => "assign"; public string Target { get; set; } = ""; public Expr? Value { get; set; } = null; }
public class IfStmt : Statement { public override string Kind => "if"; public Expr? Cond { get; set; } = null; public List<Statement> TrueBlock { get; set; } = new(); public List<(Expr Cond, List<Statement> Block)> Elsifs { get; set; } = new(); public List<Statement> ElseBlock { get; set; } = new(); }
public class CaseStmt : Statement { public override string Kind => "case"; public Expr? Target { get; set; } = null; public List<(List<Expr> Conds, List<Statement> Block)> Whens { get; set; } = new(); }
public class ProcessStmt : Statement { public override string Kind => "process"; public List<string> Sens { get; set; } = new(); public List<Statement> Body { get; set; } = new(); }

// ---- Entity/Architecture/Signal/Port ----
public record PortDef(string Name, string Direction, string DataType);
public record SignalDef(string Name, string DataType);
public record DesignUnit(EntityData Entity, ArchData Architecture);
public record EntityData(string Name, List<PortDef> Ports);
public record ArchData(string Name, string EntityName, List<SignalDef> Signals, List<Statement> Body);

// ---- FlipFlop ----
public record FlipFlop(string SignalName, string Clock, Expr DInput, Expr? ResetCondition, string ResetValue, bool IsAsync);

// ---- 補助関数 ----
public static class ExprHelpers
{
    public static bool IsTrueLit(Expr e) => e is LitExpr l && (l.Value.ToLower() == "true" || l.Value == "'1'");
    public static bool IsFalseLit(Expr e) => e is LitExpr l && (l.Value.ToLower() == "false" || l.Value == "'0'");

    public static IdExpr ID(string name) => new() { Name = name };
    public static LitExpr LIT(string value) => new() { Value = value };
    public static BinExpr BIN(Expr left, string op, Expr right) => new() { Left = left, Op = op, Right = right };
    public static NotExpr NOT(Expr expr) => new() { Expr = expr };
    public static MuxExpr MUX(Expr cond, Expr t, Expr f) => new() { Cond = cond, T = t, F = f };
    public static CallExpr CALL(string func, Expr arg) => new() { Func = func, Arg = arg };
}

// ---- Flattenユーティリティ ----
public static class FlattenUtils
{
    public static void FlattenOr(Expr expr, List<Expr> list)
    {
        if (expr is BinExpr b && b.Op == "OR") { FlattenOr(b.Left!, list); FlattenOr(b.Right!, list); }
        else list.Add(expr);
    }
    public static void FlattenAnd(Expr expr, List<Expr> list)
    {
        if (expr is BinExpr b && b.Op == "AND") { FlattenAnd(b.Left!, list); FlattenAnd(b.Right!, list); }
        else list.Add(expr);
    }
    public static void FlattenAndOr(Expr expr, List<Expr> list)
    {
        if (expr is BinExpr b && (b.Op == "AND" || b.Op == "OR")) { FlattenAndOr(b.Left!, list); FlattenAndOr(b.Right!, list); }
        else list.Add(expr);
    }
}
