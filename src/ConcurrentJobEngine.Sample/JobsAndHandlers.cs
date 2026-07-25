using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConcurrentJobEngine.Sample;

// --- Sample Job Definitions ---

public sealed record EmailNotificationJob(string Recipient, string Subject) : IJob;

public sealed record ImageProcessingJob(string ImagePath, int TargetWidth) : IJob;

public sealed record FlakyPaymentJob(string TransactionId, decimal Amount) : IJob;

public sealed record UnrecoverableJob(string ResourceId) : IJob;

// --- Sample Job Handlers ---

public sealed class EmailNotificationJobHandler : IJobHandler<EmailNotificationJob>
{
    private readonly ILogger<EmailNotificationJobHandler> _logger;

    public EmailNotificationJobHandler(ILogger<EmailNotificationJobHandler> logger) => _logger = logger;

    public async Task<JobResult> HandleAsync(EmailNotificationJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EmailHandler] Sending email to {Recipient} (Subject: {Subject}). Attempt {Attempt}", job.Recipient, job.Subject, context.AttemptNumber);
        await Task.Delay(100, cancellationToken);
        return JobResult.Success();
    }
}

public sealed class ImageProcessingJobHandler : IJobHandler<ImageProcessingJob>
{
    private readonly ILogger<ImageProcessingJobHandler> _logger;

    public ImageProcessingJobHandler(ILogger<ImageProcessingJobHandler> logger) => _logger = logger;

    public async Task<JobResult> HandleAsync(ImageProcessingJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ImageHandler] Resizing {ImagePath} to width {Width}px. Attempt {Attempt}", job.ImagePath, job.TargetWidth, context.AttemptNumber);
        await Task.Delay(300, cancellationToken);
        return JobResult.Success();
    }
}

public sealed class FlakyPaymentJobHandler : IJobHandler<FlakyPaymentJob>
{
    private static int _attemptTracker = 0;
    private readonly ILogger<FlakyPaymentJobHandler> _logger;

    public FlakyPaymentJobHandler(ILogger<FlakyPaymentJobHandler> logger) => _logger = logger;

    public async Task<JobResult> HandleAsync(FlakyPaymentJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        int attempt = Interlocked.Increment(ref _attemptTracker);
        _logger.LogInformation("[PaymentHandler] Processing transaction {TxId} for ${Amount}. Attempt {Attempt}", job.TransactionId, job.Amount, context.AttemptNumber);

        await Task.Delay(150, cancellationToken);

        if (attempt < 3)
        {
            _logger.LogWarning("[PaymentHandler] Gateway timeout for transaction {TxId}. Failing for retry.", job.TransactionId);
            return JobResult.Failure(FailureReason.ExecutionFailed, "Payment gateway timeout.");
        }

        _logger.LogInformation("[PaymentHandler] Transaction {TxId} approved!", job.TransactionId);
        return JobResult.Success();
    }
}

public sealed class UnrecoverableJobHandler : IJobHandler<UnrecoverableJob>
{
    private readonly ILogger<UnrecoverableJobHandler> _logger;

    public UnrecoverableJobHandler(ILogger<UnrecoverableJobHandler> logger) => _logger = logger;

    public Task<JobResult> HandleAsync(UnrecoverableJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogError("[UnrecoverableHandler] Resource {ResourceId} permanently corrupted.", job.ResourceId);
        return Task.FromResult(JobResult.Failure(FailureReason.ExecutionFailed, $"Resource {job.ResourceId} not found or corrupted."));
    }
}
