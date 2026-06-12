using Appointment_SaaS.Core.Utilities;
using Xunit;

namespace Appointment_SaaS.Test;

public class HtmlInputSanitizerTests
{
    [Fact]
    public void SanitizeText_RemovesScriptTags()
    {
        var result = HtmlInputSanitizer.SanitizeText("<script>alert(1)</script>Merhaba");
        Assert.DoesNotContain("<script", result);
        Assert.Contains("Merhaba", result);
    }

    [Fact]
    public void ContainsXss_DetectsEventHandlers()
    {
        Assert.True(HtmlInputSanitizer.ContainsXss("<img src=x onerror=alert(1)>"));
    }

    [Fact]
    public void EscapeForJavaScript_EscapesQuotes()
    {
        var result = HtmlInputSanitizer.EscapeForJavaScript("O'Brien");
        Assert.Contains("\\'", result);
    }

    [Fact]
    public void HtmlEncode_EncodesAngleBrackets()
    {
        Assert.Equal("&lt;b&gt;", HtmlInputSanitizer.HtmlEncode("<b>"));
    }
}
