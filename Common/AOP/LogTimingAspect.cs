using AspectInjector.Broker;
using System.Diagnostics;

namespace Common.AOP
{
    [Aspect(Scope.Global)]
    [Injection(typeof(LogTimingAspect))]
    public class LogTimingAspect : Attribute
    {
        [Advice(Kind.Around, Targets = Target.Method)]
        public object HandleMethod(
            [Argument(Source.Name)] string methodName,
            [Argument(Source.Target)] Func<object[], object> targetMethod,
            [Argument(Source.Arguments)] object[] arguments)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = targetMethod(arguments);

                if (result is Task taskResult)
                {
                    return ProcessAsync(taskResult, methodName, stopwatch);
                }
                else
                {
                    stopwatch.Stop();
                    Console.WriteLine($"[AspectInjector] {methodName} completed in {stopwatch.ElapsedMilliseconds} ms");
                    return result;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"[AspectInjector] {methodName} threw an exception after {stopwatch.ElapsedMilliseconds} ms: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessAsync(Task task, string methodName, Stopwatch stopwatch)
        {
            try
            {
                await task;
                stopwatch.Stop();
                Console.WriteLine($"[AspectInjector] {methodName} completed in {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"[AspectInjector] {methodName} threw an exception after {stopwatch.ElapsedMilliseconds} ms: {ex.Message}");
                throw;
            }
        }
    }
}
