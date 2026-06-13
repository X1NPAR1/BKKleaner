namespace BKKleaner.Services;

/// <summary>
/// Decouples pages from the shell: any view model can request navigation to a page
/// key without referencing MainViewModel (which would create a dependency cycle).
/// </summary>
public interface INavigationService
{
    event EventHandler<string>? NavigationRequested;
    void NavigateTo(string pageKey);
}

public sealed class NavigationService : INavigationService
{
    public event EventHandler<string>? NavigationRequested;
    public void NavigateTo(string pageKey) => NavigationRequested?.Invoke(this, pageKey);
}
