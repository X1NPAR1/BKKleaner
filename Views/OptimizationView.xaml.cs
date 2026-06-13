using System.Windows;
using System.Windows.Controls;
using BKKleaner.ViewModels;

namespace BKKleaner.Views;

public partial class OptimizationView : UserControl
{
    public OptimizationView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OptimizationViewModel vm && vm.StartupEntries.Count == 0)
            vm.LoadStartupCommand.Execute(null);
    }
}
