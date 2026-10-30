namespace Nantuc.Kernel.Diagnostics;

/// <summary>
/// Коды ошибок компилятора: лексера, парсера, генератора кода и ассемблера.
/// </summary>
/// <remarks>
/// <para>
/// <b>Числовые значения — стабильный контракт.</b> Они попадают в логи,
/// телеметрию, JSON и тесты, поэтому менять существующие значения нельзя.
/// Новые коды добавляйте только в конец соответствующей группы или в новую
/// сотню, не переиспользуя числа.
/// </para>
/// <para>
/// Схема группировки по сотням:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Диапазон</term>
///     <description>Подсистема</description>
///   </listheader>
///   <item><term>1000–1099</term><description>Общие ошибки</description></item>
///   <item><term>1100–1199</term><description>Лексер</description></item>
///   <item><term>1200–1299</term><description>Парсер</description></item>
///   <item><term>1300–1399</term><description>Генератор кода (CodeGen)</description></item>
///   <item><term>1400–1499</term><description>Ассемблер</description></item>
/// </list>
/// <para>
/// Перечисление полностью управляемое и совместимо с WebAssembly,
/// включая AOT. Не используйте <see cref="System.ComponentModel.DescriptionAttribute"/>
/// для сообщений — в AOT/trimming рефлексия может быть урезана.
/// </para>
/// </remarks>
public enum ErrorCode
{
    // Общие

    /// <summary>
    /// Операция или функция не поддерживается в текущей конфигурации.
    /// </summary>
    NotSupported = 1000,

    /// <summary>
    /// Функциональность ещё не реализована.
    /// </summary>
    NotImplemented = 1001,

    // Лексер

    /// <summary>
    /// Лексер встретил символ, который не может начать ни один токен.
    /// </summary>
    Lexer_UnexpectedCharacter = 1100,

    /// <summary>
    /// Некорректный числовой литерал (например, неверный формат или переполнение).
    /// </summary>
    Lexer_InvalidNumber = 1101,

    /// <summary>
    /// Строковый литерал не закрыт до конца строки или файла.
    /// </summary>
    Lexer_UnterminatedString = 1102,

    // Парсер

    /// <summary>
    /// Парсер встретил неизвестный тип в объявлении или выражении.
    /// </summary>
    Parser_UnknownType = 1200,

    /// <summary>
    /// Ожидался конкретный токен, но получен другой.
    /// </summary>
    Parser_ExpectedToken = 1201,

    /// <summary>
    /// Токен неожиданен в данной грамматической позиции.
    /// </summary>
    Parser_UnexpectedToken = 1202,

    /// <summary>
    /// Некорректное присваивание: левая часть не является lvalue
    /// или типы несовместимы.
    /// </summary>
    Parser_InvalidAssignment = 1203,

    /// <summary>
    /// Размер массива должен быть константным выражением.
    /// </summary>
    Parser_ArraySizeNotConstant = 1204,

    /// <summary>
    /// Неизвестная директива препроцессора или ассемблера.
    /// </summary>
    Parser_UnknownDirective = 1205,

    /// <summary>
    /// Использована структура, которая не была объявлена.
    /// </summary>
    Parser_StructNotDefined = 1206,

    /// <summary>
    /// Некорректный символьный литерал (например, пустой или слишком длинный).
    /// </summary>
    Parser_InvalidCharLiteral = 1207,

    /// <summary>
    /// Неоднозначное разрешение <c>include</c>: найдено несколько кандидатов
    /// или путь неоднозначен.
    /// </summary>
    Parser_IncludeAmbiguous = 1208,

    // Генератор кода (CodeGen)

    /// <summary>
    /// Не удалось определить размер типа при генерации кода.
    /// </summary>
    CodeGen_UnknownTypeSize = 1300,

    /// <summary>
    /// Неизвестный код размера операнда инструкции.
    /// </summary>
    CodeGen_UnknownOpCodeSize = 1301,

