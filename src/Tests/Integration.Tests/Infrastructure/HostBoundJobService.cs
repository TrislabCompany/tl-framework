using System.Linq.Expressions;
using Hangfire;
using Trislab.Framework.Jobs.Services;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// The test hosts' <see cref="IJobService"/>: it enqueues through this host's own <see cref="IBackgroundJobClient"/>
/// instead of Hangfire's static <c>BackgroundJob</c>. The static API writes to <c>JobStorage.Current</c>, which
/// points at whichever test host started last; with the multi-tenant and the single-tenant host both running, a job
/// enqueued by one host then ran on the other (and its e-mail reached the other host's mail capture, or nowhere).
/// Production has one host per process, where the two are the same.
/// </summary>
internal sealed class HostBoundJobService(IBackgroundJobClient client) : IJobService
{
    public bool Delete(string jobId) => client.Delete(jobId);

    public bool Delete(string jobId, string fromState) => client.Delete(jobId, fromState);

    public string Enqueue(Expression<Action> methodCall) => client.Enqueue(methodCall);

    public string Enqueue(string queue, Expression<Func<Task>> methodCall) => client.Enqueue(queue, methodCall);

    public string Enqueue(Expression<Func<Task>> methodCall) => client.Enqueue(methodCall);

    public string Enqueue<T>(Expression<Action<T>> methodCall) => client.Enqueue(methodCall);

    public string Enqueue<T>(Expression<Func<T, Task>> methodCall) => client.Enqueue(methodCall);

    public string Enqueue<T>(string queue, Expression<Func<T, Task>> methodCall) => client.Enqueue(queue, methodCall);

    public bool Requeue(string jobId) => client.Requeue(jobId);

    public bool Requeue(string jobId, string fromState) => client.Requeue(jobId, fromState);

    public string Schedule(Expression<Action> methodCall, TimeSpan delay) => client.Schedule(methodCall, delay);

    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay) => client.Schedule(methodCall, delay);

    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt) => client.Schedule(methodCall, enqueueAt);

    public string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt) => client.Schedule(methodCall, enqueueAt);

    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) => client.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) => client.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt) => client.Schedule(methodCall, enqueueAt);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt) => client.Schedule(methodCall, enqueueAt);
}
