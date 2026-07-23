-- 非同期10進カウンタの例
-- 自己リセットを非同期で行うため、タイミングハザード(レースコンディション)が発生する例
-- 9->0リセット時にタイミングハザードが発生しq2FFが偽の立ち上がりエッジを捉え、4にリセットされる

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity AsyncDecadeCounter is
    Port (
        clk : in STD_LOGIC;
        rst : in STD_LOGIC;
        q0  : out STD_LOGIC;
        q1  : out STD_LOGIC;
        q2  : out STD_LOGIC;
        q3  : out STD_LOGIC
    );
end AsyncDecadeCounter;

architecture RTL of AsyncDecadeCounter is
    signal sig_q0 : STD_LOGIC;
    signal sig_q1 : STD_LOGIC;
    signal sig_q2 : STD_LOGIC;
    signal sig_q3 : STD_LOGIC;
    
    signal clk1 : STD_LOGIC;
    signal clk2 : STD_LOGIC;
    signal clk3 : STD_LOGIC;
    
    signal async_rst : STD_LOGIC;
begin
    -- リップルクロック生成（前段の反転出力を次段のクロックとする）
    clk1 <= not sig_q0;
    clk2 <= not sig_q1;
    clk3 <= not sig_q2;

    -- 10進カウンタのリセット条件: 外部リセット OR カウント10 (1010)
    async_rst <= '1' when (rst = '1' or (sig_q3 = '1' and sig_q1 = '1')) else '0';

    -- BIT 0
    process(clk, async_rst)
    begin
        if async_rst = '1' then
            sig_q0 <= '0';
        elsif rising_edge(clk) then
            sig_q0 <= not sig_q0;
        end if;
    end process;

    -- BIT 1
    process(clk1, async_rst)
    begin
        if async_rst = '1' then
            sig_q1 <= '0';
        elsif rising_edge(clk1) then
            sig_q1 <= not sig_q1;
        end if;
    end process;

    -- BIT 2
    process(clk2, async_rst)
    begin
        if async_rst = '1' then
            sig_q2 <= '0';
        elsif rising_edge(clk2) then
            sig_q2 <= not sig_q2;
        end if;
    end process;

    -- BIT 3
    process(clk3, async_rst)
    begin
        if async_rst = '1' then
            sig_q3 <= '0';
        elsif rising_edge(clk3) then
            sig_q3 <= not sig_q3;
        end if;
    end process;

    q0 <= sig_q0;
    q1 <= sig_q1;
    q2 <= sig_q2;
    q3 <= sig_q3;
end RTL;