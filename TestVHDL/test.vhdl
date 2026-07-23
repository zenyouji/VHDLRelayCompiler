-- 4-bit counter
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
                count <= "0000";
            elsif count = 9 then
                count <= "0000";
            elsif enable = '1' then
                count <= count + 1;
            end if;
        end if;
    end process;

    q <= count when enable = '1' else "0000";
end RTL;
