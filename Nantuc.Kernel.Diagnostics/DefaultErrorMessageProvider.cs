using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace Nantuc.Kernel.Diagnostics;

/// <summary>
/// Стандартная политика исключений компилятора.
/// <para>
/// Переданные <paramref name="overrides"/> накладываются поверх стандартных шаблонов:
/// совпадающие коды перезаписываются, остальные наследуются из дефолтного набора.
/// </para>
/// </summary>
/// <param name="overrides">словарь код ошибки - текст ошибки (мерж поверх дефолтов)</param>
public class DefaultErrorMessageProvider(FrozenDictionary<ErrorCode, string>? overrides = null) : IErrorMessageProvider
{
    private static readonly FrozenDictionary<ErrorCode, string> templates = new Dictionary<ErrorCode, string>()
    {
        { ErrorCode.NotSupported, "Not supported: {0}" },
        { ErrorCode.NotImplemented, "Feature not implemented: {0}" },

        { ErrorCode.Lexer_UnexpectedCharacter, "Unexpected character '{0}' at line {1}, column {2}" },

        { ErrorCode.Parser_UnknownType, "Unknown type '{0}'" },
        { ErrorCode.Parser_ExpectedToken, "Expected {0}, got {1} at {2}:{3}" },
        { ErrorCode.Parser_UnexpectedToken, "Unexpected token: {0}" },
        { ErrorCode.Parser_InvalidAssignment, "Invalid assignment target" },
        { ErrorCode.Parser_ArraySizeNotConstant, "Array size must be constant" },
        { ErrorCode.Parser_UnknownDirective, "Unknown preprocessor directive: #{0}" },
        { ErrorCode.Parser_StructNotDefined, "Struct '{0}' is not defined" },
        { ErrorCode.Parser_InvalidCharLiteral, "Char literal '{0}' is invalid" },
        { ErrorCode.Parser_IncludeAmbiguous, "The file {0} exists in both {1} and {2}; please select a specific file."},

        { ErrorCode.CodeGen_UnknownTypeSize, "Unknown type '{0}' for memory size" },
        { ErrorCode.CodeGen_UnknownOpCodeSize, "Unknown opcode size: {0}" },
        { ErrorCode.CodeGen_UnknownStructType, "Unknown struct type '{0}'" },
        { ErrorCode.CodeGen_UnknownField, "Field '{0}' not found in struct '{1}'" },
        { ErrorCode.CodeGen_NonStructAccess, "'{0}' is not a struct or pointer to struct" },
        { ErrorCode.CodeGen_InvalidDotAccess, "Dot access only for simple variables" },
        { ErrorCode.CodeGen_UndefinedVariable, "Undefined variable '{0}'" },
        { ErrorCode.CodeGen_CannotGetRegisterAddress, "Cannot take address of register variable '{0}'" },
        { ErrorCode.CodeGen_NonIndexableType, "'{0}' is not an array or pointer" },
        { ErrorCode.CodeGen_UnknownFunction, "Function '{0}' not found" },
        { ErrorCode.CodeGen_ArgumentCountMismatch, "Argument count mismatch for function '{0}'" },
        { ErrorCode.CodeGen_ParameterNotInRegister, "Parameter '{0}' not allocated to a register" },
        { ErrorCode.CodeGen_UnknownPointedType, "Cannot determine pointed type for dereference" },
        { ErrorCode.CodeGen_DirectStructLoadStore, "Direct load/store of struct variable '{0}' is not supported" },
        { ErrorCode.CodeGen_BreakOutsideLoop, "Operator \"break\" outside loop" },
        { ErrorCode.CodeGen_ContinueOutsideLoop, "Operator \"continue\" outside loop" },

        // ── const-correctness ─────────────────────────────────────
        { ErrorCode.CodeGen_AssignmentToConst,   "Cannot assign to const '{0}'" },
        { ErrorCode.CodeGen_IncrementOfConst,    "Cannot increment/decrement const '{0}'" },

        { ErrorCode.Asm_UndefinedLabel, "Undefined label '{0}' at position '{1}'" },
        { ErrorCode.Asm_DuplicateLabel, "Label '{0}' already defined at position {1}" },
        { ErrorCode.Asm_InvalidInstruction, "Invalid instruction syntax: {0}" },
        { ErrorCode.Asm_UnknownRegister, "Unknown register '{0}'" },
        { ErrorCode.Asm_UnknownMnemonic, "Unknown mnemonic '{0}'" },
        { ErrorCode.Lexer_InvalidNumber, "Invalid number literal '{0}' at {1}:{2}" },
        { ErrorCode.Lexer_UnterminatedString, "Unterminated string starting at {0}:{1}" }
    }.ToFrozenDictionary();

    /// <summary>
    /// Стандартные <see cref="FrozenDictionary"/> шаблонов ошибок.
    /// </summary>
    public static FrozenDictionary<ErrorCode, string> DefaultTemplates => templates;

    /// <summary>
    /// Шаблоны текущей политики (стандартные + <paramref name="overrides"/>).
    /// </summary>
    public IReadOnlyDictionary<ErrorCode, string> Templates => _templates;

    private readonly ReadOnlyDictionary<ErrorCode, string> _templates =
        overrides is { Count: > 0 }
            ? MergeTemplates(templates, overrides)
            : templates.AsReadOnly();

    /// <summary>
    /// Возвращает новый read-only словарь: сначала все базовые шаблоны, затем <paramref name="overrides"/>
    /// (перезаписывают совпадающие ключи).
    /// </summary>
    private static ReadOnlyDictionary<ErrorCode, string> MergeTemplates(
        FrozenDictionary<ErrorCode, string> baseTemplates,
        FrozenDictionary<ErrorCode, string> overrides)
    {
        var merged = new Dictionary<ErrorCode, string>(baseTemplates.Count + overrides.Count);
        foreach (var kv in baseTemplates)
            merged[kv.Key] = kv.Value;
        foreach (var kv in overrides)
            merged[kv.Key] = kv.Value;
        return merged.AsReadOnly();
    }

    /// <summary>
    /// Try-версия получения строки ошибки.
    /// </summary>
    protected virtual string? TryGetTemplate(ErrorCode code)
    {
        return _templates.TryGetValue(code, out var v) ? v : null;
    }

    public string GetMessage(ErrorCode code, params ReadOnlySpan<DiagnosticData> args)
    {
        var template = TryGetTemplate(code)
            ?? throw new InvalidOperationException($"No message template for error code {code}");
        if (args.IsEmpty)
            return template;

        // string[] ковариантен object[] — string.Format получит тот же массив без копии.
        // Боксинг отсутствует: элементы уже строки.
        string[] parts = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
            parts[i] = args[i].Text;

        return string.Format(template, parts);
    }
}