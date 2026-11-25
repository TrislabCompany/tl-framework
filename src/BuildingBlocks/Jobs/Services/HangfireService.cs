using Hangfire;
using System.Linq.Expressions;

namespace Trislab.Framework.Jobs.Services;

/// <summary>
/// <see cref="IJobService"/> on this host's own <see cref="IBackgroundJobClient"/>. Never Hangfire's static
/// <c>BackgroundJob</c>: it writes to the process-wide <c>JobStorage.Current</c>, which belongs to whichever host
/// configured Hangfire last.
/// </summary>
public class HangfireService(IBackgroundJobClient client) : IJobService
{
    public bool Delete(string jobId) =>
        client.Delete(jobId);

    public bool Delete(string jobId, string fromState) =>
        client.Delete(jobId, fromState);

    public string Enqueue(Expression<Func<Task>> methodCall) =>
        client.Enqueue(methodCall);

    public string Enqueue(string queue, Expression<Func<Task>> methodCall) =>
        client.Enqueue(queue, methodCall);

    public string Enqueue<T>(Expression<Action<T>> methodCall) =>
        client.Enqueue(methodCall);

    public string Enqueue(Expression<Action> methodCall) =>
        client.Enqueue(methodCall);

    public string Enqueue<T>(Expression<Func<T, Task>> methodCall) =>
        client.Enqueue(methodCall);

    public string Enqueue<T>(string queue, Expression<Func<T, Task>> methodCall) =>
        client.Enqueue(queue, methodCall);

    public bool Requeue(string jobId) =>
        client.Requeue(jobId);

    public bool Requeue(string jobId, string fromState) =>
        client.Requeue(jobId, fromState);

    public string Schedule(Expression<Action> methodCall, TimeSpan delay) =>
        client.Schedule(methodCall, delay);

    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay) =>
        client.Schedule(methodCall, delay);

    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt) =>
        client.Schedule(methodCall, enqueueAt);

    public string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt) =>
        client.Schedule(methodCall, enqueueAt);

    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) =>
        client.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) =>
        client.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt) =>
        client.Schedule(methodCall, enqueueAt);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt) =>
        client.Schedule(methodCall, enqueueAt);
}