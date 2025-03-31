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

namespace TFMS.Wpf.Controls
{
    public enum Direction
    {
        Left, Up, Right, Down
    };

    /// <summary>
    /// Interaction logic for Chevron.xaml
    /// </summary>
    public partial class Chevron : UserControl
    {
        public static DependencyProperty DirectionProperty =
            DependencyProperty.Register(
                nameof(Direction),
                typeof(Direction),
                typeof(Chevron),
                new FrameworkPropertyMetadata(Direction.Up));

        public Direction Direction
        { 
            get => (Direction) GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        public Chevron()
        {
            InitializeComponent();
        }
    }
}
