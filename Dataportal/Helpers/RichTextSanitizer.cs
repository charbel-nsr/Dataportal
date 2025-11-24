using AngleSharp.Dom;
using Ganss.Xss;

namespace Dataportal.Helpers;

public static class RichTextSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "strong", "b", "em", "i", "u", "ol", "ul", "li", "a", "h4", "br" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("title");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("text-align");

        sanitizer.KeepChildNodes = true;

        sanitizer.AllowedClasses.Clear();

        sanitizer.PostProcessNode += (_, args) =>
        {
            if (args.Node is IElement element &&
                element.TagName.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                var rel = element.GetAttribute("rel");
                if (string.IsNullOrWhiteSpace(rel))
                {
                    element.SetAttribute("rel", "noopener");
                }
            }
        };

        return sanitizer;
    }

    public static string Sanitize(string? rawHtml)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return string.Empty;
        }

        return Sanitizer.Sanitize(rawHtml);
    }
}