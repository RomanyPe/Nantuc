using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.VmApplication.Project;
using System.Text;

namespace Nantuc.VmApplication.Extensions.Disassembler;

public static class StringDisassembler
{
    extension(CompilationResult res)
    {
        public string DisassembleToString() => Disassemble(res.Program,
                                                           res.CodeSize!.Value,
                                                           res.StartAdress!.Value);
    }

    /// <summary>
    /// Метод для декодировани и представления программы Assembler в текстовом виде
    /// </summary>
    /// <param name="program"> Байт код программы </param>
    /// <param name="length"> [OUTPUT] количество строк кода программы </param>
    /// <param name="size"> [OUTPUT] размер программы в byte</param>
    /// <param name="baseAddress"> Начальный адресс чтения программы </param>
    /// <returns> Текстовое представление Assembler</returns>
    public static string Disassemble(ReadOnlySpan<byte> program, int codeSize, ulong baseAddress = 0)
    {
        StringBuilder sb = new();
        int pos = 0;
        int size = program.Length;
        int length = 0;

        // Имитируем программный счетчик (IP) как в CPU
        ulong currentIp = baseAddress;

        while (pos < size)
        {
            // ==========================================
            // ДОСТИГЛИ СЕКЦИИ ДАННЫХ
            // ==========================================
            if (pos >= codeSize)
            {
                sb.AppendLine($"--- data section ({size - codeSize} bytes) ---");

                // Выводим данные по 8 байт (как 64-битные слова)
                WriteSectionData(program, sb, ref pos, size, ref length, ref currentIp);
                break;
            }

            // Проверяем, хватает ли байт на саму инструкцию в массиве
            if (pos + 4 > size)
            {
                sb.AppendLine($"{currentIp:X8}: incomplete instruction, {size - pos} bytes missing>");
                break;
            }

            uint instruction = BitConverter.ToUInt32(program.Slice(pos, 4));
            var opcode = InstructionDecoder.GetOpCode(instruction);


            string decoded = InstructionEncoder.Decode(instruction);
            sb.Append($"{currentIp:X8}: {instruction:X8}   {decoded}");

            // Симулируем шаг процессора: прочитали инструкцию, продвинули IP и POS
            ulong nextIp = currentIp + 4;
            int nextPos = pos + 4;

            if (InstructionDecoder.HasNeed64IntData(opcode))
            {
                // Выравнивание строго по абсолютному адресу IP, как в CPU
                ulong alignedIp = (nextIp + 7) & ~7UL;
                long pad = (long)(alignedIp - nextIp);

                // Сдвигаем позицию в массиве на величину паддинга
                nextPos += (int)pad;
                nextIp = alignedIp;

                if (nextPos + 8 <= size)
                {
                    ulong data = BitConverter.ToUInt64(program.Slice(nextPos, 8));
                    sb.Append($"  data = 0x{data:X16} ({data})");
                    nextPos += 8;
                    nextIp += 8;
                }
                else
                {
                    sb.Append($"  <insufficient bytes for data>");
                    sb.AppendLine();
                    break;
                }
            }

            currentIp = nextIp;
            pos = nextPos;
            length++;
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void WriteSectionData(ReadOnlySpan<byte> program, StringBuilder sb, ref int pos, int size, ref int length, ref ulong currentIp)
    {
        while (pos < size)
        {
            int chunkSize = Math.Min(8, size - pos);
            ulong chunkVal = 0;

            // Собираем 8 байт в 64-битное число (Little-Endian)
            for (int i = 0; i < chunkSize; i++)
                chunkVal |= (ulong)program[pos + i] << (i * 8);

            // Формируем hex-строку байт (с выравниванием для последнего чанка)
            string hexBytes = "";
            for (int i = 0; i < 8; i++)
            {
                if (i < chunkSize) hexBytes += $"{program[pos + i]:X2} ";
                else hexBytes += "   ";
            }

            // Пытаемся декодировать как строку (char = S16, 2 байта на символ)
            string str = "";
            for (int i = 0; i < chunkSize; i += 2)
            {
                if (i + 1 < chunkSize)
                {
                    ushort c = (ushort)(program[pos + i] | (program[pos + i + 1] << 8));
                    if (c == 0) break; // Конец строки
                                       // Если символ печатный (ASCII), добавляем, иначе точку
                    if (c >= 32 && c < 127) str += (char)c;
                    else str += ".";
                }
            }

            string decodedStr = string.IsNullOrEmpty(str) ? "" : $"  \"{str}\"";

            // Выводим: Адрес | Hex байты | Значение | Строка
            sb.AppendLine($"{currentIp:X8}: {hexBytes} .data 0x{chunkVal:X16}{decodedStr}");

            pos += chunkSize;
            currentIp += (ulong)chunkSize;
            length++;
        }
    }
}
