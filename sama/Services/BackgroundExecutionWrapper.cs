using System;
using System.Threading.Tasks;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around background execution functionality that cannot be easily tested.
    /// </summary>
    public class BackgroundExecutionWrapper
    {
        public virtual void Execute(Action action)
        {
            Task.Run(action);
        }
    }
}
