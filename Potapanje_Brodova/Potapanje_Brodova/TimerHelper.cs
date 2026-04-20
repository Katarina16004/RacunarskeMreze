using System;
using System.Threading.Tasks;

namespace Server
{
    public class TimerHelper
    {
        public static async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> operation, int timeoutSeconds)
        {
            try
            {
                var task = operation();
                var completedTask = await Task.WhenAny(task, Task.Delay(timeoutSeconds * 1000));

                if (completedTask == task)
                {
                    return await task;
                }
                else
                {
                    throw new TimeoutException($"Operacija je prekoračila vremenski limit od {timeoutSeconds} sekundi");
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
        }
    }
}