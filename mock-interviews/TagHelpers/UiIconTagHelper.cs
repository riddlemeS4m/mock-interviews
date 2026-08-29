using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MockInterviews.TagHelpers;

[HtmlTargetElement("ui-icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class UiIconTagHelper : TagHelper
{
    private static readonly IReadOnlyDictionary<string, string> Icons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["building-2"] = """
                <path d="M10 12h4" />
                <path d="M10 8h4" />
                <path d="M14 21v-3a2 2 0 0 0-4 0v3" />
                <path d="M6 10H4a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-2" />
                <path d="M6 21V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v16" />
                """,
            ["calendar-days"] = """
                <path d="M8 2v3" />
                <path d="M16 2v3" />
                <rect x="3" y="3" width="18" height="18" rx="2" />
                <path d="M3 9h18" />
                <path d="M8 13h.01" />
                <path d="M12 13h.01" />
                <path d="M16 13h.01" />
                <path d="M8 17h.01" />
                <path d="M12 17h.01" />
                <path d="M16 17h.01" />
                """,
            ["chart-no-axes-combined"] = """
                <path d="M12 16v5" />
                <path d="M16 14.639V21" />
                <path d="M20 10.656V21" />
                <path d="m22 3-8.646 8.646a.5.5 0 0 1-.708 0L9.354 8.354a.5.5 0 0 0-.707 0L2 15" />
                <path d="M4 18.463V21" />
                <path d="M8 14.656V21" />
                """,
            ["chevron-down"] = "<path d=\"m6 9 6 6 6-6\" />",
            ["chevron-right"] = "<path d=\"m9 18 6-6-6-6\" />",
            ["circle-check"] = """
                <circle cx="12" cy="12" r="10" />
                <path d="m16 9-5.5 5.5L8 12" />
                """,
            ["circle-x"] = """
                <circle cx="12" cy="12" r="10" />
                <path d="m15 9-6 6" />
                <path d="m9 9 6 6" />
                """,
            ["clipboard-check"] = """
                <rect width="8" height="4" x="8" y="2" rx="1" ry="1" />
                <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
                <path d="m9 14 2 2 4-4" />
                """,
            ["clock"] = """
                <circle cx="12" cy="12" r="10" />
                <path d="M12 6v6l4 2" />
                """,
            ["inbox"] = """
                <polyline points="22 12 16 12 14 15 10 15 8 12 2 12" />
                <path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" />
                """,
            ["layout-dashboard"] = """
                <rect width="7" height="9" x="3" y="3" rx="1" />
                <rect width="7" height="5" x="14" y="3" rx="1" />
                <rect width="7" height="9" x="14" y="12" rx="1" />
                <rect width="7" height="5" x="3" y="16" rx="1" />
                """,
            ["map-pin"] = """
                <path d="M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0" />
                <circle cx="12" cy="10" r="3" />
                """,
            ["menu"] = """
                <path d="M4 5h16" />
                <path d="M4 12h16" />
                <path d="M4 19h16" />
                """,
            ["search"] = """
                <path d="m21 21-4.34-4.34" />
                <circle cx="11" cy="11" r="8" />
                """,
            ["trash-2"] = """
                <path d="M10 11v6" />
                <path d="M14 11v6" />
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
                <path d="M3 6h18" />
                <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                """,
            ["triangle-alert"] = """
                <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3" />
                <path d="M12 9v4" />
                <path d="M12 17h.01" />
                """,
            ["user-round"] = """
                <circle cx="12" cy="8" r="5" />
                <path d="M20 21a8 8 0 0 0-16 0" />
                """,
            ["users"] = """
                <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
                <path d="M16 3.128a4 4 0 0 1 0 7.744" />
                <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
                <circle cx="9" cy="7" r="4" />
                """
        };

    [HtmlAttributeName("name")]
    public string Name { get; set; } = string.Empty;

    [HtmlAttributeName("label")]
    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!Icons.TryGetValue(Name, out var content))
        {
            throw new InvalidOperationException($"Unknown UI icon '{Name}'.");
        }

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.RemoveAll("name");
        output.Attributes.RemoveAll("label");
        output.Attributes.SetAttribute("viewBox", "0 0 24 24");
        output.Attributes.SetAttribute("fill", "none");
        output.Attributes.SetAttribute("stroke", "currentColor");
        output.Attributes.SetAttribute("stroke-width", "2");
        output.Attributes.SetAttribute("stroke-linecap", "round");
        output.Attributes.SetAttribute("stroke-linejoin", "round");
        output.Attributes.SetAttribute("focusable", "false");

        if (string.IsNullOrWhiteSpace(Label))
        {
            output.Attributes.SetAttribute("aria-hidden", "true");
        }
        else
        {
            output.Attributes.SetAttribute("role", "img");
            output.Attributes.SetAttribute("aria-label", Label);
        }

        output.Content.SetHtmlContent(content);
    }
}
