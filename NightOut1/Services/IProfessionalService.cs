using NightOut.Models;

namespace NightOut.Services;

public interface IProfessionalService
{
    Task<ProfessionalAccount?> GetCurrentProfessionalAccountAsync();

    Task<ProfessionalAccount?> EnsureCurrentProfessionalAccountAsync();

    Task<bool> SaveProfessionalAccountAsync(ProfessionalAccount account);

    Task<Bar?> SaveProfessionalAccountForBarAsync(ProfessionalAccount account, string? selectedBarId, bool createNewBar);

    Task<string?> UploadProfessionalImageAsync(
        string accountId,
        FileResult file,
        string imageType);

    Task<Bar?> GetLinkedBarAsync(string professionalAccountId);

    Task<List<Bar>> GetBarsForProfessionalAsync(string professionalAccountId);

    Task<List<BarOpeningHour>> GetOpeningHoursForProfessionalAsync(string professionalAccountId);

    Task<List<BarOpeningHour>> GetOpeningHoursForBarAsync(string barId);

    Task<bool> SaveOpeningHoursForProfessionalAsync(string professionalAccountId, IEnumerable<BarOpeningHour> hours);

    Task<bool> SaveOpeningHoursForBarAsync(string barId, IEnumerable<BarOpeningHour> hours);

    Task<BarClaimRequest?> GetMyClaimRequestForBarAsync(string barId);

    Task<BarClaimRequest?> CreateBarClaimRequestAsync(
        string barId,
        string contactName,
        string role,
        string phone,
        string proofMessage);
}
