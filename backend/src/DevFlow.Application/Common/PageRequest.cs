using System.ComponentModel.DataAnnotations;
namespace DevFlow.Application.Common;

// Shared query contract for future list endpoints. Validation runs at the HTTP boundary.
public sealed class PageRequest
{
    [Range(1, 1000000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 25;
}
