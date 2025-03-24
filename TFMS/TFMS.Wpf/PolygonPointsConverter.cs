using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace TFMS.Wpf
{
    public class PolygonPointsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var polygonPoints = new PointCollection();
            if (values.Length == 3 && values[2] is Thickness)
            {
                var width = System.Convert.ToDouble(values[0]);
                var height = System.Convert.ToDouble(values[1]);
                var margin = (Thickness)values[2];

                if (System.Convert.ToInt32(parameter) == 0)
                {
                    polygonPoints.Add(new Point(0 + margin.Left, height - margin.Bottom));
                    polygonPoints.Add(new Point(0, height));
                    polygonPoints.Add(new Point(0, 0));
                    polygonPoints.Add(new Point(width, 0));
                    polygonPoints.Add(new Point(width - margin.Right, 0 + margin.Top));
                }
                else
                {
                    polygonPoints.Add(new Point(0 + margin.Left, height - margin.Bottom));
                    polygonPoints.Add(new Point(0, height));
                    polygonPoints.Add(new Point(width, height));
                    polygonPoints.Add(new Point(width, 0));
                    polygonPoints.Add(new Point(width - margin.Right, 0 + margin.Top));
                }
            }
            return polygonPoints;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
