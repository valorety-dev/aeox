using System.Windows.Media;

namespace Aeox.App.ViewModels;

public enum TokenKind
{
    Text,
    Comment,
    Keyword,
    String,
    Muted,
    Removed
}

public sealed record PreviewToken(string Text, TokenKind Kind)
{
    public Brush Brush => Kind switch
    {
        TokenKind.Comment => Res("CodeCommentBrush"),
        TokenKind.Keyword => Res("CodeKeywordBrush"),
        TokenKind.String => Res("CodeStringBrush"),
        TokenKind.Muted => Res("FaintBrush"),
        TokenKind.Removed => Res("DangerBrush"),
        _ => Res("CodeTextBrush")
    };

    public bool Strike => Kind == TokenKind.Removed;

    private static Brush Res(string key) => (Brush)System.Windows.Application.Current.Resources[key];
}

public sealed record PreviewLine(int Number, IReadOnlyList<PreviewToken> Tokens, bool GapBefore);
