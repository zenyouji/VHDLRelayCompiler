library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

entity Counter is
    Port (
        clk : in STD_LOGIC;
        rst : in STD_LOGIC;
        q   : out STD_LOGIC_VECTOR(3 downto 0)
    );
end Counter;

architecture RTL of Counter is
    signal count : STD_LOGIC_VECTOR(3 downto 0);
begin
    -- クロック同期のプロセス
    process(clk, rst)
    begin
        if rst = '1' then
            count <= "0000";
        elsif rising_edge(clk) then
            count <= count + 1;
        end if;
    end process;

    -- 外部出力への代入
    q <= count;
end RTL;