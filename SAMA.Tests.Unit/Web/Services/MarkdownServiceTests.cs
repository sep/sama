using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class MarkdownServiceTests
{
    private MarkdownService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new MarkdownService();
    }

    [TestMethod]
    public void RenderToHtmlShouldReturnEmptyStringForNullInput()
    {
        var result = _service.RenderToHtml(null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void RenderToHtmlShouldReturnEmptyStringForWhitespaceInput()
    {
        var result = _service.RenderToHtml("   \n\t  ");

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void RenderToHtmlShouldRenderMarkdown()
    {
        var result = _service.RenderToHtml("**bold** and *italic*");

        StringAssert.Contains(result, "<strong>bold</strong>");
        StringAssert.Contains(result, "<em>italic</em>");
    }

    [TestMethod]
    public void RenderToHtmlShouldAddTargetBlankToLinks()
    {
        var result = _service.RenderToHtml("[Example](https://example.com)");

        StringAssert.Contains(result, "target=\"_blank\"");
        StringAssert.Contains(result, "rel=\"noopener noreferrer\"");
    }

    [TestMethod]
    public void RenderToHtmlShouldSanitizeDangerousContent()
    {
        var result = _service.RenderToHtml("<script>alert('xss')</script>Safe text");

        Assert.DoesNotContain("<script>", result);
        StringAssert.Contains(result, "Safe text");
    }

    [TestMethod]
    public void RenderToHtmlShouldBlockJavascriptUrls()
    {
        var result = _service.RenderToHtml("[Click](javascript:alert('xss'))");

        Assert.DoesNotContain("javascript:", result);
    }
}
