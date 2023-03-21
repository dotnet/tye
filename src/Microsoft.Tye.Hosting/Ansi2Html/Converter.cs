using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Tye.Hosting.Ansi2Html
{
    public class Converter
    {
        private readonly Regex _rule = new Regex("\u001b\\[(?<code>\\d+)(?<args>;\\d+)*m", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public string Parse(string input)
        {
            var htmlText = _rule.Replace(input, match =>
            {
                var code = Convert.ToInt32(match.Groups["code"].Value);
                var args = match.Groups["args"].Value;
                List<string> attributes = args.Split(';').ToList();

                var tagBuilder = new StringBuilder();
                if (code == Constants.SelectGraphicRenditionParameters.Reset)
                {
                    tagBuilder.Append("</span>");
                }
                else
                {
                    var color = Constants.White;
                    if (attributes.Count > 0)
                    {
                        string colorCode = attributes.Last();
                        if (Constants.ColorMap.ContainsKey(colorCode))
                        {
                            color = Constants.ColorMap[colorCode];
                        }
                    }

                    tagBuilder.Append("<span style=\"color:");
                    tagBuilder.Append(color);
                    tagBuilder.Append(";\">");
                }

                return tagBuilder.ToString();
            });

            return htmlText;
        }
    }
}
