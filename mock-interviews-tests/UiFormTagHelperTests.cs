using Microsoft.AspNetCore.Razor.TagHelpers;
using MockInterviews.TagHelpers;

namespace MockInterviews.UnitTests;

public sealed class UiFormTagHelperTests
{
    [Fact]
    public void Control_adds_design_system_classes_and_preserves_existing_classes()
    {
        var output = CreateOutput("input", "ui-control", "custom-class");

        new UiControlTagHelper().Process(CreateContext(output.Attributes), output);

        Assert.Contains("border-line", output.Attributes["class"].Value?.ToString());
        Assert.Contains("focus:ring-ink/10", output.Attributes["class"].Value?.ToString());
        Assert.Contains("custom-class", output.Attributes["class"].Value?.ToString());
        Assert.False(output.Attributes.ContainsName("ui-control"));
    }

    [Fact]
    public void Validation_summary_uses_the_semantic_error_rail()
    {
        var output = CreateOutput("div", "ui-validation-summary");

        new UiValidationTagHelper().Process(CreateContext(output.Attributes), output);

        Assert.Contains("border-l-negative", output.Attributes["class"].Value?.ToString());
        Assert.False(output.Attributes.ContainsName("ui-validation-summary"));
    }

    [Fact]
    public void Select_uses_the_design_system_chevron_instead_of_the_browser_arrow()
    {
        var output = CreateOutput("select", "ui-control");

        new UiControlTagHelper().Process(CreateContext(output.Attributes), output);

        Assert.Contains("appearance-none", output.Attributes["class"].Value?.ToString());
        Assert.Contains("relative", output.PreElement.GetContent());
        Assert.Contains("m6 9 6 6 6-6", output.PostElement.GetContent());
        Assert.Contains("aria-hidden=\"true\"", output.PostElement.GetContent());
    }

    private static TagHelperContext CreateContext(TagHelperAttributeList attributes)
        => new(attributes, new Dictionary<object, object>(), Guid.NewGuid().ToString());

    private static TagHelperOutput CreateOutput(string tag, string helperAttribute, string? cssClass = null)
    {
        TagHelperAttributeList attributes = [new(helperAttribute)];
        if (cssClass is not null)
        {
            attributes.Add("class", cssClass);
        }

        return new TagHelperOutput(
            tag,
            attributes,
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
    }
}
