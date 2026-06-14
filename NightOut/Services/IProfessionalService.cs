using NightOut.Models;

namespace NightOut.Services;

public interface IProfessionalService
{
    Task<ProfessionalAccount?> GetCurrentProfessionalAccountAsync();

    Task<ProfessionalAccount?> EnsureCurrentProfessionalAccountAsync();

    Task<bool> SaveProfessionalAccountAsync(ProfessionalAccount account);

    Task<string?> UploadProfessionalImageAsync(
        string accountId,
        FileResult file,
        string imageType);
}