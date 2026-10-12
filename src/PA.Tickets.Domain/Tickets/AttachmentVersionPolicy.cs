namespace PA.Tickets.Domain.Tickets;

public static class AttachmentVersionPolicy
{
    public static void ValidateTransition(string previousStatus, string nextStatus)
    {
        if (nextStatus is not ("CURRENT" or "SUPERSEDED" or "RETIRED")) throw new ArgumentException("Invalid attachment version status.");
        if (previousStatus == "RETIRED" && nextStatus != "RETIRED") throw new InvalidOperationException("Retired evidence cannot become current again.");
    }
}

