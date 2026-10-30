using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Diagnostics;

/// <summary>
/// Фасад над <see cref="IErrorMessageProvider"/> для удобного создания и выбрасывания
/// ошибок компилятора с локализованным сообщением.
/// </summary>
/// <remarks>
/// <para>
/// Инкапсулирует связку «получить текст сообщения по <see cref="ErrorCode"/> → бросить
/// <see cref="CodeException"/>». Избавляет места вызова от повторяющегося кода
/// <c>throw new CodeException(code, provider.GetMessage(code, args))</c>.
/// </para>
/// <para>
/// Экземпляр считается неизменяемым после конструирования: поле
/// <see cref="ErrorMessageProvider"/> инициализируется один раз в конструкторе
/// и дальше используется только для чтения. Если требуется горячая замена политики
/// (например, в тестах), создавайте новый экземпляр <see cref="ThrowCompiler"/>.
/// </para>
/// <para>
/// Оба метода помечены <see cref="DoesNotReturnAttribute"/> и
/// <see cref="MethodImplOptions.NoInlining"/>: компилятор знает, что управление
/// после вызова не возвращается (упрощает анализ потока управления в местах вызова),
/// а JIT не инлайнит холодный путь генерации исключения.
/// </para>
/// </remarks>
/// <param name="provider">
/// Провайдер сообщений об ошибках. Если передан <see langword="null"/>,
/// используется <see cref="IErrorMessageProvider.Default"/>.
/// </param>
public class ThrowCompiler(IErrorMessageProvider? provider)
{
    /// <summary>
    /// Текущая политика ошибок компилятора — провайдер, из которого берутся
    /// тексты сообщений для <see cref="ErrorCode"/>.
    /// </summary>
    /// <remarks>
    /// Никогда не <see langword="null"/>: при конструировании подставляется
    /// <see cref="IErrorMessageProvider.Default"/>, если аргумент не задан.
    /// </remarks>
    public IErrorMessageProvider ErrorMessageProvider = provider ?? IErrorMessageProvider.Default;

    /// <summary>
    /// Формирует сообщение по коду ошибки и выбрасывает <see cref="CodeException"/>.
    /// </summary>
    /// <param name="code">Код ошибки компилятора.</param>
    /// <param name="args">
    /// Аргументы для подстановки в шаблон сообщения. Передаются как
    /// <see cref="ReadOnlySpan{T}"/> без аллокации массива на горячем пути.
    /// </param>
    /// <exception cref="CodeException">
    /// Всегда выбрасывается. Содержит <paramref name="code"/> и сформированный текст сообщения.
    /// </exception>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Throw(ErrorCode code, params ReadOnlySpan<DiagnosticData> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeException(code, message);
    }

    /// <summary>
    /// Формирует сообщение по коду ошибки и выбрасывает <see cref="CodeException"/>,
    /// возвращая тип <typeparamref name="T"/> в сигнатуре — чтобы вызов можно было
    /// использовать в выражении, где ожидается значение (например,
    /// <c>return thrower.Throw&lt;Node&gt;(ErrorCode.UnexpectedToken);</c>),
    /// не заводя отдельную ветку с <see langword="throw"/>.
    /// </summary>
    /// <typeparam name="T">
    /// Тип, который «якобы» возвращается методом. Фактически управление не возвращается —
    /// тип нужен только для того, чтобы вызов подходил по типу в контексте выражения.
    /// </typeparam>
    /// <param name="code">Код ошибки компилятора.</param>
    /// <param name="args">
    /// Аргументы для подстановки в шаблон сообщения.
    /// </param>
    /// <exception cref="CodeException">
    /// Всегда выбрасывается. Содержит <paramref name="code"/> и сформированный текст сообщения.
    /// </exception>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public T Throw<T>(ErrorCode code, params ReadOnlySpan<DiagnosticData> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeException(code, message);
    }
}