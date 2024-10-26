using System;
using System.Collections.Generic;

namespace drewCo.Work
{
    // ===========================================================================================================================
    public class JobStep : IJobInfo
    {
        private Func<int> Worker = null!;

        public string? Description { get; private set; } = null!;
        public string Name { get; private set; }

        /// <summary>
        /// Indicates that all subsequent steps should be stopped if this one fails!
        /// </summary>
        public bool StopIfFailed { get; private set; } = false;

        public EJobState State { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public TimeSpan TotalTime
        {
            get
            {
                return this.EndTime - this.StartTime;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------------
        public JobStep(string name_, string? description_, Func<int> worker_)
            : this(name_, description_, worker_, true)
        { }

        // --------------------------------------------------------------------------------------------------------------------------
        public JobStep(string name_, string? description_, Func<int> worker_, bool stopIfFailed_)
        {
            Name = name_;
            Worker = worker_;
            Description = description_;
            StopIfFailed = stopIfFailed_;
        }

        // --------------------------------------------------------------------------------------------------------------------------
        public int Execute()
        {
            int res = Worker();
            return res;
        }

    }

    // ============================================================================================================================
    public class StepOptions
    {
        public int[] UseSteps { get; set; } = Array.Empty<int>();
    }

    // ============================================================================================================================
    public class JobRunResult : IJobInfo
    {
        public List<JobStepResult> StepResults { get; set; } = new List<JobStepResult>();

        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public EJobState State { get; set; } = EJobState.Invalid;
        public TimeSpan TotalTime
        {
            get { return this.EndTime - this.StartTime; }
        }

        public string? LogFilePath { get; set; } = null;
    }

    // ============================================================================================================================
    public enum EJobState
    {
        Invalid = 0,
        Skipped,
        Pending,
        Active,
        Success,
        Failed,

        /// <summary>
        /// The job/step was cancelled, probably because of a previous step failure.
        /// </summary>
        Cancelled
    }

    // ===========================================================================================================================
    public interface IJobInfo
    {
        EJobState State { get; set; }
        DateTimeOffset StartTime { get; set; }
        DateTimeOffset EndTime { get; set; }
        TimeSpan TotalTime { get; }
    }




    // ============================================================================================================================
    public class JobStepResult
    {
        // --------------------------------------------------------------------------------------------------------------------------
        public JobStepResult(JobStep step_)
        {
            Step = step_;
        }

        /// <summary>
        /// The step that this result is associated with.
        /// </summary>
        public JobStep Step { get; private set; }

        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }

        public int ReturnCode { get; set; } = JobRunner.CODE_OK;
        public bool Success { get { return Exception == null && ReturnCode == JobRunner.CODE_OK; } }

        public Exception? Exception { get; set; } = null;
        public string? ExceptionDetailPath { get; set; } = null;

    }
}
