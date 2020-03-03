namespace Tye
{
    public sealed class HelmChartStep : Step
    {
        public override string DisplayName => "Building Helm Chart...";

        public string ChartName { get; set; } = default!;
    }
}
