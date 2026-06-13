using BKKleaner.Models;

namespace BKKleaner.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler? SettingsChanged;
    void Save();
    void Update(Action<AppSettings> mutate);
    string DataDirectory { get; }
}
