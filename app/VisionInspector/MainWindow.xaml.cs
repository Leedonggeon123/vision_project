using System.Windows;
using VisionInspector.ViewModels;

namespace VisionInspector;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
