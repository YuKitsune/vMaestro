namespace Maestro.Contracts.Connectivity;

// Note: JSON polymorphism attributes will be added when message types are moved to Contracts
public interface IRelayableRequest
{
    string AirportIdentifier { get; }
}
