namespace Nantuc.Common;

public static class OpCodeExtension
{
    public const int LastValueInt = (int)LastValue;
    public const OpCode LastValue = OpCode.CASTS;

    extension(OpCode op)
    {
        public uint ToUint() => (uint)op;

        public string OpName() => op switch
        {
            OpCode.NOP => nameof(OpCode.NOP),
            OpCode.END => nameof(OpCode.END),
            OpCode.MOV => nameof(OpCode.MOV),
            OpCode.LOAD => nameof(OpCode.LOAD),
            OpCode.STORE => nameof(OpCode.STORE),
            OpCode.LDI => nameof(OpCode.LDI),
            OpCode.LOAD_IND => nameof(OpCode.LOAD_IND),
            OpCode.STORE_IND => nameof(OpCode.STORE_IND),
            OpCode.ADD => nameof(OpCode.ADD),
            OpCode.SUB => nameof(OpCode.SUB),
            OpCode.MULT_INT => nameof(OpCode.MULT_INT),
            OpCode.SHR => nameof(OpCode.SHR),
            OpCode.MOD => nameof(OpCode.MOD),
            OpCode.SHL => nameof(OpCode.SHL),
            OpCode.INC => nameof(OpCode.INC),
            OpCode.DEC => nameof(OpCode.DEC),
            OpCode.DIV => nameof(OpCode.DIV),
            OpCode.AND => nameof(OpCode.AND),
            OpCode.OR => nameof(OpCode.OR),
            OpCode.XOR => nameof(OpCode.XOR),
            OpCode.NOT => nameof(OpCode.NOT),
            OpCode.JMP => nameof(OpCode.JMP),
            OpCode.JZ => nameof(OpCode.JZ),
            OpCode.JNZ => nameof(OpCode.JNZ),
            OpCode.JG => nameof(OpCode.JG),
            OpCode.JL => nameof(OpCode.JL),
            OpCode.PUSH => nameof(OpCode.PUSH),
            OpCode.POP => nameof(OpCode.POP),
            OpCode.CALL => nameof(OpCode.CALL),
            OpCode.RET => nameof(OpCode.RET),
            OpCode.IN => nameof(OpCode.IN),
            OpCode.OUT => nameof(OpCode.OUT),
            OpCode.CALL_IND => nameof(OpCode.CALL_IND),
            OpCode.JMP_IND => nameof(OpCode.JMP_IND),
            OpCode.HALT => nameof(OpCode.HALT),
            OpCode.WAKE => nameof(OpCode.WAKE),
            OpCode.WAKE_INT => nameof(OpCode.WAKE_INT),
            OpCode.LOAD_UNSAFE => nameof(OpCode.LOAD_UNSAFE),
            OpCode.STORE_UNSAFE => nameof(OpCode.STORE_UNSAFE),
            OpCode.STORE_IND_UNSAFE => nameof(OpCode.STORE_IND_UNSAFE),
            OpCode.LOAD_IND_UNSAFE => nameof(OpCode.LOAD_IND_UNSAFE),
            OpCode.CMP => nameof(OpCode.CMP),
            OpCode.TEST => nameof(OpCode.TEST),
            OpCode.CASTZ => nameof(OpCode.CASTZ),
            OpCode.CASTS => nameof(OpCode.CASTS),
            _ => op.ToUint().ToString()
        };
    }

}
