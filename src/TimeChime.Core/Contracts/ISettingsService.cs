using TimeChime.Core.Models;

namespace TimeChime.Core.Contracts;

public interface ISettingsService
{
    TimeChimeSettings CurrentSettings { get; }
    Task<TimeChimeSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TimeChimeSettings settings, CancellationToken cancellationToken = default);
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
    event Action<TimeChimeSettings>? SettingsChanged;
}

