using System;

namespace Opulence
{
    public class EnvironmentAttribute : Attribute
    {
        public EnvironmentAttribute(string environmentName)
        {
            if (environmentName is null)
            {
                throw new ArgumentNullException(nameof(environmentName));
            }

            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; }
    }
}
