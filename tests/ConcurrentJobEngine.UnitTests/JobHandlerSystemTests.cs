using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using ConcurrentJobEngine.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Contains unit tests for the generic strongly-typed job handler system.
/// </summary>
public class JobHandlerSystemTests
{
    private sealed record TestJob(string Name) : IJob;

    private sealed class TestJobHandler : IJobHandler<TestJob>
    {
        public Task<JobResult> HandleAsync(TestJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (job.Name == "throw")
            {
                throw new InvalidOperationException("Handler failure simulation");
            }
            return Task.FromResult(JobResult.Success());
        }
    }

    [Fact]
    public void AddJobHandler_RegistersHandlerSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestJob, TestJobHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var handler = serviceProvider.GetService<IJobHandler<TestJob>>();
        Assert.NotNull(handler);
        Assert.IsType<TestJobHandler>(handler);
    }

    [Fact]
    public async Task JobHandlerResolver_ResolvesAndExecutesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestJob, TestJobHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var job = new TestJob("hello");
        var context = new JobExecutionContext(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, JobPriority.Normal);

        var wrapper = JobHandlerResolver.GetWrapper(job.GetType());
        Assert.NotNull(wrapper);

        var result = await wrapper.HandleAsync(job, context, serviceProvider, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task JobHandlerResolver_ReturnsHandlerNotFound_WhenHandlerMissing()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var job = new TestJob("hello");
        var context = new JobExecutionContext(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, JobPriority.Normal);

        var wrapper = JobHandlerResolver.GetWrapper(job.GetType());
        Assert.NotNull(wrapper);

        var result = await wrapper.HandleAsync(job, context, serviceProvider, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.HandlerNotFound, result.FailureReason);
    }

    [Fact]
    public async Task JobHandlerResolver_ReturnsExecutionFailed_WhenHandlerThrows()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestJob, TestJobHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var job = new TestJob("throw");
        var context = new JobExecutionContext(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, JobPriority.Normal);

        var wrapper = JobHandlerResolver.GetWrapper(job.GetType());
        Assert.NotNull(wrapper);

        var result = await wrapper.HandleAsync(job, context, serviceProvider, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.ExecutionFailed, result.FailureReason);
        Assert.Contains("Handler failure simulation", result.Message);
    }
}
