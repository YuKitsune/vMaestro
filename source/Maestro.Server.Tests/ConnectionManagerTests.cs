using Maestro.Contracts.Connectivity;
using Shouldly;

namespace Maestro.Server.Tests;

public class ConnectionManagerTests
{
    const string Version = "0.0.0";
    const string Environment = "VATSIM";
    const string Airport = "YSSY";

    Connection MakeConnection(string id, Role role = Role.Enroute, string? airport = null)
        => new(id, Version, Environment, airport ?? Airport, $"{id}_callsign", role);

    ConnectionManager Manager() => new();

    // --- Add ---

    [Fact]
    public void Add_ReturnsConnection_WithCorrectProperties()
    {
        var mgr = Manager();
        var conn = mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);

        conn.Id.ShouldBe("c1");
        conn.Callsign.ShouldBe("SY_APP");
        conn.Role.ShouldBe(Role.Approach);
        conn.IsMaster.ShouldBeFalse();
    }

    [Fact]
    public void Add_DuplicateConnectionId_Throws()
    {
        var mgr = Manager();
        mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);

        Should.Throw<InvalidOperationException>(() =>
            mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach));
    }

    // --- TryGetConnection ---

    [Fact]
    public void TryGetConnection_ExistingId_ReturnsTrueAndConnection()
    {
        var mgr = Manager();
        mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);

        var found = mgr.TryGetConnection("c1", out var conn);

        found.ShouldBeTrue();
        conn.ShouldNotBeNull();
        conn.Id.ShouldBe("c1");
    }

    [Fact]
    public void TryGetConnection_UnknownId_ReturnsFalse()
    {
        var mgr = Manager();

        var found = mgr.TryGetConnection("unknown", out var conn);

        found.ShouldBeFalse();
        conn.ShouldBeNull();
    }

    // --- Remove ---

    [Fact]
    public void Remove_ExistingConnection_IsNoLongerRetrievable()
    {
        var mgr = Manager();
        var conn = mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);

        mgr.Remove(conn);

        mgr.TryGetConnection("c1", out _).ShouldBeFalse();
    }

    // --- GetPeers ---

    [Fact]
    public void GetPeers_ReturnsSameAirportConnections_ExcludingSelf()
    {
        var mgr = Manager();
        var c1 = mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);
        var c2 = mgr.Add("c2", Version, Environment, Airport, "SY_FMP", Role.Flow);
        mgr.Add("c3", Version, Environment, "YMML", "ML_APP", Role.Approach); // different airport

        var peers = mgr.GetPeers(c1);

        peers.ShouldContain(c2);
        peers.ShouldNotContain(c1);
        peers.Length.ShouldBe(1);
    }

    [Fact]
    public void GetPeers_AfterRemove_ExcludesRemovedConnection()
    {
        var mgr = Manager();
        var c1 = mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);
        var c2 = mgr.Add("c2", Version, Environment, Airport, "SY_CTR", Role.Enroute);

        mgr.Remove(c2);
        var peers = mgr.GetPeers(c1);

        peers.ShouldBeEmpty();
    }

    // --- GetConnections ---

    [Fact]
    public void GetConnections_ReturnsAllForAirport_IncludingCaller()
    {
        var mgr = Manager();
        var c1 = mgr.Add("c1", Version, Environment, Airport, "SY_APP", Role.Approach);
        var c2 = mgr.Add("c2", Version, Environment, Airport, "SY_FMP", Role.Flow);
        mgr.Add("c3", Version, Environment, "YMML", "ML_APP", Role.Approach);

        var connections = mgr.GetConnections(Environment, Airport);

        connections.ShouldContain(c1);
        connections.ShouldContain(c2);
        connections.Length.ShouldBe(2);
    }

    // --- PromoteMaster ---

    [Fact]
    public void PromoteMaster_SetsMasterFlag_OnNewMaster()
    {
        var mgr = Manager();
        var conn = mgr.Add("c1", Version, Environment, Airport, "SY_FMP", Role.Flow);

        mgr.PromoteMaster(conn);

        conn.IsMaster.ShouldBeTrue();
    }

    [Fact]
    public void PromoteMaster_DemotesPreviousMaster_SameAirport()
    {
        var mgr = Manager();
        var c1 = mgr.Add("c1", Version, Environment, Airport, "ML-BIK_CTR", Role.Enroute);
        var c2 = mgr.Add("c2", Version, Environment, Airport, "SY_FMP", Role.Flow);

        mgr.PromoteMaster(c1);
        var previous = mgr.PromoteMaster(c2);

        c1.IsMaster.ShouldBeFalse();
        c2.IsMaster.ShouldBeTrue();
        previous.ShouldBe(c1);
    }

    [Fact]
    public void PromoteMaster_ReturnsNull_WhenNoPreviousMaster()
    {
        var mgr = Manager();
        var conn = mgr.Add("c1", Version, Environment, Airport, "SY_FMP", Role.Flow);

        var previous = mgr.PromoteMaster(conn);

        previous.ShouldBeNull();
    }

    [Fact]
    public void PromoteMaster_DoesNotDemoteMaster_ForDifferentAirport()
    {
        var mgr = Manager();
        var syssy = mgr.Add("c1", Version, Environment, Airport, "SY_FMP", Role.Flow);
        var ymml = mgr.Add("c2", Version, Environment, "YMML", "ML_FMP", Role.Flow);

        mgr.PromoteMaster(syssy);
        mgr.PromoteMaster(ymml);

        syssy.IsMaster.ShouldBeTrue();
        ymml.IsMaster.ShouldBeTrue();
    }

    [Fact]
    public void PromoteMaster_CalledTwiceWithSameConnection_RemainsMaster()
    {
        var mgr = Manager();
        var conn = mgr.Add("c1", Version, Environment, Airport, "SY_FMP", Role.Flow);

        mgr.PromoteMaster(conn);
        mgr.PromoteMaster(conn);

        conn.IsMaster.ShouldBeTrue();
    }
}
