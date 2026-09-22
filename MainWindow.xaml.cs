using System.Windows;
using System.Windows.Input;

namespace ShowVersionNum
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty VersionTextProperty =
            DependencyProperty.Register(
                nameof(VersionText),
                typeof(string),
                typeof(MainWindow),
                new PropertyMetadata(string.Empty));

        public MainWindow()
            : this("请通过右键菜单选择一个zip压缩包查看版本号。")
        {
        }

        public MainWindow(string versionText)
        {
            VersionText = versionText;
            InitializeComponent();
            Loaded += (_, _) => VersionTextBox.Focus();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        public string VersionText
        {
            get => (string)GetValue(VersionTextProperty);
            set => SetValue(VersionTextProperty, value);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
