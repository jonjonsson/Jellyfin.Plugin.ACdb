using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACdb.Services.Collections;

class SortingUtils
{
    public static readonly string SortNameStart = "!!![";
    private static readonly string SortNameEnd = "]";
    private static readonly Regex SortNameRegex = new(@"!!!\[\d+\]");

    public static int MinutesUntil2100(DateTime currentDate)
    {
        DateTime year2100 = new(2100, 1, 1);
        TimeSpan delta = year2100 - currentDate;
        int minutes = (int)delta.TotalMinutes;
        return minutes;
    }

    public static string GetSortToTopSortName(string name)
    {
        string sortName = GetDefaultSortName(name);
        return $"{SortNameStart}{MinutesUntil2100(DateTime.UtcNow)}{SortNameEnd}{sortName}";
    }

    public static string GetDateUntilSortName(string name, DateTimeOffset? now = null)
    {
        DateTime currentDate = now?.UtcDateTime ?? DateTime.UtcNow;
        return $"{SortNameStart}{MinutesUntil2100(currentDate)}{SortNameEnd}{name}";
    }

    public static string GetDefaultSortName(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        string sortName = title.ToLower();

        string[] articles = ["the ", "a ", "an ", "das ", "der ", "el ", "la "];
        foreach (string article in articles)
        {
            if (sortName.StartsWith(article))
            {
                sortName = sortName.Substring(article.Length);
                break;
            }
        }

        sortName = new string(sortName.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        sortName = string.Join(" ", sortName.Split([' '], StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrEmpty(sortName))
        {
            sortName = title.ToLower();
        }

        return sortName;
    }

    public static bool HasSortName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return SortNameRegex.IsMatch(name);
    }

    public static string RemoveSortName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return SortNameRegex.Replace(name, string.Empty);
    }

}
