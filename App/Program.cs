namespace BKKleaner.App;

/// <summary>Explicit entry point (the markup compiler does not always emit one for non-root App.xaml).</summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
