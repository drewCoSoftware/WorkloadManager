using System;
using System.Collections.Generic;

namespace drewCo.Work
{
  // ===========================================================================================================================
  // TODO: Some way for the job steps to have sub-steps / signal work availability?
  public class JobStep : IJobInfo
  {
    /// <summary>
    /// Used when a new unit of work is ready for the job runner.
    /// </summary>
    public EventHandler<WorkReadyArgs>? OnWorkReady;

    /// <summary>
    /// Used to signal when this step is complete.  This happens when no more units of work are ready.
    /// Typically downstream listeners can then de-subscribe to the 'OnWorkReady' event.
    /// </summary>
    public EventHandler<StepCompleteArgs>? OnStepComplete;

    public JobRunner JobRunner { get; internal set; } = null!;

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

    /// <summary>
    /// Used to handle work ready events from the parent step.  Override if you need this information.
    /// </summary>
    public virtual void OnWorkReadyHandler(object sender, WorkReadyArgs args)
    {
      int x = 10;
    }

    /// <summary>
    /// Used to handle step complete events from the parent step.  Override if you need this information.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    public virtual void OnStepCompleteHandler(object sender, StepCompleteArgs args)
    {
      int x = 10;
    }



    // --------------------------------------------------------------------------------------------------------------------------
    public int Execute()
    {
      int res = Worker();

      OnStepComplete?.Invoke(this, new StepCompleteArgs());

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
