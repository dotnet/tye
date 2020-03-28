namespace System
{
    internal static class StringExtensions
    {
        public static string NormalizeNewLines(this string value)
        {
            return value
                .Replace("\r\n", "\n")
                .Replace("\n", Environment.NewLine);
        }
    }
}
