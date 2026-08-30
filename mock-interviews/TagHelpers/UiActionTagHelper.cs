using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MockInterviews.TagHelpers;

public enum UiActionVariant
{
    Primary,
    Secondary,
    Quiet,
    Destructive
}

[HtmlTargetElement("a", Attributes = "ui-variant")]
[HtmlTargetElement("button", Attributes = "ui-variant")]
[HtmlTargetElement("input", Attributes = "ui-variant")]
public sealed class UiActionTagHelper : TagHelper
{
    private const string BaseClasses =
        "inline-flex min-h-10 items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600";

    [HtmlAttributeName("ui-variant")]
    public UiActionVariant Variant { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var variantClasses = Variant switch
        {
            UiActionVariant.Primary => "bg-ink text-white shadow-sm hover:bg-ink/90",
            UiActionVariant.Secondary => "border border-line bg-surface text-ink shadow-sm hover:bg-canvas",
            UiActionVariant.Quiet => "text-ink hover:bg-soft",
            UiActionVariant.Destructive => "bg-negative text-white shadow-sm hover:bg-negative/90",
            _ => throw new InvalidOperationException($"Unknown UI action variant '{Variant}'.")
        };

        var existingClasses = output.Attributes["class"]?.Value?.ToString();
        output.Attributes.RemoveAll("ui-variant");
        output.Attributes.SetAttribute("class", $"{BaseClasses} {variantClasses} {existingClasses}".Trim());
    }
}
