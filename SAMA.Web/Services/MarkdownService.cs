using Ganss.Xss;
using Markdig;

namespace SAMA.Web.Services;

/// <summary>
/// Service for rendering Markdown content to sanitized HTML.
/// </summary>
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        _sanitizer = new HtmlSanitizer();

        // Configure allowed tags (default set is good, but let's be explicit)
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "strong", "b", "em", "i", "u", "s", "del",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "ul", "ol", "li",
            "a", "code", "pre", "blockquote",
            "hr", "table", "thead", "tbody", "tr", "th", "td"
        })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        // Configure allowed attributes
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("title");

        // Allow only http, https, and mailto URLs
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        // Add target="_blank" and rel="noopener noreferrer" to all links
        _sanitizer.PostProcessNode += (sender, args) =>
        {
            if (args.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor)
            {
                anchor.Target = "_blank";
                anchor.SetAttribute("rel", "noopener noreferrer");
            }
        };
    }

    public virtual string RenderToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.ToHtml(markdown, _pipeline);
        return _sanitizer.Sanitize(html);
    }
}
