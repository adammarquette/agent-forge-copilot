using FluentAssertions;

namespace MarqSpec.AgentForge.IntegrationTests.Support;

/// <summary>Waits for a real, network-backed task with a generous bounded timeout instead of hanging forever on a stalled test.</summary>
public static class TaskWaitHelper
{
    public static async Task<T> WaitForAsync<T>(Task<T> task, TimeSpan? timeout = null)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout ?? TimeSpan.FromSeconds(60)));
        completed.Should().Be(task, "the real host should have responded within the timeout");
        return await task;
    }
}
