using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Aeox.App.ViewModels;

namespace Aeox.App.Controls;

public static class TokenText
{
    public static readonly DependencyProperty TokensProperty = DependencyProperty.RegisterAttached(
        "Tokens", typeof(IEnumerable<PreviewToken>), typeof(TokenText), new PropertyMetadata(null, OnTokensChanged));

    public static void SetTokens(DependencyObject element, IEnumerable<PreviewToken>? value) =>
        element.SetValue(TokensProperty, value);

    public static IEnumerable<PreviewToken>? GetTokens(DependencyObject element) =>
        (IEnumerable<PreviewToken>?)element.GetValue(TokensProperty);

    private static void OnTokensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block) return;
        block.Inlines.Clear();
        if (e.NewValue is not IEnumerable<PreviewToken> tokens) return;
        foreach (var token in tokens)
        {
            var run = new Run(token.Text) { Foreground = token.Brush };
            if (token.Strike) run.TextDecorations = TextDecorations.Strikethrough;
            block.Inlines.Add(run);
        }
    }
}
