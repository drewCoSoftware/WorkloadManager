using drewCo.Tools;
using drewCo.Tools.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace drewCo.Work
{



  // ===========================================================================================================================
  /// <summary>
  /// Things that a job step can / needs to do:
  /// </summary>
  public interface IJobStep
  {
    // Each step needs to find + load dependent data.
    // That dependent data can come from the previous step, and may already be in memory.... so I guess that the
    // previous step is really the one that needs to be able to save/load that data!

    /// <summary>
    /// The step that comes before this one.  If it is null, then this step is the first.
    /// </summary>
    IJobStep? Previous { get; }

    /// <summary>
    /// Return the output data from this step.
    /// This may be already resident in memory, loaded from disk, etc.
    /// </summary>
    T GetOutput<T>();

  }


  // data, and how it is provisioned can take many forms.....
  // do we expect a list of stuff?  A single object, etc.?
  // Lists of things are nice because their content can be streamed......
  // Just using streams is a pain because they are pretty abstract.....
  // --> But I guess that is where the stream readers/writings come into play....
  // if you are going to abstract stuff like job steps, then you should expect some extra overhead to
  // deal with those abstractions....
  // In reality, it is just up to the previous step to provide the data that you request....
  // in the case of something like the data grabber, we pull a bunch of tables, and compose their content into dictionaries.
  // this is its own web of dependencies, but highlights that for a certain step we might expect some partial amount of data?
  // // --> This is an interesting thought, but maybe too complex.... it seems like we are asking for there to be a single
  // manager of data that can provide content based on previous runs, but that would be a real furball......
  // 
  // What we are actually proposing is a stepwise process that can go to a previous step is the currently requested step doesn't
  // have what it needs.

  // ===========================================================================================================================
  public interface IJobOutput<TOut>
  {
    public TOut GetData();
  }

  // ===========================================================================================================================
  public interface IJobInput<TIn>
  {
    public TIn GetData();
  }


  // ===========================================================================================================================
  public interface IJobStepEx
  {
    string Name { get; }
    string Description { get; }

    Type InputType { get; }
    Type OutputType { get; }

    IJobStepEx Previous { get; }
    object GetData();
    void ClearOutput();

    bool StopIfFailed { get; }

    // *** Record Keeping Properties *** //
    EJobState State { get; set; }
    int StepNumber { get; set; }
  }

  // ===========================================================================================================================
  public abstract class OutputSerializer<TOut>
  {
    public abstract TOut? LoadOutputData();
    public abstract bool SaveOutputData(TOut data);
  }

  // ===========================================================================================================================
  public interface IJobStepEx<TIn, TOut>
  {
    TOut RunStep();
  }

  // ===========================================================================================================================
  public class JobStepEventArgs : EventArgs
  {
    public readonly IJobStepEx Step = null!;
    public JobStepEventArgs(IJobStepEx step_)
    {
      Step = step_;
    }
  }

  // ===========================================================================================================================
  public class JobStepEx<TIn, TOut> : IJobStepEx, IJobStepEx<TIn, TOut>
  {
    // The current outoput data, in memory...
    protected TOut? Output = default;
    protected bool IsOutputSet = false;

    protected Func<TIn, TOut> ProcessStep = null!;

    /// <summary>
    /// Used to save/load state for this step.  Not always required.
    /// </summary>
    protected OutputSerializer<TOut>? OutputSerializer = null!;

    public Type InputType => typeof(TIn);
    public Type OutputType => typeof(TOut);

    public IJobStepEx? Previous { get; private set; }
    public bool StopIfFailed { get; private set; } = true;

    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;

    public EJobState State { get; set; } = EJobState.Invalid;
    public int StepNumber { get; set; }

    public class LoadDataResult
    {
      public bool HasData { get; set; } = false;
      public TOut Data { get; set; } = default!;
    }

    public EventHandler<JobStepEventArgs> OnOutputDataLoaded = null!;

    // ------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// For derived classes:
    /// </summary>
    protected JobStepEx() { }

    // ------------------------------------------------------------------------------------------------------------------------
    public JobStepEx(string name_, string description_, Func<TIn, TOut> processStep_, IJobStepEx previous_, bool stopIfFailed = true, OutputSerializer<TOut>? stepSerializer_ = null)
    {
      Name = name_;
      Description = description_;
      Previous = previous_;
      ProcessStep = processStep_;
      OutputSerializer = stepSerializer_;
    }

    // ------------------------------------------------------------------------------------------------------------------------
    public void SaveState(TOut state)
    {
      if (this.OutputSerializer != null)
      {
        this.OutputSerializer.SaveOutputData(state);
      }
    }

    // ------------------------------------------------------------------------------------------------------------------------
    public object GetData()
    {
      if (!IsOutputSet)
      {
        Log.Verbose($"Data is not ready... Rerunning step: {this.StepNumber}");
        this.State = EJobState.Rerun;
        Output = RunStep();
      }
      return Output;
    }

    // ------------------------------------------------------------------------------------------------------------------------
    public void ClearOutput()
    {
      Output = default;
      IsOutputSet = false;
    }

    // ------------------------------------------------------------------------------------------------------------------------
    public TOut RunStep()
    {
      var cached = LoadOutputData();
      if (cached.HasData)
      {
        Log.Verbose("Loaded step data from cache:");
        Output = cached.Data;

        this.OnOutputDataLoaded?.Invoke(this, new JobStepEventArgs(this));

        return Output;
      }


      TIn? input = default;
      if (Previous != null)
      {
        input = (TIn)Previous.GetData();
      }

      Output = ProcessStep(input!);
      IsOutputSet = true;

      if (this.OutputSerializer != null)
      {
        Log.Verbose($"Saving data for step: {this.StepNumber}...");
        bool saveOK = this.OutputSerializer.SaveOutputData(Output);

        // TOOD: 'LogIf' function, or predicate based overloads?
        // Def colors tho!
        if (!saveOK)
        {
          Log.Warning($"Data wan not able to be saved!");
        }
      }

      return Output;
    }


    // -----------------------------------------------------------------------------------------------
    protected LoadDataResult LoadOutputData()
    {
      LoadDataResult res = new LoadDataResult();

      if (this.OutputSerializer != null)
      {
        Log.Verbose($"Loading previous output data for step: {this.StepNumber}...");
        TOut? data = this.OutputSerializer.LoadOutputData();
        if (data != null)
        {
          res.Data = data;
          res.HasData = true;
        }
      }

      return res;
    }
  }


  // ===========================================================================================================================
  public class JobStep : IJobInfo
  {

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


    // --------------------------------------------------------------------------------------------------------------------------
    public int Execute()
    {
      int res = Worker();
      return res;
    }

  }

  //// ============================================================================================================================
  // LEGACY: This version is overkill.
  //public class StepOptions
  //{
  //  public int[] UseSteps { get; set; } = Array.Empty<int>();
  //}

  // ============================================================================================================================
  public class StepOptions
  {
    public int StartStep { get; set; } = 1;
    public int EndStep { get; set; } = int.MaxValue;
  }

  // ============================================================================================================================
  public class JobRunResultEx : IJobInfo
  {
    public List<JobStepResultEx> StepResults { get; set; } = new List<JobStepResultEx>();

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
  [Obsolete("This will be replaced with: 'JobRunResultEx'")]
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
    /// The step was set to be skipped, but had to be run because of missing data.
    /// </summary>
    Rerun,

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
  public class JobStepResultEx
  {
    // --------------------------------------------------------------------------------------------------------------------------
    public JobStepResultEx(IJobStepEx step_)
    {
      Step = step_;
      State = EJobState.Pending;
    }

    /// <summary>
    /// The step that this result is associated with.
    /// </summary>
    public IJobStepEx Step { get; private set; }

    public EJobState State { get; set; }

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public bool Success { get { return Exception == null; } }

    public Exception? Exception { get; set; } = null;
    public string? ExceptionDetailPath { get; set; } = null;
  }


  // ============================================================================================================================
  [Obsolete("This will be replaced with: 'JobStepResultEx' in the future!")]
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


  // ============================================================================================================================
  public class StepCompleteArgs : EventArgs
  {
    public readonly JobStepResult WasCancelled;

    // --------------------------------------------------------------------------------------------------------------------------
    public StepCompleteArgs(JobStepResult result_)
    {
      WasCancelled = result_;
    }
  }

}
