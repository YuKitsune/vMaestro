using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;
using Shouldly;

namespace Maestro.Contracts.Tests;

public static class SnapshotTestHelper
{
    public static readonly DateTimeOffset FixedTime = new(2024, 6, 15, 12, 30, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static readonly MessagePackSerializerOptions MessagePackOptions =
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

    static readonly string SnapshotsDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "Snapshots");

    public static void VerifyJsonSnapshot<T>(T original, string snapshotName)
    {
        var snapshotPath = Path.Combine(SnapshotsDirectory, snapshotName);
        var serialized = JsonSerializer.Serialize(original, JsonOptions);

        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(SnapshotsDirectory);
            File.WriteAllText(snapshotPath, serialized);
            Assert.Fail($"Snapshot '{snapshotName}' did not exist and was created. Re-run the test.");
        }

        var snapshot = File.ReadAllText(snapshotPath);

        // Verify serialization matches snapshot
        serialized.ShouldBe(snapshot, $"Serialized output does not match snapshot '{snapshotName}'");

        // Verify deserialization from snapshot produces equivalent object
        var deserialized = JsonSerializer.Deserialize<T>(snapshot, JsonOptions);
        deserialized.ShouldNotBe(default);
        deserialized.ShouldBeEquivalentTo(original);
    }

    public static void VerifyMessagePackSnapshot<T>(T original, string snapshotName)
    {
        var snapshotPath = Path.Combine(SnapshotsDirectory, snapshotName);
        var serialized = MessagePackSerializer.Serialize(original, MessagePackOptions);

        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(SnapshotsDirectory);
            File.WriteAllBytes(snapshotPath, serialized);
            Assert.Fail($"Snapshot '{snapshotName}' did not exist and was created. Re-run the test.");
        }

        var snapshot = File.ReadAllBytes(snapshotPath);

        // Verify serialization matches snapshot
        serialized.ShouldBe(snapshot, $"Serialized output does not match snapshot '{snapshotName}'");

        // Verify deserialization from snapshot produces equivalent object
        var deserialized = MessagePackSerializer.Deserialize<T>(snapshot, MessagePackOptions);
        deserialized.ShouldNotBe(default);
        deserialized.ShouldBeEquivalentTo(original);
    }
}
