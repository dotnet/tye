namespace Microsoft.Tye.ConfigModel
{
    public class ConfigProbe
    {
        public ConfigHttpProbe? Http { get; set; }
        public int InitialDelay { get; set; } = 0;
        public int Period { get; set; } = 1;
    }
}
