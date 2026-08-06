namespace TanbalType;

/// <summary>
/// Mapping between US English and Persian (Iranian) keyboard on Windows.
/// </summary>
public static class Mapper
{
    /// <summary>لایهٔ پایه (بدون Shift) روی چیدمان استاندارد فارسی.</summary>
    private static readonly Dictionary<char, char> BaseKeys = new()
    {
        ['`'] = 'ذ', ['1'] = '۱', ['2'] = '۲', ['3'] = '۳', ['4'] = '۴',
        ['5'] = '۵', ['6'] = '۶', ['7'] = '۷', ['8'] = '۸', ['9'] = '۹',
        ['0'] = '۰', ['-'] = '-', ['='] = '=',
        ['q'] = 'ض', ['w'] = 'ص', ['e'] = 'ث', ['r'] = 'ق', ['t'] = 'ف',
        ['y'] = 'غ', ['u'] = 'ع', ['i'] = 'ه', ['o'] = 'خ', ['p'] = 'ح',
        ['['] = 'ج', [']'] = 'چ', ['\\'] = 'پ',
        ['a'] = 'ش', ['s'] = 'س', ['d'] = 'ی', ['f'] = 'ب', ['g'] = 'ل',
        ['h'] = 'ا', ['j'] = 'ت', ['k'] = 'ن', ['l'] = 'م',
        [';'] = 'ک', ['\''] = 'گ',
        ['z'] = 'ظ', ['x'] = 'ط', ['c'] = 'ز', ['v'] = 'ر', ['b'] = 'ذ',
        ['n'] = 'د', ['m'] = 'ئ', [','] = 'و', ['.'] = '.', ['/'] = '/',
    };

    /// <summary>
    /// کلیدهای Shift دار که روی چیدمان استاندارد فارسی حرف تولید می‌کنند.
    /// «آ» با Shift+H تایپ می‌شود و بدون این نگاشت، واژه‌هایی مثل «آقا» (کلیدهای Hrh)
    /// اصلاً تشخیص داده نمی‌شدند.
    /// </summary>
    private static readonly Dictionary<char, char> ShiftedKeys = new()
    {
        ['H'] = 'آ',
    };

    public static readonly IReadOnlyDictionary<char, char> EnKeyToFa = BuildEnKeyToFa();

    private static Dictionary<char, char> BuildEnKeyToFa()
    {
        var map = new Dictionary<char, char>(BaseKeys);

        // حروف بزرگ (Caps Lock یا Shift) مثل حرف کوچکِ خودشان نگاشت شوند
        foreach (var (key, fa) in BaseKeys)
        {
            if (key is >= 'a' and <= 'z')
                map[char.ToUpperInvariant(key)] = fa;
        }

        // نگاشت اختصاصی Shift بر فالبکِ بالا اولویت دارد
        foreach (var (key, fa) in ShiftedKeys)
            map[key] = fa;

        return map;
    }

    public static readonly HashSet<char> EnLayoutKeys = EnKeyToFa.Keys.ToHashSet();
    public static readonly HashSet<char> PersianPunctAscii = [',', ';'];

    private static readonly Dictionary<char, char> FaToEnKey = EnKeyToFa
        .OrderBy(static kv => ReverseMapPriority(kv.Key))
        .ThenBy(static kv => kv.Key)
        .GroupBy(static kv => kv.Value)
        .ToDictionary(static g => g.Key, static g => g.First().Key);

    /// <summary>Prefer a-z over digits/punctuation when Persian letter maps from multiple keys (e.g. b and ` → ذ).</summary>
    private static int ReverseMapPriority(char enKey) => enKey switch
    {
        >= 'a' and <= 'z' => 0,
        >= '0' and <= '9' => 1,
        _ => 2,
    };

    public static string EnKeysToPersian(string text)
    {
        var result = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            result[i] = EnKeyToFa.TryGetValue(ch, out var fa) ? fa : ch;
        }
        return new string(result);
    }

    public static string PersianToEnKeys(string text)
    {
        var result = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            result[i] = FaToEnKey.TryGetValue(ch, out var en) ? en : ch;
        }
        return new string(result);
    }

    public static int CountEnLayoutKeys(string text) =>
        text.Count(ch => EnLayoutKeys.Contains(ch));

    public static int CountPersian(string text) =>
        text.Count(ch => ch is >= '\u0600' and <= '\u06FF' or 'آ' or 'ئ' or 'ؤ' or 'ي' or 'ك' or 'ة' or 'ژ');

    public static int CountAsciiLetters(string text) =>
        text.Count(ch => char.IsAsciiLetter(ch));
}
