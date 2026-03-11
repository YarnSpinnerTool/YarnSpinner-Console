#nullable enable

namespace YarnSpinnerConsole
{
    using System;
    using System.Diagnostics;

    public class TimeTracker
    {
        public TimeTracker(System.Action<string, double, double> onLog)
        {
            this.OnLog = onLog;
            this.Stopwatch = new System.Diagnostics.Stopwatch();
            this.TotalStopwatch = new System.Diagnostics.Stopwatch();
        }

        private string? CurrentPhase { get; set; }

        public void StartPhase(string phase)
        {
            if (CurrentPhase != null)
            {
#if DEBUG
                OnLog(CurrentPhase, Stopwatch.Elapsed.TotalSeconds, TotalStopwatch.Elapsed.TotalSeconds);
#endif
            }
            CurrentPhase = phase;
            Stopwatch.Restart();
            TotalStopwatch.Start();
        }

        public void Stop()
        {
            if (CurrentPhase != null)
            {
#if DEBUG
                OnLog(CurrentPhase, Stopwatch.Elapsed.TotalSeconds, TotalStopwatch.Elapsed.TotalSeconds);
#endif
            }
            CurrentPhase = null;
            Stopwatch.Stop();
            TotalStopwatch.Stop();
        }


        public Action<string, double, double> OnLog { get; }
        public Stopwatch Stopwatch { get; }
        public Stopwatch TotalStopwatch { get; }
    }
}
