using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Unsafe;

/// <summary>
/// Небезопасные расширения для <see cref="VmHostProject"/>.
/// Предоставляют доступ к внутреннему конвейеру компиляции проекта.
/// </summary>
/// <remarks>
/// Использование этого класса нарушает инкапсуляцию <see cref="VmHostProject"/>.
/// Вызывающий код берёт на себя ответственность за корректность переданных делегатов,
/// типов ассемблера и результата. Ошибки в этих делегатах могут приводить к исключениям
/// внутри конвейера компиляции, некорректному байткоду или повреждению состояния проекта.
/// </remarks>
public static class VmPtojectUnsafeExtension
{
    /// <param name="proj">
    /// Проект, для которого выполняется компиляция. Не может быть <see langword="null"/>.
    /// </param>
    extension(VmHostProject proj)
    {
        /// <summary>
        /// Выполняет компиляцию исходных файлов проекта с возможностью подставить собственные
        /// фабрики ассемблера и результата компиляции.
        /// </summary>
        /// <typeparam name="TAssembler">
        /// Конкретный тип ассемблера, создаваемый фабрикой <paramref name="createAssembler"/>.
        /// Должен наследовать <see cref="AssemblerBase"/>.
        /// </typeparam>
        /// <typeparam name="TResult">
        /// Тип результата, возвращаемого фабрикой <paramref name="createResult"/>.
        /// </typeparam>
        /// <param name="baseAddress">Базовый адрес, по которому будет размещён скомпилированный код.</param>
        /// <param name="optimize">
        /// Признак необходимости применять оптимизации в процессе компиляции
        /// (<see langword="true"/> — включить, <see langword="false"/> — отключить).
        /// </param>
        /// <param name="files">
        /// Набор исходных файлов для компиляции. Не может быть <see langword="null"/>.
        /// </param>
        /// <param name="createAssembler">
        /// Фабрика, создающая экземпляр <typeparamref name="TAssembler"/> по базовому адресу.
        /// Не может быть <see langword="null"/>.
        /// </param>
        /// <param name="createResult">
        /// Фабрика, формирующая итоговый результат компиляции <typeparamref name="TResult"/>
        /// на основе ассемблера, журнала оптимизаций и массива диагностических сообщений.
        /// Не может быть <see langword="null"/>.
        /// </param>
        /// <returns>
        /// Результат, сформированный фабрикой <paramref name="createResult"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Если <paramref name="proj"/>, <paramref name="files"/>,
        /// <paramref name="createAssembler"/> или <paramref name="createResult"/>
        /// равны <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Метод обходит штатный публичный API компиляции. Корректность поведения
        /// полностью зависит от переданных делегатов; при их ошибочной реализации возможны
        /// исключения внутри конвейера, некорректный байткод или порча состояния проекта.
        /// </remarks>
        public TResult CustomCompile<TAssembler, TResult>(
            ulong baseAddress,
            bool optimize,
            IEnumerable<SourceFile> files,
            Func<ulong, TAssembler> createAssembler,
            Func<TAssembler, OptimizationResultLog?, string[]?, TResult> createResult)
            where TAssembler : AssemblerBase
        {
            return proj.CompileInternal(baseAddress, optimize, files, createAssembler, createResult);
        }
    }
}

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