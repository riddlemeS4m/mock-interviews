using Microsoft.AspNetCore.Razor.TagHelpers;
using MockInterviews.TagHelpers;

namespace MockInterviews.UnitTests;

public sealed class UiIconTagHelperTests
{
    [Fact]
    public void Decorative_icon_renders_accessible_svg_without_helper_attributes()
    {
        var helper = new UiIconTagHelper { Name = "search" };
        var output = CreateOutput("search");

        helper.Process(CreateContext("search"), output);

        Assert.Equal("svg", output.TagName);
        Assert.Equal("true", output.Attributes["aria-hidden"].Value);
        Assert.False(output.Attributes.ContainsName("name"));
        Assert.Contains("<circle", output.Content.GetContent());
    }

    [Fact]
    public void Labeled_icon_exposes_an_accessible_name()
    {
        var helper = new UiIconTagHelper
        {
            Name = "triangle-alert",
            Label = "Warning"
        };
        var output = CreateOutput("triangle-alert", "Warning");

        helper.Process(CreateContext("triangle-alert", "Warning"), output);

        Assert.Equal("img", output.Attributes["role"].Value);
        Assert.Equal("Warning", output.Attributes["aria-label"].Value);
        Assert.False(output.Attributes.ContainsName("aria-hidden"));
        Assert.False(output.Attributes.ContainsName("label"));
    }

    private static TagHelperContext CreateContext(string name, string? label = null)
    {
        var attributes = CreateAttributes(name, label);
        return new TagHelperContext(attributes, new Dictionary<object, object>(), Guid.NewGuid().ToString());
    }

    private static TagHelperOutput CreateOutput(string name, string? label = null)
    {
        var attributes = CreateAttributes(name, label);
        return new TagHelperOutput(
            "ui-icon",
            attributes,
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
    }

    private static TagHelperAttributeList CreateAttributes(string name, string? label)
    {
        TagHelperAttributeList attributes = [new("name", name)];
        if (label is not null)
        {
            attributes.Add("label", label);
        }

        return attributes;
    }
}
