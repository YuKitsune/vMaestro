using System;
using System.Collections.Generic;
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

namespace TFMS.Wpf
{
    /// <summary>
    /// Interaction logic for Separator.xaml
    /// </summary>
    public partial class Separator : UserControl
    {
        public static DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(Separator), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsRender, OnOrientationChanged));

        public Separator()
        {
            InitializeComponent();
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        static void OnOrientationChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is Separator separator)
            {
                separator.Orientation = (Orientation)args.NewValue;
            }
        }
    }
}
