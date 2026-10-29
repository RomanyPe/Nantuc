using Nantuc.Compiler.ASM.IR;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Unsafe;

/// <summary>
/// Небезопасные расширения для <see cref="CompilationToILResult"/>.
/// Предоставляют прямой доступ к внутреннему <see cref="ILAssembler"/> без дополнительных проверок и приведений.
/// </summary>
/// <remarks>
/// Использование этого класса нарушает инкапсуляцию <see cref="CompilationToILResult"/>.
/// Полученный <see cref="ILAssembler"/> не имеет ограничений, поэтому его некорректное
/// использование может привести к повреждению состояния компиляции, некорректному байткоду
/// или неопределённому поведению. Применяйте только в отладочных, диагностических
/// или исследовательских сценариях.
/// </remarks>
public static class CompilationResultUnsafeExtension
{
    /// <param name="il">
    /// Результат компиляции, из которого извлекается ассемблер IL.
    /// Не может быть <see langword="null"/>.
    /// </param>
    extension(CompilationToILResult il)
    {
        /// <summary>
        /// Возвращает внутренний <see cref="ILAssembler"/>, связанный с результатом компиляции,
        /// либо <see langword="null"/>, если ассемблер отсутствует.
        /// </summary>
        /// <value>
        /// Внутренний экземпляр <see cref="ILAssembler"/> или <see langword="null"/>.
        /// </value>
        /// <remarks>
        /// Свойство предоставляет <see cref="ILAssembler"/> «как есть», без проверок состояния
        /// и без гарантий целостности после завершения компиляции. Некорректные операции
        /// с ним могут сломать состояние результата компиляции.
        /// </remarks>
        public ILAssembler? Assembler => il.Assembler;
    }
}