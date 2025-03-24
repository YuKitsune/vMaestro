using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace TFMS.Wpf
{
    public enum BevelType
    {
        Raised,
        Sunken
    }

    /// <summary>
    /// Interaction logic for BeveledBorder.xaml
    /// </summary>
    [ContentProperty(nameof(Children))]
    public partial class BeveledBorder : UserControl
    {
        static readonly DependencyProperty BevelTypeProperty = DependencyProperty.Register(
            "BevelType", typeof(BevelType), typeof(BeveledBorder), new FrameworkPropertyMetadata(BevelType.Raised, FrameworkPropertyMetadataOptions.AffectsRender, OnBevelTypeChanged));

        static readonly DependencyProperty GurthProperty = DependencyProperty.Register(
            "Gurth", typeof(Thickness), typeof(BeveledBorder), new FrameworkPropertyMetadata(new Thickness(2, 2, 2, 2), FrameworkPropertyMetadataOptions.AffectsRender, OnGurthChanged));


        public static readonly DependencyPropertyKey ChildrenProperty = DependencyProperty.RegisterReadOnly(
            nameof(Children),  // Prior to C# 6.0, replace nameof(Children) with "Children"
            typeof(UIElementCollection),
            typeof(BeveledBorder),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnChildrenChanged));

        public BeveledBorder()
        {
            InitializeComponent();
            Children = Content.Children;
        }

        public BevelType BevelType
        {
            get { return (BevelType)GetValue(BevelTypeProperty); }
            set { SetValue(BevelTypeProperty, value); }
        }

        public Thickness Gurth
        {
            get { return (Thickness)GetValue(GurthProperty); }
            set { SetValue(GurthProperty, value); }
        }

        public UIElementCollection Children
        {
            get { return (UIElementCollection)GetValue(ChildrenProperty.DependencyProperty); }
            private set { SetValue(ChildrenProperty, value); }
        }

        static void OnBevelTypeChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is BeveledBorder beveledBorder)
            {
                beveledBorder.BevelType = (BevelType)e.NewValue;
            }
        }

        static void OnGurthChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is BeveledBorder beveledBorder)
            {
                beveledBorder.Gurth = (Thickness)e.NewValue;
            }
        }

        static void OnChildrenChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is BeveledBorder beveledBorder)
            {
                if (e.NewValue is null)
                {
                    beveledBorder.Children.Clear();
                }

                if (e.NewValue is UIElementCollection uiElementCollection)
                {
                    beveledBorder.Children = uiElementCollection;
                }
            }
        }
    }
}
