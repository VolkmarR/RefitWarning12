using Refit;

namespace WebApplication1;

internal interface IRefitInterface
{
    [Get("/api/prezzarioRegionale/voci/{voceId}/detailsPage")]
    Task<ApiResponse<string>> GetVoiceDetailsAsync(string voceId, [Query] string? lang);
}