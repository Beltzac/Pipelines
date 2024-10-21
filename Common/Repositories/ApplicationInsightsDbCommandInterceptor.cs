using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Diagnostics;

namespace Common.Repositories
{
    public class ApplicationInsightsDbCommandInterceptor : DbCommandInterceptor
    {
        private readonly TelemetryClient _telemetryClient;

        public ApplicationInsightsDbCommandInterceptor(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        // Override ReaderExecuting
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            return ExecuteWithTelemetry(
                command,
                eventData,
                result,
                base.ReaderExecuting);
        }

        // Override ScalarExecuting
        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            return ExecuteWithTelemetry(
                command,
                eventData,
                result,
                base.ScalarExecuting);
        }

        // Override NonQueryExecuting
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            return ExecuteWithTelemetry(
                command,
                eventData,
                result,
                base.NonQueryExecuting);
        }

        // Override ReaderExecutingAsync
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithTelemetryAsync(
                command,
                eventData,
                result,
                base.ReaderExecutingAsync,
                cancellationToken);
        }

        // Override ScalarExecutingAsync
        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithTelemetryAsync(
                command,
                eventData,
                result,
                base.ScalarExecutingAsync,
                cancellationToken);
        }

        // Override NonQueryExecutingAsync
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithTelemetryAsync(
                command,
                eventData,
                result,
                base.NonQueryExecutingAsync,
                cancellationToken);
        }

        // Helper method for synchronous execution
        private InterceptionResult<T> ExecuteWithTelemetry<T>(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<T> result,
            Func<DbCommand, CommandEventData, InterceptionResult<T>, InterceptionResult<T>> baseMethod)
        {
            var telemetry = CreateTelemetry(command);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var returnValue = baseMethod(command, eventData, result);
                telemetry.Success = true;
                return returnValue;
            }
            catch (Exception ex)
            {
                telemetry.Success = false;
                telemetry.Properties.Add("Exception", ex.ToString());
                throw;
            }
            finally
            {
                stopwatch.Stop();
                telemetry.Duration = stopwatch.Elapsed;
                _telemetryClient.TrackDependency(telemetry);
            }
        }

        // Helper method for asynchronous execution
        private async ValueTask<InterceptionResult<T>> ExecuteWithTelemetryAsync<T>(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<T> result,
            Func<DbCommand, CommandEventData, InterceptionResult<T>, CancellationToken, ValueTask<InterceptionResult<T>>> baseMethodAsync,
            CancellationToken cancellationToken)
        {
            var telemetry = CreateTelemetry(command);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var returnValue = await baseMethodAsync(command, eventData, result, cancellationToken);
                telemetry.Success = true;
                return returnValue;
            }
            catch (Exception ex)
            {
                telemetry.Success = false;
                telemetry.Properties.Add("Exception", ex.ToString());
                throw;
            }
            finally
            {
                stopwatch.Stop();
                telemetry.Duration = stopwatch.Elapsed;
                _telemetryClient.TrackDependency(telemetry);
            }
        }

        // Helper method to create telemetry
        private DependencyTelemetry CreateTelemetry(DbCommand command)
        {
            return new DependencyTelemetry
            {
                Name = command.CommandText,
                Target = command.Connection.DataSource,
                Type = command.Connection.GetType().Name,
                Data = command.CommandText,
                Timestamp = DateTimeOffset.Now
            };
        }
    }
}
