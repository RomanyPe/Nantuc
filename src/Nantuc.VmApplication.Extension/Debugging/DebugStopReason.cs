namespace Nantuc.VmApplication.Extensions.Debugging;

/// <summary>
/// Причина, по которой отладчик вернул управление вызывающему.
/// </summary>
public enum DebugStopReason
{
    /// <summary>Отладчик ещё не останавливался.</summary>
    None = 0,

    /// <summary>Выполнена одна инструкция (<c>Step</c>/<c>StepOver</c>/<c>StepOut</c>).</summary>
    StepComplete,

    /// <summary>Сработала точка останова на текущем <c>rIP</c>.</summary>
    Breakpoint,

    /// <summary>Программа выполнила <c>END</c> и завершилась штатно.</summary>
    Completed,

    /// <summary>Программа упала (<c>BiosStatus</c> — не <c>EndProgramm</c> и не <c>Success</c>).</summary>
    Faulted,

    /// <summary>Программа выполнила <c>HALT</c> и ждёт пробуждения.</summary>
    Sleeping,

    /// <summary>Достигнут лимит шагов, переданный в <c>Continue</c>.</summary>
    StepLimit,

    /// <summary>Пользователь вызвал <c>RequestStop</c>.</summary>
    UserStop,

    /// <summary>Внутренняя ошибка отладчика.</summary>
    Error,
}
