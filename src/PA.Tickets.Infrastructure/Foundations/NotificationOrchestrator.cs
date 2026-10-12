using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Communications;
using PA.Tickets.Infrastructure.Persistence;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class NotificationOrchestrator(
    TicketsDbContext db,
    ICommunicationService communicationService,
    ITicketPriorityConfigurationRepository priorities) : INotificationOrchestrator
{
    public async Task<NotificationProcessResult> ProcessAsync(NotificationEventEnvelope envelope, long actor, CancellationToken ct)
    {
        if (actor <= 0) throw new UnauthorizedAccessException("A server-authenticated actor is required.");
        _ = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == envelope.EventId, ct) ?? throw new KeyNotFoundException("Event was not found.");

        var now = DateTimeOffset.UtcNow;
        var rules = await QueryActiveRulesAsync(envelope.EventType, now, ct);
        if (rules.Count == 0)
        {
            var executionId = await RecordExecutionAsync(new NotificationExecutionRow
            {
                TicketId = envelope.TicketId,
                EventId = envelope.EventId,
                RuleId = null,
                RuleVersionId = null,
                Status = "SKIPPED",
                EvaluatedAt = now,
                Matched = false,
                SkipReason = "NO_MATCHING_RULE",
                CreatedCommunicationCount = 0,
                CorrelationId = envelope.CorrelationId,
                CreatedAt = now,
                CompletedAt = now,
                NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, envelope.EventCategory, envelope.EventAt, envelope.Payload, envelope.TicketId })
            }, ct);

            return new(false, "SKIPPED", "NO_MATCHING_RULE", 0, [], null, null, executionId, envelope.CorrelationId);
        }

        foreach (var rule in rules)
        {
            var result = await TryProcessRuleAsync(envelope, rule, actor, now, ct);
            if (result is not null) return result;
        }

        var fallbackExecutionId = await RecordExecutionAsync(new NotificationExecutionRow
        {
            TicketId = envelope.TicketId,
            EventId = envelope.EventId,
            RuleId = null,
            RuleVersionId = null,
            Status = "SKIPPED",
            EvaluatedAt = now,
            Matched = false,
            SkipReason = "NO_MATCHING_RULE",
            CreatedCommunicationCount = 0,
            CorrelationId = envelope.CorrelationId,
            CreatedAt = now,
            CompletedAt = now,
            NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, envelope.EventCategory, envelope.EventAt, envelope.Payload, envelope.TicketId, reason = "NO_MATCHING_RULE" })
        }, ct);

        return new(false, "SKIPPED", "NO_MATCHING_RULE", 0, [], null, null, fallbackExecutionId, envelope.CorrelationId);
    }

    private async Task<List<NotificationRuleVersionRow>> QueryActiveRulesAsync(string eventType, DateTimeOffset now, CancellationToken ct)
    {
        var versions = await db.NotificationRuleVersions
            .Include(x => x.Rule)
            .Where(x => x.Rule != null
                && (x.EventType == eventType || x.EventType == "" && x.Rule.EventType == eventType)
                && x.Status == "PUBLISHED"
                && x.Enabled
                && x.EffectiveFrom <= now
                && (x.EffectiveTo == null || x.EffectiveTo > now))
            .OrderByDescending(x => x.EventType == "" ? x.Rule!.Priority : x.Priority)
            .ThenByDescending(x => x.VersionNo)
            .ToListAsync(ct);

        return versions
            .GroupBy(x => x.RuleId)
            .Select(g => g.OrderByDescending(x => x.VersionNo).First())
            .OrderByDescending(x => x.EventType == "" ? x.Rule!.Priority : x.Priority)
            .ThenByDescending(x => x.VersionNo)
            .ToList();
    }

    private async Task<NotificationProcessResult?> TryProcessRuleAsync(NotificationEventEnvelope envelope, NotificationRuleVersionRow version, long actor, DateTimeOffset now, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == envelope.TicketId, ct);
        if (ticket is null)
        {
            var missingContextExecutionId = await RecordExecutionAsync(new NotificationExecutionRow
            {
                TicketId = envelope.TicketId,
                EventId = envelope.EventId,
                RuleId = version.RuleId,
                RuleVersionId = version.Id,
                Status = "SKIPPED",
                EvaluatedAt = now,
                Matched = false,
                SkipReason = "MISSING_CONTEXT",
                CreatedCommunicationCount = 0,
                CorrelationId = envelope.CorrelationId,
                CreatedAt = now,
                CompletedAt = now,
                RuleCode = version.Rule?.Code,
                TemplateCode = version.TemplateCode,
                SelectedChannel = version.ChannelDefinition,
                DedupeKey = BuildDedupeKey(envelope, version),
                NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, envelope.TicketId, envelope.WorkItemId, envelope.Payload, reason = "MISSING_CONTEXT" })
            }, ct);
            return new(false, "SKIPPED", "MISSING_CONTEXT", 0, [], version.Rule?.Code, version.Id, missingContextExecutionId, envelope.CorrelationId);
        }

        if (version.Rule is null || !version.Rule.Enabled)
        {
            var disabledExecutionId = await RecordExecutionAsync(new NotificationExecutionRow
            {
                TicketId = envelope.TicketId,
                EventId = envelope.EventId,
                RuleId = version.RuleId,
                RuleVersionId = version.Id,
                Status = "SKIPPED",
                EvaluatedAt = now,
                Matched = false,
                SkipReason = "RULE_DISABLED",
                CreatedCommunicationCount = 0,
                CorrelationId = envelope.CorrelationId,
                CreatedAt = now,
                CompletedAt = now,
                RuleCode = version.Rule?.Code,
                TemplateCode = version.TemplateCode,
                SelectedChannel = version.ChannelDefinition,
                DedupeKey = BuildDedupeKey(envelope, version),
                NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, reason = "RULE_DISABLED" })
            }, ct);
            return new(false, "SKIPPED", "RULE_DISABLED", 0, [], version.Rule?.Code, version.Id, disabledExecutionId, envelope.CorrelationId);
        }

        var priority = ticket.PriorityId.HasValue
            ? await priorities.GetPriorityAsync(actor, ticket.PriorityId.Value, ct)
            : await priorities.ResolvePriorityForSelectionAsync(actor, null, ct);
        var conditionMatch = EvaluateCondition(version.Condition, ticket, envelope, priority.Code, priority.Id);
        if (!conditionMatch)
        {
            var conditionExecutionId = await RecordExecutionAsync(new NotificationExecutionRow
            {
                TicketId = envelope.TicketId,
                EventId = envelope.EventId,
                RuleId = version.RuleId,
                RuleVersionId = version.Id,
                Status = "SKIPPED",
                EvaluatedAt = now,
                Matched = false,
                SkipReason = "CONDITION_NOT_MATCHED",
                CreatedCommunicationCount = 0,
                CorrelationId = envelope.CorrelationId,
                CreatedAt = now,
                CompletedAt = now,
                RuleCode = version.Rule?.Code,
                TemplateCode = version.TemplateCode,
                SelectedChannel = version.ChannelDefinition,
                DedupeKey = BuildDedupeKey(envelope, version),
                NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, envelope.TicketId, ticket.StatusId, ticket.PriorityId, ticket.CurrentDepartmentId, ticket.CurrentTeamId, envelope.Payload, reason = "CONDITION_NOT_MATCHED" })
            }, ct);
            return new(false, "SKIPPED", "CONDITION_NOT_MATCHED", 0, [], version.Rule?.Code, version.Id, conditionExecutionId, envelope.CorrelationId);
        }

        var dedupeKey = BuildDedupeKey(envelope, version);
        var existingExecution = await db.NotificationExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == envelope.EventId && x.RuleVersionId == version.Id, ct);
        if (existingExecution is not null)
        {
            return new(false, "DUPLICATE", "DUPLICATE", existingExecution.CreatedCommunicationCount, [], existingExecution.RuleCode, existingExecution.RuleVersionId, existingExecution.Id, existingExecution.CorrelationId);
        }

        var channels = ResolveChannels(version.ChannelDefinition);
        var channelNames = channels.Select(channel => channel.ToString().ToUpperInvariant()).ToArray();
        var channelConfigurations = await db.CommunicationChannelConfigurations.AsNoTracking()
            .Where(x => channelNames.Contains(x.Channel))
            .ToDictionaryAsync(x => x.Channel, ct);
        channels = channels.Where(channel =>
        {
            if (!channelConfigurations.TryGetValue(channel.ToString().ToUpperInvariant(), out var config)) return true;
            return config.Enabled && config.EffectiveFrom <= now && (config.EffectiveTo is null || config.EffectiveTo > now);
        }).ToList();
        if (channels.Count == 0)
        {
            var channelExecutionId = await RecordExecutionAsync(new NotificationExecutionRow
            {
                TicketId = envelope.TicketId,
                EventId = envelope.EventId,
                RuleId = version.RuleId,
                RuleVersionId = version.Id,
                Status = "SKIPPED",
                EvaluatedAt = now,
                Matched = false,
                SkipReason = "CHANNEL_NOT_CONFIGURED",
                CreatedCommunicationCount = 0,
                CorrelationId = envelope.CorrelationId,
                CreatedAt = now,
                CompletedAt = now,
                RuleCode = version.Rule?.Code,
                TemplateCode = version.TemplateCode,
                SelectedChannel = version.ChannelDefinition,
                DedupeKey = dedupeKey,
                NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, reason = "CHANNEL_NOT_CONFIGURED" })
            }, ct);
            return new(false, "SKIPPED", "CHANNEL_NOT_CONFIGURED", 0, [], version.Rule?.Code, version.Id, channelExecutionId, envelope.CorrelationId);
        }

        var recipient = ResolveRecipient(ticket, version.RecipientDefinition, envelope, actor);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            var recipientExecutionId = await RecordExecutionAsync(new NotificationExecutionRow
            {
                TicketId = envelope.TicketId,
                EventId = envelope.EventId,
                RuleId = version.RuleId,
                RuleVersionId = version.Id,
                Status = "SKIPPED",
                EvaluatedAt = now,
                Matched = false,
                SkipReason = "NO_RECIPIENT",
                CreatedCommunicationCount = 0,
                CorrelationId = envelope.CorrelationId,
                CreatedAt = now,
                CompletedAt = now,
                RuleCode = version.Rule?.Code,
                TemplateCode = version.TemplateCode,
                SelectedChannel = version.ChannelDefinition,
                DedupeKey = dedupeKey,
                NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, reason = "NO_RECIPIENT" })
            }, ct);
            return new(false, "SKIPPED", "NO_RECIPIENT", 0, [], version.Rule?.Code, version.Id, recipientExecutionId, envelope.CorrelationId);
        }

        var created = new List<NotificationCommunicationRecord>();
        var createdRows = new List<CommunicationRow>();
        foreach (var channel in channels)
        {
            var communicationIdempotencyKey = $"notification:{envelope.EventId}:{version.RuleId}:{version.Id}:{channel}:{recipient}";
            var queued = await communicationService.QueueAsync(new QueueCommunicationCommand(
                envelope.TicketId,
                envelope.WorkItemId,
                channel,
                "deterministic",
                version.TemplateCode,
                recipient,
                null,
                null,
                null,
                null,
                envelope.CorrelationId,
                communicationIdempotencyKey,
                new Dictionary<string, string>
                {
                    ["TicketNumber"] = ticket.Number,
                    ["TicketTitle"] = ticket.Subject,
                    ["Status"] = NormalizeStatus(ticket.StatusId),
                    ["Priority"] = priority.Code,
                    ["EventType"] = envelope.EventType,
                    ["CorrelationId"] = envelope.CorrelationId ?? string.Empty
                }),
                actor,
                ct);

            var createdRow = await db.Communications.SingleOrDefaultAsync(x => x.IdempotencyKey == communicationIdempotencyKey, ct);
            if (createdRow is not null)
            {
                createdRow.NotificationRuleId = version.RuleId;
                createdRow.NotificationRuleVersionId = version.Id;
                createdRow.EventId = envelope.EventId;
                createdRows.Add(createdRow);
            }

            created.Add(new NotificationCommunicationRecord(
                queued.Id,
                queued.Channel.ToString(),
                queued.Recipient ?? string.Empty,
                version.TemplateCode ?? "DEFAULT",
                queued.Subject,
                queued.Body,
                queued.CorrelationId,
                queued.CreatedAt,
                queued.PublicId));
        }

        var executionId = await RecordExecutionAsync(new NotificationExecutionRow
        {
            TicketId = envelope.TicketId,
            EventId = envelope.EventId,
            RuleId = version.RuleId,
            RuleVersionId = version.Id,
            Status = "COMPLETED",
            EvaluatedAt = now,
            Matched = true,
            SkipReason = null,
            CreatedCommunicationCount = created.Count,
            CorrelationId = envelope.CorrelationId,
            CreatedAt = now,
            CompletedAt = now,
            RuleCode = version.Rule?.Code,
            TemplateCode = version.TemplateCode,
            SelectedChannel = version.ChannelDefinition,
            DedupeKey = dedupeKey,
            NotificationContext = JsonSerializer.Serialize(new { envelope.EventType, envelope.TicketId, envelope.WorkItemId, recipient, channels = channels.Select(x => x.ToString()).ToArray(), ruleVersion = version.VersionNo, reason = "MATCHED" })
        }, ct);

        foreach (var communication in createdRows)
            communication.NotificationExecutionId = executionId;

        await db.Events.AddAsync(new EventRow
        {
            TicketId = envelope.TicketId,
            WorkItemId = envelope.WorkItemId,
            Type = "NOTIFICATION_CREATED",
            Category = "NOTIFICATION",
            ActorId = actor,
            NewValue = executionId.ToString(),
            Reason = envelope.CorrelationId,
            At = now
        }, ct);
        await db.Events.AddAsync(new EventRow
        {
            TicketId = envelope.TicketId,
            WorkItemId = envelope.WorkItemId,
            Type = "NOTIFICATION_MATCHED",
            Category = "NOTIFICATION",
            ActorId = actor,
            NewValue = version.Rule?.Code ?? version.RuleId.ToString(),
            Reason = envelope.CorrelationId,
            At = now
        }, ct);
        await db.SaveChangesAsync(ct);

        return new(true, "COMPLETED", null, created.Count, created, version.Rule?.Code, version.Id, executionId, envelope.CorrelationId);
    }

    private static string? ResolveRecipient(TicketRow? ticket, string? recipientDefinition, NotificationEventEnvelope envelope, long actor)
    {
        if (string.IsNullOrWhiteSpace(recipientDefinition)) return null;

        return recipientDefinition.Trim() switch
        {
            "REQUESTER" => ticket is not null ? ticket.RequestedBy.ToString() : actor.ToString(),
            "ASSIGNEE" => ticket is not null && ticket.CurrentAssignee.HasValue ? ticket.CurrentAssignee.Value.ToString() : null,
            "TEAM" => ticket is not null && ticket.CurrentTeamId.HasValue ? ticket.CurrentTeamId.Value.ToString() : null,
            "DEPARTMENT" => ticket is not null && ticket.CurrentDepartmentId.HasValue ? ticket.CurrentDepartmentId.Value.ToString() : null,
            "EXPLICIT_RECIPIENT" => envelope.CorrelationId,
            _ => null
        };
    }

    private static List<CommunicationChannel> ResolveChannels(string? definition)
    {
        if (string.IsNullOrWhiteSpace(definition)) return [];
        var text = definition.Trim();
        if (text.StartsWith("[") || text.StartsWith("{"))
        {
            var parsed = JsonSerializer.Deserialize<string[]>(text);
            if (parsed is not null) return parsed.Select(ParseChannel).ToList();
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseChannel)
            .ToList();
    }

    private static CommunicationChannel ParseChannel(string value) =>
        Enum.TryParse<CommunicationChannel>(value, true, out var channel)
            ? channel
            : throw new InvalidOperationException($"Unsupported notification channel: {value}");

    private static bool EvaluateCondition(string? condition, TicketRow ticket, NotificationEventEnvelope envelope, string priorityCode, long priorityId)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;

        try
        {
            using var document = JsonDocument.Parse(condition);
            var context = BuildConditionContext(ticket, envelope, priorityCode, priorityId);
            return EvaluateConditionNode(document.RootElement, context);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Dictionary<string, string?> BuildConditionContext(TicketRow ticket, NotificationEventEnvelope envelope, string priorityCode, long priorityId)
    {
        var status = NormalizeStatus(ticket.StatusId);

        return new Dictionary<string, string?>
        {
            ["TicketId"] = ticket.Id.ToString(),
            ["EventType"] = envelope.EventType,
            ["Status"] = status,
            ["Priority"] = priorityCode,
            ["CurrentDepartmentId"] = ticket.CurrentDepartmentId?.ToString(),
            ["CurrentTeamId"] = ticket.CurrentTeamId?.ToString(),
            ["CurrentAssignee"] = ticket.CurrentAssignee?.ToString(),
            ["RequestedBy"] = ticket.RequestedBy.ToString(),
            ["CorrelationId"] = envelope.CorrelationId,
            ["OPEN"] = status,
            ["LOW"] = priorityCode,
            ["TicketStatus"] = status,
            ["TicketPriority"] = priorityCode,
            ["StatusId"] = ticket.StatusId.ToString(),
            ["PriorityId"] = priorityId.ToString()
        };
    }

    private static string NormalizeStatus(int statusId)
    {
        return statusId switch
        {
            1 or 10 => "OPEN",
            2 => "CLASSIFIED",
            3 => "ROUTED",
            4 => "IN_PROGRESS",
            5 => "WAITING",
            6 => "PENDING_APPROVAL",
            7 => "RESOLVED",
            8 => "CLOSED",
            9 => "CANCELLED",
            _ => statusId.ToString()
        };
    }

    private static bool EvaluateConditionNode(JsonElement element, Dictionary<string, string?> context)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.EnumerateObject().Count() == 1
                && element.TryGetProperty("all", out var all)
                && all.ValueKind == JsonValueKind.Array)
            {
                if (all.GetArrayLength() == 0) return false;
                foreach (var child in all.EnumerateArray())
                {
                    if (!EvaluateConditionNode(child, context)) return false;
                }
                return true;
            }

            if (element.EnumerateObject().Count() == 1
                && element.TryGetProperty("any", out var any)
                && any.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in any.EnumerateArray())
                {
                    if (EvaluateConditionNode(child, context)) return true;
                }
                return false;
            }

            if (element.EnumerateObject().Count() == 3
                && element.TryGetProperty("field", out var field)
                && element.TryGetProperty("op", out var op)
                && element.TryGetProperty("value", out var value)
                && field.ValueKind == JsonValueKind.String
                && op.ValueKind == JsonValueKind.String
                && value.ValueKind == JsonValueKind.String)
            {
                if (!context.TryGetValue(field.GetString() ?? string.Empty, out var actual)) return false;
                var target = value.GetString();
                var normalizedActual = actual ?? string.Empty;

                return op.GetString() switch
                {
                    "Equals" => string.Equals(normalizedActual, target, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(normalizedActual, target?.Trim('"'), StringComparison.OrdinalIgnoreCase),
                    "NotEquals" => !string.Equals(normalizedActual, target, StringComparison.OrdinalIgnoreCase),
                    "Present" => !string.IsNullOrWhiteSpace(normalizedActual),
                    _ => false
                };
            }
        }

        return false;
    }

    private static string BuildDedupeKey(NotificationEventEnvelope envelope, NotificationRuleVersionRow version)
        => $"notification:{envelope.EventId}:{version.RuleId}:{version.Id}:{version.DeduplicationScope ?? "PER_EVENT"}";

    private async Task<long> RecordExecutionAsync(NotificationExecutionRow execution, CancellationToken ct)
    {
        db.NotificationExecutions.Add(execution);
        await db.SaveChangesAsync(ct);
        return execution.Id;
    }
}
