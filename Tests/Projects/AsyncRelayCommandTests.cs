using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Utilities;

namespace PiAgentGui.Tests.Projects;

[TestClass]
public sealed class AsyncRelayCommandTests
{
    [TestMethod]
    public async Task RepeatedClickDoesNotRepeatAnInFlightOperation()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var command = new AsyncRelayCommand(_ => { calls++; return completion.Task; }, _ => Assert.Fail("Unexpected error"));
        var running = command.ExecuteAsync();
        Assert.IsFalse(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.AreEqual(1, calls);
        completion.SetResult();
        await running;
        Assert.IsTrue(command.CanExecute(null));
    }

    [TestMethod]
    public async Task FailureIsReportedAndCommandCanBeRetried()
    {
        Exception? reported = null;
        var error = new IOException("Test failure");
        var command = new AsyncRelayCommand(_ => Task.FromException(error), exception => reported = exception);
        await command.ExecuteAsync();
        Assert.AreSame(error, reported);
        Assert.IsTrue(command.CanExecute(null));
    }
}
