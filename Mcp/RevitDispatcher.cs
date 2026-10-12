using Autodesk.Revit.UI;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace TNov.Mcp
{
    /// <summary>
    /// Выполнение кода на главном потоке Revit для запросов MCP.
    /// HTTP-запросы приходят на фоновых потоках, а Revit API доступен только из своего контекста,
    /// поэтому работа ставится в очередь и выполняется в ExternalEvent.
    /// </summary>
    internal static class RevitDispatcher
    {
        private class Job
        {
            public Func<UIApplication, object> Work;
            public TaskCompletionSource<object> Tcs;
        }

        private class Handler : IExternalEventHandler
        {
            public void Execute(UIApplication app)
            {
                while (_queue.TryDequeue(out Job job))
                {
                    // Клиент уже получил таймаут — не меняем модель задним числом.
                    if (job.Tcs.Task.IsCompleted) continue;
                    try { job.Tcs.TrySetResult(job.Work(app)); }
                    catch (Exception ex) { job.Tcs.TrySetException(ex); }
                }
            }

            public string GetName() => "TNov MCP";
        }

        private static readonly ConcurrentQueue<Job> _queue = new ConcurrentQueue<Job>();
        private static ExternalEvent _event;

        /// <summary>Создаёт ExternalEvent. Вызывать из контекста Revit API (OnStartup).</summary>
        public static void Initialize()
        {
            if (_event == null) _event = ExternalEvent.Create(new Handler());
        }

        public static object Invoke(Func<UIApplication, object> work, TimeSpan timeout)
        {
            if (_event == null) throw new InvalidOperationException("Коннектор не инициализирован.");

            var job = new Job
            {
                Work = work,
                Tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            _queue.Enqueue(job);
            _event.Raise();

            bool done;
            try { done = job.Tcs.Task.Wait(timeout); }
            catch (AggregateException) { done = true; } // исключение работы — отдадим его ниже как есть
            if (!done)
            {
                job.Tcs.TrySetCanceled();
                throw new TimeoutException(
                    $"Revit не ответил за {timeout.TotalSeconds:0} с. Вероятно, открыт диалог или выполняется команда — " +
                    "попросите пользователя закрыть его и повторите.");
            }
            return job.Tcs.Task.GetAwaiter().GetResult();
        }
    }
}
