namespace Jobs.Application;

public sealed record OutboxItem(
    Guid Id,
    string Type,
    string Payload);
