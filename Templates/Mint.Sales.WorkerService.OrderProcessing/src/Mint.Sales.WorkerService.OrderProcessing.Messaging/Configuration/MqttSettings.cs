using System.ComponentModel.DataAnnotations;

namespace Mint.Sales.WorkerService.OrderProcessing.Messaging.Configuration;

/// <summary>Décrit et valide la section de configuration nécessaire au transport MQTT.</summary>
public sealed class MqttSettings
{
    /// <summary>Nom canonique de la section de configuration.</summary>
    public const string SectionName = "Mqtt";
    /// <summary>Obtient le nom DNS ou l’adresse du broker.</summary>
    [Required] public string Host { get; init; } = string.Empty;
    /// <summary>Obtient le port TCP du broker.</summary>
    [Range(1, 65535)] public int Port { get; init; } = 1883;
    /// <summary>Obtient l’identité MQTT unique de cette instance.</summary>
    [Required] public string ClientId { get; init; } = string.Empty;
    /// <summary>Obtient la racine gouvernée commune à tous les topics du service.</summary>
    [Required] public string BaseTopic { get; init; } = string.Empty;
    /// <summary>Obtient l’identité du module utilisée dans la hiérarchie des topics.</summary>
    [Required] public string ModuleIdentity { get; init; } = string.Empty;
    /// <summary>Obtient l’identité du service utilisée dans la hiérarchie des topics.</summary>
    [Required] public string ServiceIdentity { get; init; } = string.Empty;
    /// <summary>Obtient l’URI absolue placée dans l’attribut <c>source</c> des CloudEvents.</summary>
    [Required] public string CloudEventSource { get; init; } = string.Empty;
    /// <summary>Indique si le transport local sans broker doit être utilisé pour le développement ou les tests.</summary>
    public bool UseInMemoryTransport { get; init; }
}
