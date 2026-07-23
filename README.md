# VHDL → Relay Compiler (C#)

VHDLソースコードからリレー論理回路（ラダーダイアグラムおよびKiCad用ネットリスト）を生成するC#製合成ツールです。

リレー電卓/リレーコンピュータの自作にどうぞ。

**[ここ](https://lab.discretek.com/vhdl_relay_compiler/) にTypescript実装版があります。こちらはシミュレーション機能まで実装されているため、まずはtypescript版で試して頂くのが良いかと思います。**

## 概要

このツールは、ハードウェア記述言語VHDLで記述された組み合わせ論理および时序回路を解析し、リレー接点による実現回路へと変換します。出力はKiCad EDA用ネットリスト（`.sch`ベース）およびラダーダイアグラムのテキスト図として生成されます。

---

## システム要件

- **.NET 10.0 SDK** (または .NET 8.0 SDK)
- Windows / macOS / Linux (any platform supporting .NET)

---

## ビルド方法

```bash
cd RelayCompiler
dotnet build
```

ビルド成功時に `bin/Debug/net10.0/RelayCompiler.dll` および `RelayCompiler.exe` (Windows) が出力されます。

実行例:

```bash
dotnet run
dotnet run -- -i design.vhdl -o circuit.net
dotnet run -- --input design.vhdl --netlist circuit.net --verbose
```

---

## CLI オプション

| フラグ | 説明 |
|--------|------|
| `-i, --input <file>` | VHDL入力ファイルのパス |
| `-o, --netlist <file>` | KiCadネットリスト出力ファイルのパス |
| `-v, --verbose` | 各処理ステージの詳細ログを標準出力 |
| `-h, --help` | ヘルプを表示 |

### 使用例

外部VHDLファイルを指定してネットリストに出力:

```bash
dotnet run -- -i counter.vhdl -o output.net
```

verboseモードで中間状態の確認:

```bash
dotnet run -- -i design.vhdl --verbose
```

---

## サポートされるVHDL構文

### 対応構文

| カテゴリ | サポート構文 |
|---------|-------------|
| **宣言** | `entity`, `architecture`, `port`, `signal` |
| **演算子** | `AND`, `OR`, `NOT`, `XOR`, `=` , `/=`, `<`, `>`, `<=`, `>=`, `+`, `-` |
| **リテラル** | `'1'`, `'0'` (2値), `"0011"` (ビット列), `X"AB"` (16進) |
| **制御** | `if` / `elsif` / `else`, `when ... else` (条件付き代入) |
| **时序** | `rising_edge(clk)`, ポゼッジ検出 |
| **プロセス** | `process(sens_list) ... end process` |

### 未サポート

| カテゴリ | 備考 |
|---------|------|
| `case` / `when ... others` | パースはするがsynthesize対象にしない |
| 配列代入 (`<=` によるベクタ全体代入) | ビットごとの展開は行うが複雑な代入は制限あり |
| ファンクション/プロシージャ | インライン関数はサポート外 |
| generate文 | 未対応 |

---

## 内部パイプライン

以下は、VHDL入力から網リスト出力に至る11の処理ステージです。

```
VHDL Source
    │
    ▼
┌─────────────┐
│  1. LEXER   │  字句解析: ソースをトークン列に分解
└──────┬──────┘
       ▼
┌─────────────┐
│   2. PARSER │  構文解析: 再帰降下法でASTを構築
└──────┬──────┘
       ▼
┌─────────────┐
│   3. WIDTH  │  幅マッピング: ポート/シグナルのビット幅を推定
└──────┬──────┘
       ▼
┌───────────┐
│ 4. SYNTH  │  論理合成: 等価式を抽出
└────┬──────┘
     ▼
┌───────────┐
│ 5. OPT    │  最適化: 定数畳み込み、簡約
└────┬──────┘
     ▼
┌───────────┐
│6. BITBLAST│  ビット展開: ベクタ演算を1ビット単位に分解
└────┬──────┘
     ▼
┌──────────────────┐
│7 . TECHNOLOGY    │  技術マッピング: FF分離 + クワインマクラスキー法による論理簡単化
│    MAPPING       │
└────┬─────────────┘
     ▼
┌──────────┐
│8. RELAY  │  リレーマッピング: 接点制約適用
└────┬─────┘
     ▼
┌──────────────────┐
│   9. LADDER      │  ラダー図の構築
└────┬─────────────┘
     ▼
┌───────────────────┐
│   10. KICAD NET   │  KiCadネットリストの生成
└────┬──────────────┘
     ▼
┌───────────────────┐
│  11. SIMULATION   │  回路シミュレーション用式の出力 (シミュレータは未実装)
└───────────────────┘
```

### ステージ1: LEXER（字句解析）

ソースコードをトークンに分解/変換します。以下のトークン型を認識:

- **Keyword**: VHDLキーワード (`entity`, `process`, `if`...`else`, `and`, `or`, `not`, `rising_edge`...)
- **Identifier**: シグナル名・ポート名
- **Literal**: 数値リテラル、2値リテラル (`'0'`/`'1'`)、ビット列 (`"0011"`)、16進 (`X"AB"`)
- **Symbol**: 演算子・区切り子 (`<=`, `=`, `+`, `(`, `)`, `;`)

コメント (`--` 行末まで) と空白は全てスキップされます。

### ステージ2: PARSER（構文解析）

再帰降下パーサーでAST (Abstract Syntax Tree) を構築します。

**対応演算子の優先順位**（低い → 高い）:

| 優先度 | 演算子 | 結合性 |
|--------|--------|--------|
| 1 | `OR` | 左結合 |
| 2 | `AND` | 左結合 |
| 3 | `/=`, `=`, `<`, `<=`, `>`, `>=` | 左結合 |
| 4 | `NOT` | 単項（右結合） |
| 5 | `XOR` | 左結合 |
| 6 | `+`, `-` | 左結合 |

`when ... else` の条件付き代入はMux式として表現されます。

### ステージ3: ビット幅マッピング

`STD_LOGIC_VECTOR(n downto m)` の `n - m + 1` を計算し、全ポート/シグナルのビット幅を決定します。`STD_LOGIC` タイプは幅1として扱います。

### ステージ4: SYNTHESIZER（論理合成）

ASTの文をスキャンし、各信号のの等価式 (`Expr`) を生成します。

- **プロセス文**: clock信号 (`rising_edge`) と制御信号を抽出
  - `if rising_edge(clk)` → clock edgeを検出
  - `rising_edge()`: `CallExpr` に変換
- **代入文**: 右辺の式を `BinExpr`, `NotExpr`, `MuxExpr` として保存
- **if文**: (逐次)条件をMux式としてネスト化
- **when...else**: Mux式として表現 (`MUX(cond, T, F)`)

結果として、各シグナルに対応する `Equations<Dictionary<string, Expr>>` を生成します。

### ステージ5: OPTIMIZER（論理最適化）

式の簡略化を再帰的に適用します:

| 最適化ルール | 変換前 | 変換後 |
|-------------|--------|--------|
| 二重否定解除 | `NOT(NOT(x))` | `x` |
| 定数畳み込み | `AND(x, '1')` | `x` |
| 定数畳み込み | `AND(x, '0')` | `'0'` |
| 定数畳み込み | `OR(x, '1')` | `'1'` |
| 比較定数化 | `='1' AND = '1'` | 定数判定結果 |
| AND鎖簡略化 | 矛盾する条件の削除 | 矛盾項を消去 |
| MUX簡約 | `MUX(NOT(C), A, MUX(C, B, D))` | `MUX(C, B, A)` |
| MUX簡約 | `MUX(true, A, B)` | `A` |

### ステージ6: BITBLASTER（ビット展開）

ベクタ演算を1ビットレベルの論理式に分解します。

**主要なビット演算変換**:

- **加算 (`+`)**: 1ビット全加算器を生成 (XOR + ANDの連鎖)
- **減算 (`-`)**: 2の補数に変換
- **比較 (`=`, `/=`, `<`, `<=`, `>`, `>=`)**:
  - `=` → 全ビットのXNORをANDで結合
  - `<` → MSBから逐次比較 (MSB > LSB優先)
- **リテラル変換**:
  - `X"AB"` → `10101011` (2進数展開)
  - `"0011"` → `0011` (文字列として保持)
- **`rising_edge(clk)`** → `posedge(clk)` に置換

### ステージ7: TECHNOLOGY MAPPING

Dフリップフロップ(F/F)回路への変換を行います。

**2つのパス**:

| Pass | 処理内容 |
|------|---------|
| Pass 1 | clock-edge式 (`posedge(...)`) からFFを識別, FFと組み合わせロジックを分離 |
| Pass 2 | ワンショット・回路, FF保持回路, 組み合わせロジックを再構築 |

**FF抽出アルゴリズム**:

1. clock信号の立ち上がり検出パターン (ワンショット):
   ```
   clk_PLS = clk AND NOT(clk_PREV)
   ```
2. FFの同期/非同期リセットを検出:
   - クロック停止時に値が収束 → **非同期リセット**
   - クロック立ち上がり時のみ収束 → **同期リセット**
3. リセット条件付きのDインライン表現:
   ```
   Q = (NOT(rst) AND new_state) OR (rst AND reset_val)
   ```
4. **Quine-McCluskey法**でD入力を最小化 (最大10変数まで)

**Quine-McCluskey実装**:

- エントリ数: `2^n` (nは変数数)
- 全ての入力組み合わせで式の評価を行い、1となる組み合わせ (minterm) を抽出
- 隣接するmintermのマージで素項 (prime implicant) を生成
- 最小被覆問題 (essential + greedy) で最適な素項を選択
- 生成した素項からAND-ORの素式を構成

### ステージ8: RELAY MAPPING（リレー制約適用）

ロジック式をリレー接点構成に変換します。

``ここを変更したら、例えばMinecraftのRedStone回路を出力したりできるかも？``

**制約**: 2接点リレーのみ使用 (A接点1 + B接点1) → 各変数の使用回数は最大2回 (2C DPDTリレーの制約)

**処理フロー**:

1. Boolean正規化: 全NOTを下位へプッシュ (De-Morgan法則適用)
2. 接点数のカウント: 各変数が式内でどの程度出現するかを計測
3. 制約適用: 使用回数が2を超える変数にリレー (中継器) を挿入
4. 接点割り当て: 通常接点と逆接点の最適ペアリング

### ステージ9: LADDER DIAGRAM（ラダー図構築）

リレー制約を満たす接点配置からラダーダイアグラムを生成します。

| 要素 | 表現 |
|------|------|
| 直列 | AND |
| 並列 | OR |
| 通常接点 | `[ name ]` |
| 逆接点 | `[ /name ]` |
| 電源 | short |
| 接地 | open |

**テキスト出力フォーマットの例**:

```
// count[0]用の段
|--[ NOT count[2] ]--[  count[1]  ]--[  count[0]  ]----( count[0] )
|
|--[    count[2]   ]--[  count[1]  ]------( count[0]     )
```

### ステージ10: KICAD NETLIST（KiCadネットリスト生成）

KiCad EDAで読み可能なネットリストファイル (`*.sch` / デプレケッド: `*.net`) を生成します。
一般的なプリント基板CADでもインポートできることを期待しています。

**生成構成**:

| コンポーネント | 内容 |
|---------------|------|
| 電源コネクタ | `J_PWR`: 2ピン |
| 入力コネクタ | `J_IN`: スイッチ入力用 |
| 出力コネクタ | `J_OUT`: リレー出力用 |
| スイッチ | `SW_*`: 各入力信号をスイッチとして配置 |
| コイル | `RLY_*`: リレーコイル (2接点タイプ) |
| 接点 | `C1`, `C2` (通常), `A1`, `A2` / `B1`, `B2` (逆) |

**接点ピン割当**:

| ピン | 機能 |
|------|------|
| C1 | 共通接点 |
| A1, B1 | C1に接続されたA/B接点 |
| C2 | 共通接点 |
| A2, B2 | C2に接続されたA/B接点 |

### ステージ11: SIMULATION EQUATIONS（シミュレーション方程式）

最終的な回路方程式を全て出力します。ラダー図の各段について、対応する式を示します。
現状シミュレーション機能は実装されていません。

---

## モジュール構成

```
RelayCompiler/
├── AST.cs              -- ASTノード型定義 & 補助関数
│   Expr (抽象)                    -- 式の基本抽象クラス
│   ├── IdExpr                   -- 変数名
│   ├── LitExpr                  -- リテラル
│   ├── BinExpr                  -- 2項演算子
│   ├── CallExpr                 -- 関数呼び出し
│   ├── NotExpr                  -- 否定
│   └── MuxExpr                  -- 多路選択器
│   Statement (抽象)             -- 文の基本抽象クラス
│   ├── AssignStmt               -- 代入文
│   ├── IfStmt                   -- 条件分岐
│   ├── CaseStmt                 -- 場合分け
│   └── ProcessStmt              -- プロセス文
├── Program.cs        -- エントリポイント & CLI引数解析
├── Lexer.cs          -- 字句解析器
├── Parser.cs         -- 構文解析器 (再帰降下法)
├── Synthesizer.cs    -- 論理合成 (AST → 等価式)
├── Optimizer.cs      -- 論理最適化 (定数畳み込みなど)
├── OptimizeUtils.cs  -- 最適化補助ユーティリティ
├── BitBlaster.cs     -- ビット展開 (ベクタ → 1ビット分解)
├── LogicCompressor.cs -- Quine-McCluskey 圧縮
├── TechnologyMapper.cs -- FF分離 + 技術マッピング
├── RelayOptimizer.cs   -- 接点最適化 & 制約適用
├── RelayMapper.cs      -- リレーマッピング統合
├── Factorizer.cs       -- 因数分解 / 積和形正規化
├── LadderDiagram.cs    -- ラダー図構築 & テキスト描画
├── KiCadNetlistGenerator.cs -- KiCadネットリスト出力
└── LogicPrinter.cs     -- 式のテキストレンダリング
```

---

## 使用フロー

```
design.vhdl ──► コンパイラ ──► circuit.net ──► KiCadで開く ──► 回路図表示
                          │
                          └──► stdout (ラダー図 + 統計)
```

### 1. VHDLファイルの準備

```vhdl
-- example.vhdl
entity Counter is
    Port (
        clk    : in STD_LOGIC;
        rst    : in STD_LOGIC;
        enable : in STD_LOGIC;
        q      : out STD_LOGIC_VECTOR(3 downto 0)
    );
end Counter;

architecture RTL of Counter is
    signal count : STD_LOGIC_VECTOR(3 downto 0);
begin
    process(clk)
    begin
        if rising_edge(clk) then
            if rst = '1' then
                count <= "0000";
            elsif enable = '1' then
                count <= count + 1;
            end if;
        end if;
    end process;

    q <= count;
end RTL;
```

### 2. コンパイル実行

```bash
dotnet run -- -i example.vhdl -o output.net --verbose
```

### 3. 出力の解釈

- **relay-statistics**: 必要なリレー総数と内訳を表示
- **ladder-diagram**: ラダーダイアグラムのテキスト図
- **kicad-netlist**: KiCad用ネットリスト (XML形式)

KiCadで `output.net` を開くと、配線済の回路図が表示されます。

---

## 統計出力フォーマット

```
=== RELAY USAGE STATISTICS === (Entity: counter)
Logic/State Relays : 10 coils (Registers & Outputs)
Input Buffer Relays: 3 coils
Repeater Relays    : 27 coils (For 2C Constraints)
------------------------------------------------
TOTAL RELAYS NEEDED: 40 relays (All 2C type)
```

| 項目 | 説明 |
|------|------|
| Logic/State Relays | FF(レジスタ)出力用のリレーコイル |
| Input Buffer Relays | 入力信号をバッファリングするリレー |
| Repeater Relays | 2接点制約超過時の中継用リレー |
| Total | リレー総数 (すべて2接点タイプ) |

---

## 制限事項

- **Max変数数 (QM圧縮)**: 10変数まで (それ以上は原始式を保持)
- **时序制約**: clock信号は単一に限る (multi-clock未対応)
- **プリスケール**: 2進リテラルのみ (VHDLの `2#1010#` は未対応)
- **エントリポイント**: トップレベル `Main` を持たないため `dotnet run` 必須


# Author

* **Kaoru Zenyouji** - [@kaoruzen](https://x.com/kaoruzen) / [Discretek Inc.](https://www.discretek.com/)

## License

This software is released under the MIT License,