    /// <summary>
    /// Неизвестный тип структуры при генерации кода.
    /// </summary>
    CodeGen_UnknownStructType = 1302,

    /// <summary>
    /// Обращение к несуществующему полю структуры.
    /// </summary>
    CodeGen_UnknownField = 1303,

    /// <summary>
    /// Обращение к полю/члену у значения, которое не является структурой.
    /// </summary>
    CodeGen_NonStructAccess = 1304,

    /// <summary>
    /// Некорректный доступ через точку (например, к недопустимому члену).
    /// </summary>
    CodeGen_InvalidDotAccess = 1305,

    /// <summary>
    /// Использование переменной, которая не была объявлена.
    /// </summary>
    CodeGen_UndefinedVariable = 1306,

    /// <summary>
    /// Невозможно получить адрес регистра (например, регистр не адресуем).
    /// </summary>
    CodeGen_CannotGetRegisterAddress = 1307,

    /// <summary>
    /// Индексация применена к типу, который не поддерживает индексацию.
    /// </summary>
    CodeGen_NonIndexableType = 1308,

    /// <summary>
    /// Вызов неизвестной функции.
    /// </summary>
    CodeGen_UnknownFunction = 1309,

    /// <summary>
    /// Количество аргументов при вызове не совпадает с сигнатурой функции.
    /// </summary>
    CodeGen_ArgumentCountMismatch = 1310,

    /// <summary>
    /// Параметр должен находиться в регистре, но это не так.
    /// </summary>
    CodeGen_ParameterNotInRegister = 1311,

    /// <summary>
    /// Неизвестный тип, на который указывает указатель.
    /// </summary>
    CodeGen_UnknownPointedType = 1312,

    /// <summary>
    /// Прямая загрузка/сохранение структуры не поддерживается —
    /// требуется доступ к полям.
    /// </summary>
    CodeGen_DirectStructLoadStore = 1313,

    /// <summary>
    /// <c>break</c> использован вне цикла.
    /// </summary>
    CodeGen_BreakOutsideLoop = 1314,

    /// <summary>
    /// <c>continue</c> использован вне цикла.
    /// </summary>
    CodeGen_ContinueOutsideLoop = 1315,
    /// <summary>
    /// Попытка записи в <c>const</c>-сущность: переменную, параметр, массив
    /// или значение, адресуемое <c>*const</c>-указателем.
    /// </summary>
    /// <remarks>
    /// Срабатывает для обычного присваивания (<c>x = ...</c>), составного
    /// (<c>x += ...</c>) и присваивания через <c>*p = ...</c> либо
    /// <c>arr[i] = ...</c>, если цель помечена как <c>const</c>.
    /// </remarks>
    CodeGen_AssignmentToConst = 1316,

    /// <summary>
    /// Попытка применить <c>++</c> или <c>--</c> к <c>const</c>-сущности:
    /// переменной, параметру, элементу <c>const</c>-массива или значению
    /// по <c>*const</c>-указателю.
    /// </summary>
    /// <remarks>
    /// Срабатывает и для префиксной, и для постфиксной формы
    /// (<c>++x</c>, <c>x++</c>, <c>--x</c>, <c>x--</c>, <c>(*p)++</c>,
    /// <c>arr[i]++</c>).
    /// </remarks>
    CodeGen_IncrementOfConst = 1317,    // Ассемблер

    /// <summary>
    /// Ссылка на неопределённую метку.
    /// </summary>
    Asm_UndefinedLabel = 1400,

    /// <summary>
    /// Метка объявлена более одного раза.
    /// </summary>
    Asm_DuplicateLabel = 1401,

    /// <summary>
    /// Некорректная инструкция (неверные операнды или формат).
    /// </summary>
    Asm_InvalidInstruction = 1402,

    /// <summary>
    /// Неизвестный регистр в операнде инструкции.
    /// </summary>
    Asm_UnknownRegister = 1403,

    /// <summary>
    /// Неизвестная мнемоника инструкции.
    /// </summary>
    Asm_UnknownMnemonic = 1404,
}
