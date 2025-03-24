using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace TFMS.Wpf
{
    /// <summary>
    /// Interaction logic for TFMSView.xaml
    /// </summary>
    public partial class TFMSView : UserControl
    {
        public TFMSView(Theme theme)
        {
            InitializeComponent();

            DataContext = Ioc.Default.GetRequiredService<TFMSViewModel>();

            this.FontFamily = theme.Font.FontFamily.ToWindowsFontFamily();
            this.FontSize = theme.Font.Size;

            Background = new SolidColorBrush(theme.BackgroundColor.ToWindowsColor());
            Foreground = new SolidColorBrush(theme.ForegroundColor.ToWindowsColor());
        }

        public TFMSViewModel ViewModel => (TFMSViewModel)DataContext;
    }
}
