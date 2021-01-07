using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around background execution functionality that cannot be easily tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class BackgroundExecutionWrapper
    {
        public virtual void Execute(Action action)
        {
            Task.Run(action);
        }

        public virtual async Task ExecuteDelayed(Action action, int delayMsec)
        {
            await Task.Delay(delayMsec);
            action();
        }
    }
}
