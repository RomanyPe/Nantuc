namespace Nantuc.VmApplication.Project;

/// <summary>
/// Перечисление языков
/// </summary>
public enum SourceLanguage
{
    /// <summary> nanasm - ассемблерный язык для виртуальной машины </summary>
    Nanasm,
    /// <summary> MiniC - C подобный язык, очень близок к реальному C по сематике, но нет множества функций </summary>
    MiniC,
    /// <summary> язык неопределен </summary>
    None,
}
