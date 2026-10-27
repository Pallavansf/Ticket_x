using System.Text.RegularExpressions;

namespace PA.Tickets.Application.Tickets;

public static class TicketIdentifier
{
    private static readonly Regex TicketNumberPattern = new(
        "^(REQ|INC|PRB|TSK|CHG)-[0-9]{6,}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PublicIdPattern = new(
        "^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{16}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizeForLookup(string? identifier)
    {
        var value = identifier?.Trim();
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("A ticket number or 16-character public ID is required.", nameof(identifier));

        if (TicketNumberPattern.IsMatch(value))
            return value.ToUpperInvariant();

        if (PublicIdPattern.IsMatch(value))
            return value;

        throw new ArgumentException("Use a ticket number with at least six digits or a 16-character public ID.", nameof(identifier));
    }

    public static bool IsTicketNumber(string normalizedIdentifier) => TicketNumberPattern.IsMatch(normalizedIdentifier);
}
