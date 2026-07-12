using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Application.CQRS.Shared.Outbox;

// ── Queries ─────────────────────────────────────────────────────

public sealed record GetOutboxMessagesQuery(
    OutboxMessageStatusDto? Status = null,
    string? TypeFilter = null,
    int Page = 1,
    int PageSize = 50
) : IQuery<ApiResponse<OutboxMessageListResultDto>>;

public sealed record GetOutboxMessageByIdQuery(Guid Id)
    : IQuery<ApiResponse<OutboxMessageDto>>;

// ── Commands ────────────────────────────────────────────────────

public sealed record RequeueOutboxMessageCommand(Guid Id)
    : ICommand<ApiResponse<bool>>;

public sealed record RequeueAllFailedOutboxMessagesCommand()
    : ICommand<ApiResponse<int>>;

public sealed record ProcessOutboxNowCommand(int BatchSize = 50)
    : ICommand<ApiResponse<ProcessOutboxResultDto>>;

// ── Handlers ────────────────────────────────────────────────────

public sealed class GetOutboxMessagesHandler
    : IQueryHandler<GetOutboxMessagesQuery, ApiResponse<OutboxMessageListResultDto>>
{
    private readonly IOutboxAdminStore _store;

    public GetOutboxMessagesHandler(IOutboxAdminStore store) => _store = store;

    public async Task<ApiResponse<OutboxMessageListResultDto>> HandleAsync(
        GetOutboxMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var result = await _store.ListAsync(query.Status, query.TypeFilter, page, pageSize, cancellationToken);
        return new ApiResponse<OutboxMessageListResultDto>(true, null, result);
    }
}

public sealed class GetOutboxMessageByIdHandler
    : IQueryHandler<GetOutboxMessageByIdQuery, ApiResponse<OutboxMessageDto>>
{
    private readonly IOutboxAdminStore _store;

    public GetOutboxMessageByIdHandler(IOutboxAdminStore store) => _store = store;

    public async Task<ApiResponse<OutboxMessageDto>> HandleAsync(
        GetOutboxMessageByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var item = await _store.GetByIdAsync(query.Id, cancellationToken);
        return item == null
            ? new ApiResponse<OutboxMessageDto>(false, "Outbox message not found", null)
            : new ApiResponse<OutboxMessageDto>(true, null, item);
    }
}

public sealed class RequeueOutboxMessageHandler
    : ICommandHandler<RequeueOutboxMessageCommand, ApiResponse<bool>>
{
    private readonly IOutboxAdminStore _store;

    public RequeueOutboxMessageHandler(IOutboxAdminStore store) => _store = store;

    public async Task<ApiResponse<bool>> HandleAsync(
        RequeueOutboxMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        var ok = await _store.RequeueAsync(command.Id, cancellationToken);
        return ok
            ? new ApiResponse<bool>(true, "Message requeued for processing", true)
            : new ApiResponse<bool>(false, "Message not found or cannot be requeued (already processed?)", false);
    }
}

public sealed class RequeueAllFailedOutboxMessagesHandler
    : ICommandHandler<RequeueAllFailedOutboxMessagesCommand, ApiResponse<int>>
{
    private readonly IOutboxAdminStore _store;

    public RequeueAllFailedOutboxMessagesHandler(IOutboxAdminStore store) => _store = store;

    public async Task<ApiResponse<int>> HandleAsync(
        RequeueAllFailedOutboxMessagesCommand command,
        CancellationToken cancellationToken = default)
    {
        var count = await _store.RequeueAllFailedAsync(cancellationToken);
        return new ApiResponse<int>(true, $"Requeued {count} failed message(s)", count);
    }
}

public sealed class ProcessOutboxNowHandler
    : ICommandHandler<ProcessOutboxNowCommand, ApiResponse<ProcessOutboxResultDto>>
{
    private readonly IOutboxProcessor _processor;

    public ProcessOutboxNowHandler(IOutboxProcessor processor) => _processor = processor;

    public async Task<ApiResponse<ProcessOutboxResultDto>> HandleAsync(
        ProcessOutboxNowCommand command,
        CancellationToken cancellationToken = default)
    {
        var batch = Math.Clamp(command.BatchSize, 1, 200);
        var processed = await _processor.ProcessPendingAsync(batch, cancellationToken);
        var dto = new ProcessOutboxResultDto(processed, $"Processed {processed} outbox message(s)");
        return new ApiResponse<ProcessOutboxResultDto>(true, dto.Message, dto);
    }
}
