using Microsoft.Tye.Hosting.Ansi2Html;
using Xunit;

namespace Microsoft.Tye.UnitTests;

public class Ansi2HtmlConverterTests
{
    [Theory]
    [InlineData("\u001b[31;1mThis text is red\u001b[0m", $"<span style=\"color:{Constants.Red};\">This text is red</span>")]
    [InlineData(
        "\u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m   3 | \u001b[0m \u001b[36;1muvicorn\u001b[0m app.main:app --port $Env:PORT --reload --log-level debug\u001b[0m",
        $"<span style=\"color:{Constants.Red};\"></span><span style=\"color:{Constants.Red};\"><span style=\"color:{Constants.Red};\">   3 | </span> <span style=\"color:{Constants.Red};\">uvicorn</span> app.main:app --port $Env:PORT --reload --log-level debug</span>")]
    public void ShouldParse(string input, string expected)
    {
        Converter converter = new Converter();

        string actual = converter.Parse(input);

        Assert.Equal(expected, actual);
    }
}
