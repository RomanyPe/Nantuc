namespace Nantuc.Kernel.Common;

///<summary>разрядность числа. S8 = нумератор для 8 бит = 1 байт, </summary>
public enum OpCodeSize
{
    /// <summary>
    /// разрядность числа 8 бит
    /// </summary>
    S8 = 0b00,
    /// <summary>разрядность числа 16 </summary
    S16 = 0b01,
    /// <summary>разрядность числа 32 </summary
    S32 = 0b10,
    /// <summary>разрядность числа 64 </summary
    S64 = 0b11,
}

