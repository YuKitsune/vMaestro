using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFMS.Wpf
{
    public class Theme
    {
        public Font Font { get; set; }
        public Color BackgroundColor { get; set; }
        public Color ForegroundColor { get; set; }
        public Color ButtonHoverColor { get; set; }
        public Color BorderColor { get; set; }

        public Color BorderColorTopLeft { get; set; }

        public Color BorderColorBottomRight { get; set; }

        public static Theme Default => new Theme
        {
            Font = new Font("Terminus (TTF)", 14f),
            BackgroundColor = SystemColors.ControlLight,
            ForegroundColor = Color.Salmon,
        };
    }
}
