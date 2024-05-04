using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ChaosLib;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using ILogger = ChaosLib.ILogger;
using Log = ChaosLib.Log;

/* This file is not included in ChaosDbg.Engine. To use this file, add Serilog as a dependency and then include this file manually in your client application */

namespace ChaosDbg.Logger
{
    class RetryHttpClientHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage result = null;

            for (var i = 0; i < 10; i++)
            {
                result = await base.SendAsync(request, cancellationToken);

                if (result.IsSuccessStatusCode)
                    return result;
            }

            Debug.Assert(false);
            throw new LoggingFailedException($"Failed to post log event to Seq. Last status result: {result.StatusCode}");
        }
    }

    public class SerilogLogger : ILogger
    {
        public static void Install()
        {
            /* How to fix auto refresh: by default when you click on an item, this causes the autorefresh to stop working. To can fix
             * this by hacking the Seq main.js file. Open C:\Program Files\Seq\wwwroot\main-<xxx>.js in a text editor and search for the function "interacted"
             *   g.control.interacted=function(){null!==g.eventSet&&"auto"===g.eventSet.state&&g.control.stop()}
            //Remove "&& g.control.stop()", save, and force refresh your browser */
            var rawSerilogLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .Enrich.With(new SerilogDefaultEnricher())
                .AuditTo.Seq("http://127.0.0.1:5341", messageHandler: new RetryHttpClientHandler()) //Use IP instead of localhost so there isn't a delay while Windows tries to resolve IPv6 first
                .CreateLogger();

            var serilogWrapper = new SerilogLogger(rawSerilogLogger);

            Log.SetBaseLogger(serilogWrapper, () => new SerilogThreadContextEnricher());
        }

        private Serilog.ILogger rawLogger;

        public SerilogLogger(Serilog.ILogger rawLogger)
        {
            this.rawLogger = rawLogger;
        }

        public ILogger ForContext(ChaosLib.ILogEventEnricher enricher)
        {
            return new SerilogLogger(rawLogger.ForContext((Serilog.Core.ILogEventEnricher) enricher));
        }

        public IDisposable WithCategories(params Type[] categories) =>
            LogContext.Push(new CategoryEnricher(categories));

        #region Verbose

        public void Verbose(string messageTemplate, params object[] propertyValues) =>
            rawLogger.Verbose(messageTemplate, propertyValues);

        public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues) =>
            rawLogger.Verbose(exception, messageTemplate, propertyValues);

        #endregion
        #region Debug

        public void Debug(string messageTemplate, params object[] propertyValues) =>
            rawLogger.Debug(messageTemplate, propertyValues);

        public void Debug(Exception exception, string messageTemplate, params object[] propertyValues) =>
            rawLogger.Debug(exception, messageTemplate, propertyValues);

        #endregion
        #region Information

        public void Information(string messageTemplate, params object[] propertyValues) =>
            rawLogger.Information(messageTemplate, propertyValues);

        public void Information(Exception exception, string messageTemplate, params object[] propertyValues) =>
            rawLogger.Information(exception, messageTemplate, propertyValues);

        #endregion
        #region Warning

        public void Warning(string messageTemplate, params object[] propertyValues) =>
            rawLogger.Warning(messageTemplate, propertyValues);

        public void Warning(Exception exception, string messageTemplate, params object[] propertyValues) =>
            rawLogger.Warning(exception, messageTemplate, propertyValues);

        #endregion
        #region Error

        public void Error(string messageTemplate, params object[] propertyValues) =>
            rawLogger.Error(messageTemplate, propertyValues);

        public void Error(Exception exception, string messageTemplate, params object[] propertyValues) =>
            rawLogger.Error(exception, messageTemplate, propertyValues);

        #endregion
        #region Fatal

        public void Fatal(string messageTemplate, params object[] propertyValues) =>
            rawLogger.Fatal(messageTemplate, propertyValues);

        public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues) =>
            rawLogger.Fatal(exception, messageTemplate, propertyValues);

        #endregion
    }

    class SerilogDefaultEnricher : Serilog.Core.ILogEventEnricher
    {
        private int processId;

        public SerilogDefaultEnricher()
        {
            this.processId = Kernel32.GetCurrentProcessId();
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ProcessId", processId));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ThreadId", Kernel32.GetCurrentThreadId()));

            var name = Thread.CurrentThread.Name;

            if (!string.IsNullOrEmpty(name))
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadName", name));
        }
    }

    class SerilogThreadContextEnricher : Serilog.Core.ILogEventEnricher, ChaosLib.ILogEventEnricher
    {
        private Dictionary<string, object> properties = new();

        public void UpdateProperty(string name, object value) => properties[name] = value;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var kv in properties)
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(kv.Key, kv.Value));
        }

        public void ClearProperties()
        {
            properties.Clear();
        }
    }

    class CategoryEnricher : Serilog.Core.ILogEventEnricher
    {
        private Type[] categories;

        public CategoryEnricher(Type[] categories)
        {
            this.categories = categories;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Category", categories.Select(c => c.Name).ToArray()));
        }
    }
}
