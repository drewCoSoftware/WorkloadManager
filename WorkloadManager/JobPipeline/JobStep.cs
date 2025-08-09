using drewCo.Tools.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace drewCo.Work
{

  //1. _ -> b
  //2. b -> c
  //3. c -> d
  //4. d -> e

  //// ===========================================================================================================================
  //public interface IJobInput<T>
  //{
  //  T InputData { get; }
  //}

  //// ===========================================================================================================================
  //public interface IJobOutput<T>
  //{
  //  T OutputData { get; }
  //}

  // ===========================================================================================================================
  /// <summary>
  /// Provides data for a job step.  This is also responsible for saving/loading data in long term storage (disk, database, etc.)
  /// </summary>
  public interface IStepDataProvider<T>
  {
    T GetData();
  }

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
  public interface IJobStepEx<TIn, TOut> : IJobOutput<TOut>
   
   
  {
    IJobOutput<TIn>? Previous { get; }
  }



  // ===========================================================================================================================
  public abstract class JobStepEx<TIn, TOut> : IJobStepEx<TIn, TOut>
  {

    // The current outoput data, in memory...
    protected TOut? Output = default;

    public IJobOutput<TIn>? Previous { get; private set; } = null;

    // ------------------------------------------------------------------------------------------------------------------------
    public TOut GetData()
    {
      if (Output == null)
      {
        Log.Verbose("Data is not ready... Rerunning step:");
        Output = RunStep();
      }
      return Output;
    }

    // ------------------------------------------------------------------------------------------------------------------------
    public TOut RunStep()
    {
      TIn? input = default;
      if (Previous != null)
      {
        input = Previous.GetData();
      }

      TOut output = RunStepInternal(input!);
      return output;
    }

    // ------------------------------------------------------------------------------------------------------------------------
    protected abstract TOut RunStepInternal(TIn input);
  }

  // Let's good around with something.........
  // Step 1 provides 3 numbers, and step two sums them.

  // ===========================================================================================================================
  public class Step1 : JobStepEx<object, IEnumerable<int>>
  {
    // ------------------------------------------------------------------------------------------------------------------------
    protected override IEnumerable<int> RunStepInternal(object input)
    {
      // the input will be null, so fuck it.
      return new[] { 1, 2, 3 };
    }
  }

  // ===========================================================================================================================
  public class Step2 : JobStepEx<IEnumerable<int>, int>
  {
    // ------------------------------------------------------------------------------------------------------------------------
    protected override int RunStepInternal(IEnumerable<int> input)
    {
      int res = input.Sum();
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
