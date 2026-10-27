namespace PA.Tickets.Api.Realtime;

public static class RealtimeGroups
{
    public static string User(long userId) => $"user:{userId}";
    public static string Ticket(long ticketId) => $"ticket:{ticketId}";
}
