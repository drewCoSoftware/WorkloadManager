using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using drewCo.Tools;
using drewCo.Tools.Logging;

namespace drewCo.Work
{



  // ===========================================================================================================================
  public class JobRunnerEx
  {
    /// <summary>
    /// Standard timespan format.
    /// </summary>
    public const string TIMESPAN_FORMAT = "hh':'mm':'ss";

    /// <summary>
    /// Standard timespan format, including milliseconds.
    /// </summary>
    public const string TIMESPAN_FORMAT_MS = "hh':'mm':'ss\\.fff";

    private IJobStepEx[] AllSteps = null!;
    public int StepCount { get { return AllSteps.Length; } }

    public EJobState State { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan TotalTime
    {
      get { return this.EndTime - this.StartTime; }
    }

    public string JobName { get; private set; }
    public string JobDescription { get; private set; }

    public IJobStepEx CurrentStep { get; private set; }

    // ------------------------------------------------------------------------------------------
    /// <summary>
    /// Construct the runner using the last step, which will include all of the references
    /// to the previous steps.
    /// </summary>
    /// <param name="lastStep_"></param>
    public JobRunnerEx(string jobName_, string jobDescription_, IJobStepEx lastStep_)
    {
      JobName = jobName_;
      JobDescription = jobDescription_;

      var steps = new List<IJobStepEx>();
      var step = lastStep_;
      while (step != null)
      {
        step.State = EJobState.Pending;
        steps.Insert(0, step);
        step = step.Previous;
      }
      int count = steps.Count;
      for (int i = 0; i < count; i++)
      {
        steps[i].StepNumber = i + 1;
      }

      
      AllSteps = steps.ToArray();
    }

    // ------------------------------------------------------------------------------------------
    public JobRunResultEx Execute(StepOptions stepOps, DateTimeOffset timestamp)
    {
      ValidateOptions(stepOps);

      Log.Info($"Starting job: {this.JobName} on step:{stepOps.StartStep}");

      SetSkippedSteps(stepOps);

      StartTime = timestamp;
      State = EJobState.Active;

      // Run the steps here.....
      JobRunResultEx res = ExecuteSteps(timestamp);

      EndTime = res.EndTime;
      State = res.State;

      if (res.State == EJobState.Success)
      {
        Log.Info("Job completed successfully!");
        Log.Info($"Total runtime was: {res.TotalTime.ToString(TIMESPAN_FORMAT_MS)}");
      }
      else
      {
        Log.Error("Job did not complete successfully!");

      }

      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private JobRunResultEx ExecuteSteps(DateTimeOffset startTime)
    {
      bool cancel = false;

      var exeSw = Stopwatch.StartNew();

      var res = new JobRunResultEx();
      res.StartTime = startTime;
      res.State = EJobState.Active;

      int stepIndex = 0;
      foreach (var item in AllSteps)
      {
        ++stepIndex;
        var sw = Stopwatch.StartNew();

        CurrentStep = item;
        bool isSkipped = item.State == EJobState.Skipped;

        string logMsg = $"Step {stepIndex}: {CurrentStep.Name}" + (isSkipped ? " [SKIPPED]" : null);
        Log.Info(logMsg);

        if (isSkipped)
        {
          var skipRes = new JobStepResultEx(CurrentStep);
          res.StepResults.Add(skipRes);

          continue;
        }
        CurrentStep.State = EJobState.Active;

        var stepRes = new JobStepResultEx(CurrentStep)
        {
          StartTime = startTime,
        };

        try
        {
          Type interfaceType = typeof(IJobStepEx<,>).MakeGenericType(CurrentStep.InputType, CurrentStep.OutputType);

//          bool loadedState = CurrentStep.LoadStepData();

          //if (CurrentStep.HasStepSerializer) { 
          //}

          // NOTE: This is where any pre/post operations might need to happen.
          // Those should probably be callbacks in the step definition?
          var m = interfaceType.GetMethod("RunStep");
          if (m == null) { 
            throw new InvalidOperationException($"The current job step does not implement the interface: IJobStepEx correctly!");
          }
          m.Invoke(CurrentStep, null);

        }
        catch (Exception ex)
        {
          stepRes.Exception = ex;
        }
        finally
        {
          TimeSpan elapsed = sw.Elapsed;
          stepRes.EndTime = startTime + elapsed;
          startTime = stepRes.EndTime;

          // Raise the step complete event. [Removed for now]
          // item.OnStepComplete?.Invoke(item, new StepCompleteArgs(stepRes));

          res.StepResults.Add(stepRes);

          CurrentStep.State = stepRes.Success ? EJobState.Success : EJobState.Failed;

          // TODO: Get more granular about steps that failed, but we are allowed to proceed on...
          // We can even provide more information about the failure (bad return code, etc.)
          if (!stepRes.Success && CurrentStep.StopIfFailed)
          {
            Log.Error($"The current step: {CurrentStep.Name} failed!  All subsequent steps will be cancelled!");

            Log.Exception(stepRes.Exception);

            stepRes.ExceptionDetailPath =  "NOT AVAILABLE!";

            CurrentStep.State = EJobState.Failed;

            // Stop early!
            // All subsequent steps need to be marked as skipped!
            CancelRemainingSteps(CurrentStep, res);
            cancel = true;
          }

          // TODO: Add uniform timespan formatting function to helpers + apply to all :f3 usages...
          Log.Info($"Step Completed in: {elapsed.ToString(TIMESPAN_FORMAT)}");
        }

        if (cancel)
        {
          Log.Error("One or more steps failed and the job will be cancelled!");
          break;
        }

      }

      TimeSpan totalElapsed = exeSw.Elapsed;
      res.EndTime = res.StartTime + totalElapsed;
      res.State = cancel ? EJobState.Failed : EJobState.Success;

      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void SetSkippedSteps(StepOptions stepOps)
    {
      for (int i = 0; i < AllSteps.Length; i++)
      {
        int stepNumber = i + 1;
        if (stepNumber >= stepOps.StartStep && stepNumber <= stepOps.EndStep) { continue; }
        AllSteps[i].State = EJobState.Skipped;
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void CancelRemainingSteps(IJobStepEx afterStep, JobRunResultEx runResult)
    {
      int index = -1;
      for (int i = 0; i < AllSteps.Length; i++)
      {
        if (AllSteps[i] == afterStep) { 
        index = i;
        break;
        }
      }
      index += 1;

      for (int i = index; i < AllSteps.Length; i++)
      {
        var step = AllSteps[i];
        if (step.State != EJobState.Skipped)
        {
          step.State = EJobState.Cancelled;
        }

        runResult.StepResults.Add(new JobStepResultEx(step));
      }
    }


    // --------------------------------------------------------------------------------------------------------------------------
    private void ValidateOptions(StepOptions stepOps)
    {
      int totalSteps = AllSteps.Length;
      if (stepOps.StartStep < 1)
      {
        Log.Verbose("Fixed start step: 1");
        stepOps.StartStep = 1;
      }
      if (stepOps.EndStep > totalSteps)
      {
        Log.Verbose($"Fixed end step: {totalSteps}");
        stepOps.EndStep = totalSteps;
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public object GetData()
    {
      if (this.State != EJobState.Success) { 
        throw new InvalidOperationException("Can't report data for a non-successful pipeline!");
      }

      var last= AllSteps[AllSteps.Length-1];
      object res=  last.GetData();
      return res;
    }
  }



  // ============================================================================================================================
  /// <summary>
  /// This class will run all steps in a job definition.
  /// </summary>
  [Obsolete("This will be replaced with 'JobRunnerEx'")] 
  public class JobRunner : IJobInfo
  {
    /// <summary>
    /// Standard timespan format.
    /// </summary>
    public const string TIMESPAN_FORMAT = "hh':'mm':'ss";

    /// <summary>
    /// Standard timespan format, including milliseconds.
    /// </summary>
    public const string TIMESPAN_FORMAT_MS = "hh':'mm':'ss\\.fff";

    public const int CODE_OK = 0;
    public const int CODE_GENERAL_FAIL = -1;

    private JobDefinition JobDef = null!;

    public EJobState State { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan TotalTime
    {
      get { return this.EndTime - this.StartTime; }
    }

    private JobStep? CurrentStep = null;
    private ILogger Logger = null!;

    // --------------------------------------------------------------------------------------------------------------------------
    public JobRunner(JobDefinition jobDef_, ILogger logger_)
    {
      JobDef = jobDef_;
      if (!JobDef.IsFrozen)
      {
        throw new InvalidOperationException("The job definition must be frozen before it can be used in a JobRunner!");
      }

      Logger = logger_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public JobRunResult Execute()
    {
      return Execute(new StepOptions(), DateTimeOffset.Now);
    }

    /// <summary>
    /// This is how we will track all job steps that need to run / complete.
    /// </summary>
    private Dictionary<JobStep, int> JobStepsToNumber = new Dictionary<JobStep, int>();

    // --------------------------------------------------------------------------------------------------------------------------
    private void OnStepComplete(object sender, StepCompleteArgs args)
    {
      // Track the completed steps, and signal that we are complete when all have reported.
      int x = 10;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public JobRunResult Execute(StepOptions stepOps, DateTimeOffset timestamp)
    {
      ValidateOptions(stepOps);

      Logger.Info($"Starting Job: {JobDef.Name}");


      // All steps are now pending.
      // We will also assign the runner + hook up the event chains.
      foreach (var item in JobDef.Steps)
      {
        item.State = EJobState.Pending;
        item.JobRunner = this;
      }

      SetSkippedSteps(stepOps);

      StartTime = timestamp;
      State = EJobState.Active;

      var res = ExecuteJobSteps(StartTime);

      EndTime = res.EndTime;
      State = res.State; // ? EJobState.Success : EJobState.Failed;

      if (res.State == EJobState.Success)
      {
        Logger.Info("Job completed successfully!");
        Logger.Info($"Total runtime was: {res.TotalTime.ToString(TIMESPAN_FORMAT_MS)}");
      }
      else
      {
        Logger.Error("Job did not complete successfully!");

      }

      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void ValidateOptions(StepOptions stepOps)
    {
      int totalSteps = JobDef.Steps.Count;
      if (stepOps.StartStep < 1) { stepOps.StartStep = 1; }
      if (stepOps.EndStep > totalSteps) { 
        stepOps.EndStep = totalSteps;
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void SetSkippedSteps(StepOptions stepOps)
    {
      for (int i = 0; i < JobDef.Steps.Count; i++)
      {
        int stepNumber = i + 1;
        if (stepNumber >= stepOps.StartStep  && stepNumber <= stepOps.EndStep) { continue; }
        JobDef.Steps[i].State = EJobState.Skipped;
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <remarks>
    /// Steps marked as skipped will not be run in this function.
    /// </remarks>
    private JobRunResult ExecuteJobSteps(DateTimeOffset startTime)
    {
      bool cancel = false;

      var exeSw = Stopwatch.StartNew();

      var res = new JobRunResult();
      res.StartTime = startTime;
      res.State = EJobState.Active;

      int stepIndex = 0;
      foreach (var item in JobDef.Steps)
      {
        ++stepIndex;
        var sw = Stopwatch.StartNew();

        CurrentStep = item;
        bool isSkipped = item.State == EJobState.Skipped;

        string logMsg = $"Step {stepIndex}: {CurrentStep.Name}" + (isSkipped ? " [SKIPPED]" : null);
        Logger.Info(logMsg);

        if (isSkipped)
        {
          var skipRes = new JobStepResult(CurrentStep);
          res.StepResults.Add(skipRes);

          continue;
        }
        CurrentStep.State = EJobState.Active;

        var stepRes = new JobStepResult(CurrentStep)
        {
          StartTime = startTime,
        };

        try
        {
          // NOTE: This is where any pre/post operations might need to happen.
          // Those should probably be callbacks in the step definition?
          stepRes.ReturnCode = CurrentStep.Execute();
        }
        catch (Exception ex)
        {
          stepRes.Exception = ex;
        }
        finally
        {
          TimeSpan elapsed = sw.Elapsed;
          stepRes.EndTime = startTime + elapsed;
          startTime = stepRes.EndTime;

          // Raise the step complete event.
          item.OnStepComplete?.Invoke(item, new StepCompleteArgs(stepRes));

          res.StepResults.Add(stepRes);

          CurrentStep.State = stepRes.Success ? EJobState.Success : EJobState.Failed;

          // TODO: Get more granular about steps that failed, but we are allowed to proceed on...
          // We can even provide more information about the failure (bad return code, etc.)
          if (!stepRes.Success && CurrentStep.StopIfFailed)
          {
            Logger.Error($"The current step: {CurrentStep.Name} failed!  All subsequent steps will be cancelled!");

            stepRes.ExceptionDetailPath = Logger.Exception(stepRes.Exception);

            CurrentStep.State = EJobState.Failed;

            // Stop early!
            // All subsequent steps need to be marked as skipped!
            CancelRemainingSteps(CurrentStep, res);
            cancel = true;
          }

          // TODO: Add uniform timespan formatting function to helpers + apply to all :f3 usages...
          Logger.Info($"Step Completed in: {elapsed.ToString(TIMESPAN_FORMAT)}");
        }

        if (cancel)
        {
          Logger.Error("One or more steps failed and the job will be cancelled!");
          break;
        }

      }

      TimeSpan totalElapsed = exeSw.Elapsed;
      res.EndTime = res.StartTime + totalElapsed;
      res.State = cancel ? EJobState.Failed : EJobState.Success;

      // All done!
      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void CancelRemainingSteps(JobStep afterStep, JobRunResult runResult)
    {
      int index = JobDef.Steps.IndexOf(afterStep) + 1;
      for (int i = index; i < JobDef.Steps.Count; i++)
      {
        JobStep step = JobDef.Steps[i];
        if (step.State != EJobState.Skipped)
        {
          step.State = EJobState.Cancelled;
        }

        runResult.StepResults.Add(new JobStepResult(step));
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Makes sure that all dependencies are met.  If a certain step relies on output from a previous
    /// step this function will detect that condition and adjust the step state accordingly.
    /// </summary>
    private int EvaluateDependencies(int startStep)
    {
      // TODO: We don't have dependency evaluation at this time.
      // TODO: We should log reasons for altering the start step!
      return startStep;
    }
  }
}
