namespace Nantuc.Kernel.Diagnostics;

/// <summary>
/// Политика исплючений
/// </summary>
public interface IErrorMessageProvider
{
    /// <summary>
    /// Стандартная реализация
    /// </summary>
    static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
    /// <summary>
    /// Метод получения готовой строки с ошибкой в зависимости от <paramref name="code"/>
    /// </summary>
    /// <param name="code">код ошибки</param>
    /// <param name="args">аргументы</param>
    string GetMessage(ErrorCode code, params ReadOnlySpan<object> args);
}
