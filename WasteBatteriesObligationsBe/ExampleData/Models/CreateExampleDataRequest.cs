using System.ComponentModel.DataAnnotations;

namespace WasteBatteriesObligationsBe.ExampleData.Models;

public sealed class CreateExampleDataRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(255)]
    public string ExampleText { get; init; } = string.Empty;
}
