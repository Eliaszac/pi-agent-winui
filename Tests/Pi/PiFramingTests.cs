using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Pi;

[TestClass]
public sealed class PiFramingTests
{
    [TestMethod]
    public async Task OnlyLfSeparatesRecordsAndTrailingCrIsRemoved()
    {
        using var reader = new StringReader("\n{\"text\":\"one\u2028two\u2029three\"}\r\n{}\n");
        var records = new List<string>();
        await foreach (var record in PiJsonLineReader.ReadAsync(reader)) records.Add(record);
        CollectionAssert.AreEqual(new[] { "{\"text\":\"one\u2028two\u2029three\"}", "{}" }, records);
    }

    [TestMethod]
    public async Task IncompleteFinalRecordIsNotDelivered()
    {
        using var reader = new StringReader("{}");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in PiJsonLineReader.ReadAsync(reader)) Assert.Fail("Partial record delivered.");
        });
    }

    [TestMethod]
    public async Task OversizedRecordFailsAtConfiguredLimit()
    {
        using var reader = new StringReader("12345\n");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in PiJsonLineReader.ReadAsync(reader, maxRecordLength: 4)) Assert.Fail("Oversized record delivered.");
        });
    }
}
