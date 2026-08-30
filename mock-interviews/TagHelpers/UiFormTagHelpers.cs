using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MockInterviews.TagHelpers;

[HtmlTargetElement("input", Attributes = "ui-control")]
[HtmlTargetElement("select", Attributes = "ui-control")]
[HtmlTargetElement("textarea", Attributes = "ui-control")]
public sealed class UiControlTagHelper : TagHelper
{
    private const string Classes =
        "block min-h-11 w-full rounded-md border border-line bg-surface px-3 py-2 text-ink shadow-sm placeholder:text-muted focus:border-ink focus:outline-none focus:ring-3 focus:ring-ink/10";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var classes = string.Equals(output.TagName, "select", StringComparison.OrdinalIgnoreCase)
            ? $"{Classes} appearance-none pr-10"
            : Classes;
        ApplyClasses(output, "ui-control", classes);

        if (string.Equals(output.TagName, "select", StringComparison.OrdinalIgnoreCase))
        {
            output.PreElement.SetHtmlContent("<span class=\"relative block\">");
            output.PostElement.SetHtmlContent("""
                <svg class="pointer-events-none absolute right-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="m6 9 6 6 6-6"></path></svg></span>
                """);
        }
    }

    internal static void ApplyClasses(TagHelperOutput output, string helperAttribute, string classes)
    {
        var existingClasses = output.Attributes["class"]?.Value?.ToString();
        output.Attributes.RemoveAll(helperAttribute);
        output.Attributes.SetAttribute("class", $"{classes} {existingClasses}".Trim());
    }
}

[HtmlTargetElement("label", Attributes = "ui-label")]
public sealed class UiLabelTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
        => UiControlTagHelper.ApplyClasses(output, "ui-label", "block text-sm font-semibold text-ink");
}

[HtmlTargetElement("span", Attributes = "ui-validation")]
[HtmlTargetElement("div", Attributes = "ui-validation-summary")]
public sealed class UiValidationTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var attribute = output.Attributes.ContainsName("ui-validation")
            ? "ui-validation"
            : "ui-validation-summary";
        var classes = attribute == "ui-validation"
            ? "mt-1.5 block text-sm text-negative"
            : "mb-5 border-l-4 border-l-negative pl-4 text-sm text-negative";
        UiControlTagHelper.ApplyClasses(output, attribute, classes);
    }
}
