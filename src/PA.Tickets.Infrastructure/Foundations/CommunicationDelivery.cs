using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using PA.Tickets.Application.Foundations;
using PA.Tickets.Domain.Communications;

namespace PA.Tickets.Infrastructure.Foundations;

public sealed class ExplicitRecipientResolver : IRecipientResolver
{
    public Task<string> ResolveAsync(QueueCommunicationCommand command,long actor,CancellationToken ct)
    {
        if (actor <= 0 || string.IsNullOrWhiteSpace(command.Recipient)) throw new ArgumentException("A recipient is required.");
        if (command.Recipient.Length > 2000 || command.Recipient.Contains('\0')) throw new ArgumentException("The recipient is invalid.");
        return Task.FromResult(command.Recipient.Trim());
    }
}

public sealed class CommunicationProviderResolver(IEnumerable<ICommunicationProvider> providers,bool production=false) : ICommunicationProviderResolver
{
    private readonly IReadOnlyCollection<ICommunicationProvider> _providers=providers.ToArray();

    public void ValidateConfiguration()
    {
        if(production&&!_providers.Any(x=>x.IsProductionReady))
            throw new InvalidOperationException("Communication delivery is disabled: no production-ready provider is configured.");
    }

    public ICommunicationProvider Resolve(string? providerCode,CommunicationChannel channel)
    {
        var code=string.IsNullOrWhiteSpace(providerCode)?"deterministic":providerCode;
        var selected=_providers.FirstOrDefault(x=>string.Equals(x.Code,code,StringComparison.OrdinalIgnoreCase));
        if (selected is null && (code.Equals("transient",StringComparison.OrdinalIgnoreCase)||code.Equals("permanent",StringComparison.OrdinalIgnoreCase)||code.Equals("delivered",StringComparison.OrdinalIgnoreCase)||code.Equals("lost-response",StringComparison.OrdinalIgnoreCase))) selected=_providers.FirstOrDefault(x=>x.Code=="deterministic");
        if(selected is null)throw new InvalidOperationException($"No communication provider is configured for {channel}.");
        if(production&&!selected.IsProductionReady)throw new InvalidOperationException("Communication delivery is disabled: the selected provider is test-only.");
        return selected;
    }
}

public sealed class DeterministicCommunicationProvider : ICommunicationProvider
{
    public string Code => "deterministic";
    public bool IsProductionReady => false;
    public Task<CommunicationDeliveryResult> SendAsync(ProviderSendRequest request,CancellationToken ct)
    {
        var mode=request.Message.ProviderCode?.Trim().ToLowerInvariant();
        return Task.FromResult(mode switch
        {
            "transient" => new CommunicationDeliveryResult(false,null,"Temporary provider failure","TEMPORARY_FAILURE",CommunicationFailureClassification.Transient),
            "permanent" => new CommunicationDeliveryResult(false,null,"Recipient rejected","INVALID_RECIPIENT",CommunicationFailureClassification.Permanent),
            "delivered" => new CommunicationDeliveryResult(true,"det-"+request.Message.PublicId,"Provider confirmed delivery",null,null,true),
            "lost-response" => new CommunicationDeliveryResult(true,"det-"+request.Message.PublicId,"Provider accepted; response was lost",null,null,false),
            _ => new CommunicationDeliveryResult(true,"det-"+request.Message.PublicId,null,null,null,false)
        });
    }
}

public sealed class CommunicationTemplateRenderer
{
    private static readonly Regex Token = new("\\{\\{([A-Za-z][A-Za-z0-9_]*)\\}\\}",RegexOptions.Compiled|RegexOptions.CultureInvariant);
    public string Render(string template,IReadOnlyDictionary<string,string>? values)
    {
        return Token.Replace(template,m=>values is not null&&values.TryGetValue(m.Groups[1].Value,out var value)?value:throw new ArgumentException($"Template variable '{m.Groups[1].Value}' was not supplied."));
    }
}
