using System.ComponentModel.DataAnnotations;
using MockInterviews.Models.Entities;

namespace MockInterviews.Models.ViewModels.GlobalConfigVarsController;

public sealed record ConfigurationIndexViewModel(
    IReadOnlyList<Setting> Settings,
    ConfigurationEditViewModel Editor,
    string? ActiveDialog = null);

public sealed class ConfigurationEditViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Value { get; set; } = string.Empty;

    public static ConfigurationEditViewModel FromSetting(Setting setting) => new()
    {
        Id = setting.Id,
        Name = setting.Name,
        Value = setting.Value
    };
}
