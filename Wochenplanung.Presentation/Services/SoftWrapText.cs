using System.Text;

namespace Wochenplanung.Presentation.Services;

public static class SoftWrapText
{
    public static string InsertHints(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length + 8);
        var run = 0;
        foreach (var ch in text)
        {
            sb.Append(ch);
            if (char.IsWhiteSpace(ch))
            {
                run = 0;
                continue;
            }

            run++;
            if (ch is '+' or '/' or '-' or '_' or '.' or '&' or ',' or ';' or ':' or '(' or ')' or '[' or ']')
            {
                sb.Append('\u200B');
                run = 0;
            }
            else if (run >= 8)
            {
                sb.Append('\u200B');
                run = 0;
            }
        }

        return sb.ToString();
    }
}
