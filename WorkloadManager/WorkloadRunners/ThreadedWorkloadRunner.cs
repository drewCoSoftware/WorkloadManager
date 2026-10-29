
using drewCo.Tools.Logging;
using System.Threading;

namespace drewCo.Work;

// ============================================================================================================================
public class ThreadedWorkloadRunner<TWorkItem> : WorkloadRunner<TWorkItem>
  where TWorkItem: IWorkItem
{
  public int MaxThreads { get; private set; } = 1;

  // --------------------------------------------------------------------------------------------------------------------------
  public ThreadedWorkloadRunner(IWorkloadManager<TWorkItem> workMgr_, int maxThreads_)
      : base(workMgr_)
  {
    MaxThreads = maxThreads_;
    if (MaxThreads <= 0) { throw new InvalidOperationException("Max threads must be set to at least 1!"); }
  }


  // --------------------------------------------------------------------------------------------------------------------------
  public override void DoWork(Action<TWorkItem, IWorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException)
  {
    base.BeginWork();

    CancelSource = new CancellationTokenSource();

    bool skipRateDelay = false;

    List<Task> tasks = new List<Task>();
    for (int i = 0; i < MaxThreads; i++)
    {
      int workerId = i;
      var t = Task.Factory.StartNew(() =>
      {

        while (true)
        {
          WorkItemRequest<TWorkItem> req = WorkMgr.GetNextWorkItem(!skipRateDelay);
          if (CancelSource.IsCancellationRequested)
          {
            Log.Info("Work was cancelled!");
            return;
          }
          else if (!req.IsWorkAvailable)
          {
            if (!WorkMgr.HasActiveItems)
            {
              Log.Info($"No work for worker: {workerId}!  Quitting!");
              return;
            }

            // No work now, but other items are still in flight.  Let's wait a bit to see what happens....
            // DEBUG LOG:  NOTE: This sleep time should be the average of work items, which I'm sure workmgr can log / tell us.....
            // It can at least predict some kind of reasonable wait time I think as not all jobs may be uniform....
            // TODO: Figure out what the actual wait time should be.
            Log.Debug("Work is not available, but not all workers have completed....  Waiting for an arbitrary amount of time....");
            Thread.Sleep(250);
          }
          else
          {
            // Execute the work as normal...
            try
            {
              workAction.Invoke(req.WorkItem, WorkMgr);

              WorkMgr.SetComplete(req.WorkItem, req.WorkItem.FromCache);
            }
            catch (Exception ex)
            {
              exHandler(req.WorkItem, ex);
              WorkMgr.SetComplete(req.WorkItem, false, ex);

              if (cancelOnException)
              {
                this.Cancel();
              }
            }
          }
        }

      }, CancelSource.Token);
      tasks.Add(t);
    }
    Log.Info($"{tasks.Count} workers are now active!");

    Task.WaitAll(tasks.ToArray());

    EndWork(false);
  }



}
