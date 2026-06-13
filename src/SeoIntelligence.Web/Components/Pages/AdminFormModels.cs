namespace SeoIntelligence.Web.Components.Pages;

public sealed class AdminWorkspaceFormModel
{
    public string? Name { get; set; }

    public string? DefaultLocation { get; set; }

    public string? DefaultLanguage { get; set; }

    public string? RetentionSettingsJson { get; set; } = "{}";

    public string? NotificationDefaultsJson { get; set; } = "{}";
}

public sealed class AdminCredentialFormModel
{
    public string? Provider { get; set; } = "rakko_keyword";

    public string? KeyRef { get; set; }

    public string? SecretValue { get; set; }
}

public sealed class AdminRotateCredentialFormModel
{
    public string? NewKeyRef { get; set; }

    public string? NewSecretValue { get; set; }
}

public sealed class AdminChannelFormModel
{
    public string? Name { get; set; }

    public string? ChannelType { get; set; } = "discord";

    public string? ProjectId { get; set; }

    public string? WebhookSecretRef { get; set; }

    public string? EventTypes { get; set; } = "job_failed, credit_low";
}

public sealed class AdminConnectorFormModel
{
    public string? ConnectorType { get; set; } = "gsc";

    public string? Name { get; set; }

    public string? AuthRef { get; set; }

    public string? SettingsJson { get; set; } = "{}";

    public string? Status { get; set; } = "active";
}

public sealed class AdminAuditSearchFormModel
{
    public string? Q { get; set; }

    public string? Actor { get; set; } = "developer";

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public string? CorrelationId { get; set; }

    public string? From { get; set; }

    public string? To { get; set; }
}
