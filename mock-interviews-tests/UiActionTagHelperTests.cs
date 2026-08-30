using Microsoft.AspNetCore.Razor.TagHelpers;
using MockInterviews.TagHelpers;

namespace MockInterviews.UnitTests;

public sealed class UiActionTagHelperTests
{
    [Theory]
    [InlineData(UiActionVariant.Primary, "bg-ink")]
    [InlineData(UiActionVariant.Secondary, "border-line")]
    [InlineData(UiActionVariant.Quiet, "hover:bg-soft")]
    [InlineData(UiActionVariant.Destructive, "bg-negative")]
    public void Variant_adds_expected_classes_and_preserves_existing_classes(
        UiActionVariant variant,
        string expectedClass)
    {
        var helper = new UiActionTagHelper { Variant = variant };
        TagHelperAttributeList attributes =
        [
            new("ui-variant", variant.ToString()),
            new("class", "mt-4")
        ];
        var context = new TagHelperContext(attributes, new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "button",
            attributes,
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);

        var classes = output.Attributes["class"].Value?.ToString();
        Assert.Contains(expectedClass, classes);
        Assert.Contains("mt-4", classes);
        Assert.False(output.Attributes.ContainsName("ui-variant"));
    }
}
