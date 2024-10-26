﻿using System;
using System.Diagnostics;
using System.Linq;
using drewCo.Tools.Logging;

namespace drewCo.Work
{
    // ============================================================================================================================
    internal class JobRunner : IJobInfo
    {
        public const string TIMESPAN_FORMAT = "hh':'mm':'ss";

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
        private Logger Logger = null!;

        // --------------------------------------------------------------------------------------------------------------------------
        public JobRunner(JobDefinition jobDef_, Logger logger_)
        {
            JobDef = jobDef_;
            if (!JobDef.IsFrozen)
            {
                throw new InvalidOperationException("The job definition must be frozen!");
            }

            Logger = logger_;
        }

        // --------------------------------------------------------------------------------------------------------------------------
        public JobRunResult Execute(StepOptions stepOps, DateTimeOffset timestamp)
        {
            ValidateOptions(stepOps);

            Logger.Info($"Starting Job: {JobDef.Name}");


            // All steps are now pending.
            foreach (var item in JobDef.Steps)
            {
                item.State = EJobState.Pending;
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
            if (stepOps.UseSteps?.Length > 0)
            {
                foreach (var stepNumber in stepOps.UseSteps)
                {
                    if (stepNumber < 1 || stepNumber > totalSteps)
                    {
                        throw new InvalidOperationException($"Invalid start step!  Must be between 1 and {totalSteps} for this job!");
                    }
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------------
        private void SetSkippedSteps(StepOptions stepOps)
        {
            if (stepOps.UseSteps?.Length > 0)
            {
                int totalSteps = JobDef.Steps.Count;
                for (int i = 0; i < totalSteps; i++)
                {
                    var step = JobDef.Steps[i];
                    if (!stepOps.UseSteps.Contains(i + 1))
                    {
                        step.State = EJobState.Skipped;
                    }
                }
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

                    res.StepResults.Add(stepRes);

                    CurrentStep.State = stepRes.Success ? EJobState.Success : EJobState.Failed;

                    // TODO: Get more granular about steps that failed, but we are allowed to proceed on...
                    // We can even provide more information about the failure (bad return code, etc.)
                    if (!stepRes.Success && CurrentStep.StopIfFailed)
                    {
                        Logger.Error($"The current step: {CurrentStep.Name} failed!  All subsequent steps will be cancelled!");

                        stepRes.ExceptionDetailPath = Logger.LogException(stepRes.Exception);

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
