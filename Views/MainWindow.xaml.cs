using System.Windows;
using BKKleaner.ViewModels;

namespace BKKleaner.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
