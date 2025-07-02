using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Maestro.Wpf.Controls;

public partial class TextInput : UserControl
{
    public TextInput()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty MaxCharactersProperty =
        DependencyProperty.Register(nameof(MaxCharacters), typeof(int), typeof(TextInput), new PropertyMetadata(100));

    public int MaxCharacters
    {
        get => (int)GetValue(MaxCharactersProperty);
        set => SetValue(MaxCharactersProperty, value);
    }

    public static readonly DependencyProperty DigitsOnlyProperty =
        DependencyProperty.Register(nameof(DigitsOnly), typeof(bool), typeof(TextInput), new PropertyMetadata(false));

    public bool DigitsOnly
    {
        get => (bool)GetValue(DigitsOnlyProperty);
        set => SetValue(DigitsOnlyProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(TextInput), new PropertyMetadata(""));

    public string Value
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    private void InputBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (DigitsOnly)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]*$");
        }
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