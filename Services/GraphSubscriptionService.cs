// Copyright (c) CitizenAgent Project
// Licensed under the MIT License.

using CitizenAgent.Setup.Cli.Exceptions;
using CitizenAgent.Setup.Cli.Models;
using Microsoft.Extensions.Logging;

namespace CitizenAgent.Setup.Cli.Services;

/// <summary>
/// Interface for Graph Subscription operations
/// </summary>
public interface IGraphSubscriptionService
{
    /// <summary>
    /// Creates a Graph subscription for webhook notifications
    /// </summary>
    Task<GraphSubscription> CreateSubscriptionAsync(
        string webhookUrl,
        string resource,
        string changeType,
        TimeSpan expirationOffset,
        string? clientState = null,
        CancellationToken ct = default);
}

/// <summary>
/// Service for Graph Subscription operations
/// </summary>
public class GraphSubscriptionService : IGraphSubscriptionService
{
    private readonly ILogger<GraphSubscriptionService> _logger;
    private readonly IGraphApiService _graphService;

    public GraphSubscriptionService(ILogger<GraphSubscriptionService> logger, IGraphApiService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    /// <inheritdoc />
    public async Task<GraphSubscription> CreateSubscriptionAsync(
        string webhookUrl,
        string resource,
        string changeType,
        TimeSpan expirationOffset,
        string? clientState = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating Graph subscription for webhook: {WebhookUrl}", webhookUrl);
        _logger.LogDebug("  Resource: {Resource}", resource);
        _logger.LogDebug("  Change Type: {ChangeType}", changeType);

        var expirationDateTime = DateTime.UtcNow.Add(expirationOffset).ToString("yyyy-MM-ddTHH:mm:ss.0000000Z");

        var payload = new
        {
            changeType,
            notificationUrl = webhookUrl,
            resource,
            expirationDateTime,
            clientState = clientState ?? "CitizenAgent-webhook-secret"
        };

        // Use v1.0 for subscriptions
        var originalVersion = _graphService.ApiVersion;
        _graphService.ApiVersion = "v1.0";

        try
        {
            var doc = await _graphService.PostAsync("/subscriptions", payload, ct);

            if (doc == null)
            {
                throw new GraphApiException("Failed to create Graph subscription", 500);
            }

            var subscription = new GraphSubscription
            {
                Id = doc.RootElement.GetProperty("id").GetString(),
                ChangeType = changeType,
                NotificationUrl = webhookUrl,
                Resource = resource,
                ExpirationDateTime = doc.RootElement.TryGetProperty("expirationDateTime", out var ed)
                    ? ed.GetDateTime()
                    : null,
                ClientState = clientState
            };

            _logger.LogInformation("Graph subscription created: {Id}", subscription.Id);
            _logger.LogInformation("  Expires: {Expiration}", subscription.ExpirationDateTime);

            return subscription;
        }
        finally
        {
            _graphService.ApiVersion = originalVersion;
        }
    }
}
