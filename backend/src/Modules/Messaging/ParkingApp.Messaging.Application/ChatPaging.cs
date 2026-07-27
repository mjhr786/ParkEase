namespace ParkingApp.Messaging.Application;

/// <summary>
/// Canonical paging limits for chat list/message queries.
/// Caps protect DB/memory under abusive or buggy clients without changing default client behavior.
/// </summary>
public static class ChatPaging
{
    public const int DefaultConversationPageSize = 20;
    public const int MaxConversationPageSize = 50;
    public const int DefaultMessagePageSize = 50;
    public const int MaxMessagePageSize = 100;

    public static (int Page, int PageSize) ClampConversations(int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var size = pageSize <= 0 ? DefaultConversationPageSize : pageSize;
        if (size > MaxConversationPageSize)
            size = MaxConversationPageSize;
        return (safePage, size);
    }

    public static (int Page, int PageSize) ClampMessages(int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var size = pageSize <= 0 ? DefaultMessagePageSize : pageSize;
        if (size > MaxMessagePageSize)
            size = MaxMessagePageSize;
        return (safePage, size);
    }
}
