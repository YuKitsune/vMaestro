using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;

namespace Maestro.Avalonia.Controls;

public partial class TextInput : UserControl
{
    public TextInput()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<int> MaxCharactersProperty =
        AvaloniaProperty.Register<TextInput, int>(nameof(MaxCharacters), 100);

    public int MaxCharacters
    {
        get => GetValue(MaxCharactersProperty);
        set => SetValue(MaxCharactersProperty, value);
    }

    public static readonly StyledProperty<bool> DigitsOnlyProperty =
        AvaloniaProperty.Register<TextInput, bool>(nameof(DigitsOnly), false);

    public bool DigitsOnly
    {
        get => GetValue(DigitsOnlyProperty);
        set => SetValue(DigitsOnlyProperty, value);
    }

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<TextInput, string>(nameof(Value), string.Empty);

    public string Value
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DigitsOnly)
        {
            var digitsOnly = Regex.Replace(InputBox.Text, "[^0-9]", "");
            if (InputBox.Text != digitsOnly)
            {
                var caretIndex = InputBox.CaretIndex;
                InputBox.Text = digitsOnly;
                InputBox.CaretIndex = caretIndex > digitsOnly.Length ? digitsOnly.Length : caretIndex;
            }
        }
    }
}
