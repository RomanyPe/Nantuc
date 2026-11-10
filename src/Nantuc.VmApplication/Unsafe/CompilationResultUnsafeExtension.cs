using Nantuc.Compiler.ASM.IR;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Unsafe;

/// <summary>
/// Небезопасные расширения для <see cref="CompilationToIRResult"/>.
/// Предоставляют прямой доступ к внутреннему <see cref="IRAssembler"/> без дополнительных проверок и приведений.
/// </summary>
/// <remarks>
/// Использование этого класса нарушает инкапсуляцию <see cref="CompilationToIRResult"/>.
/// Полученный <see cref="IRAssembler"/> не имеет ограничений, поэтому его некорректное
/// использование может привести к повреждению состояния компиляции, некорректному байткоду
/// или неопределённому поведению. Применяйте только в отладочных, диагностических
/// или исследовательских сценариях.
/// </remarks>
public static class CompilationResultUnsafeExtension
{
    /// <param name="il">
    /// Результат компиляции, из которого извлекается ассемблер IR.
    /// Не может быть <see langword="null"/>.
    /// </param>
    extension(CompilationToIRResult il)
    {
        /// <summary>
        /// Возвращает внутренний <see cref="IRAssembler"/>, связанный с результатом компиляции,
        /// либо <see langword="null"/>, если ассемблер отсутствует.
        /// </summary>
        /// <value>
        /// Внутренний экземпляр <see cref="IRAssembler"/> или <see langword="null"/>.
        /// </value>
        /// <remarks>
        /// Свойство предоставляет <see cref="IRAssembler"/> «как есть», без проверок состояния
        /// и без гарантий целостности после завершения компиляции. Некорректные операции
        /// с ним могут сломать состояние результата компиляции.
        /// </remarks>
        public IRAssembler? Assembler => il.Assembler;
    }
}
