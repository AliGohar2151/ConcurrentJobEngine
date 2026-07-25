using System;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConcurrentJobEngine.Sample;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("          ConcurrentJobEngine Sample Application Demonstration    ");
        Console.WriteLine("=================================================================");
        Console.WriteLine();

        // 1. Build ServiceProvider with ConcurrentJobEngine and Handlers
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        services.AddConcurrentJobEngine(options =>
        {
            options.WorkerCount = 4;
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });

        services.AddJobHandler<EmailNotificationJob, EmailNotificationJobHandler>();
        services.AddJobHandler<ImageProcessingJob, ImageProcessingJobHandler>();
        services.AddJobHandler<FlakyPaymentJob, FlakyPaymentJobHandler>();
        services.AddJobHandler<UnrecoverableJob, UnrecoverableJobHandler>();

        var provider = services.BuildServiceProvider();

        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        // 2. Start Worker Pool
        Console.WriteLine("--> Starting ConcurrentJobEngine Worker Pool (4 Workers)...");
        await workerPool.StartAsync();
        Console.WriteLine();

        // 3. Submit Jobs with Different Priorities & Policies
        Console.WriteLine("--> Submitting sample jobs with priorities & retry policies...");

        var emailId = await processor.SubmitAsync(
            new EmailNotificationJob("user@example.com", "Welcome to ConcurrentJobEngine!"),
            new JobOptions { Priority = JobPriority.High });

        var imageId = await processor.SubmitAsync(
            new ImageProcessingJob("assets/hero.png", 1920),
            new JobOptions { Priority = JobPriority.Normal });

        var paymentId = await processor.SubmitAsync(
            new FlakyPaymentJob("TX-998231", 149.99m),
            new JobOptions
            {
                Priority = JobPriority.Critical,
                Retry = new RetryOptions
                {
                    MaxAttempts = 3,
                    InitialDelay = TimeSpan.FromMilliseconds(200),
                    BackoffMultiplier = 2.0,
                    UseJitter = true
                }
            });

        var deadLetterJobId = await processor.SubmitAsync(
            new UnrecoverableJob("RES-404-BROKEN"),
            new JobOptions
            {
                Priority = JobPriority.Low,
                Retry = new RetryOptions { MaxAttempts = 2, InitialDelay = TimeSpan.FromMilliseconds(100) }
            });

        Console.WriteLine($"   Submitted EmailJob    [High Priority]: {emailId}");
        Console.WriteLine($"   Submitted ImageJob    [Normal Priority]: {imageId}");
        Console.WriteLine($"   Submitted PaymentJob  [Critical Priority + Retries]: {paymentId}");
        Console.WriteLine($"   Submitted CorruptJob  [Low Priority + DeadLetter]: {deadLetterJobId}");
        Console.WriteLine();

        // 4. Poll and Display Progress
        Console.WriteLine("--> Processing jobs in background...");
        await Task.Delay(2000);

        Console.WriteLine();
        Console.WriteLine("=================================================================");
        Console.WriteLine("                     Job Execution Summary                       ");
        Console.WriteLine("=================================================================");

        var jobsToQuery = new[]
        {
            ("Email Notification", emailId),
            ("Image Processing", imageId),
            ("Flaky Payment", paymentId),
            ("Unrecoverable Job", deadLetterJobId)
        };

        foreach (var (name, id) in jobsToQuery)
        {
            var info = await processor.GetStatusAsync(id);
            if (info != null)
            {
                Console.WriteLine($"  {name,-20} | Status: {info.Status,-10} | Attempts: {info.AttemptCount} | Reason: {info.FailureReason?.ToString() ?? "None"}");
            }
        }

        Console.WriteLine();

        // 5. Inspect Dead-Letter Store
        var deadLetterRecords = await processor.GetDeadLetterJobsAsync();
        Console.WriteLine($"--> Dead-Letter Store Entries: {deadLetterRecords.Count}");
        foreach (var record in deadLetterRecords)
        {
            Console.WriteLine($"   - JobId: {record.JobId} | Type: {record.JobType} | Reason: {record.FailureReason} | Attempts: {record.AttemptCount}");
        }

        Console.WriteLine();

        // 6. Perform Graceful Shutdown
        Console.WriteLine("--> Initiating Graceful Engine Shutdown...");
        await processor.StopAsync();
        Console.WriteLine("--> Engine Shutdown Complete. All background workers terminated.");
        Console.WriteLine("=================================================================");
    }
}
