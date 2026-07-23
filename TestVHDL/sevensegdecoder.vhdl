library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity SevenSegDecoder is
    Port (
        din : in  STD_LOGIC_VECTOR(3 downto 0);
        seg : out STD_LOGIC_VECTOR(6 downto 0)
    );
end SevenSegDecoder;

architecture RTL of SevenSegDecoder is
begin
    -- 組み合わせ回路のプロセス
    process(din)
    begin
        case din is
            when "0000" => 
                seg <= "1111110"; -- '0'の表示
            when "0001" => 
                seg <= "0110000"; -- '1'の表示
            when "0010" => 
                seg <= "1101101"; -- '2'の表示
            when "0011" => 
                seg <= "1111001"; -- '3'の表示
            when "0100" => 
                seg <= "0110011"; -- '4'の表示
            when others => 
                seg <= "0000000"; -- 消灯（またはエラー表示）
        end case;
    end process;
end RTL;