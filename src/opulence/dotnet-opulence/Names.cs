using System;

namespace Opulence
{
    internal static class Names
    {
        public static string NormalizeToDns(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.ToLowerInvariant();
            name = name.Replace('.', '-');
            name = name.Replace('_', '-');
            return name;
        }

        public static string NormalizeToFriendly(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.Replace('.', '_');
            name = name.Replace('-', '_');
            return name;
        }
    }
}