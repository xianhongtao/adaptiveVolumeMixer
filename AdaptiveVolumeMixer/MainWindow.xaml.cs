using System.Windows;
using System.Windows.Controls;
using AdaptiveVolumeMixer.ViewModels;

namespace AdaptiveVolumeMixer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 删除进程按钮点击事件
    /// 使用事件处理而非 Command 绑定，避免嵌套 ItemsControl 中绑定失效的问题
    /// </summary>
    private void RemoveProcessButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LevelItemViewModel item)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RemoveProcessCommand.Execute(item);
            }
        }
    }
}
