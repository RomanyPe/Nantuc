using Nantuc.Compiler.ASM.Base;
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
