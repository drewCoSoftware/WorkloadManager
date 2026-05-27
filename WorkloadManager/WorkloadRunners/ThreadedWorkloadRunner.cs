
namespace drewCo.Work;

// ============================================================================================================================
public class ThreadedWorkloadRunner<TWorkItem> : WorkloadRunner<TWorkItem>
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

    List<Task> tasks = new List<Task>();
    for (int i = 0; i < MaxThreads; i++)
    {
      var t = Task.Factory.StartNew(() =>
      {
        int workerId = i;

        while (true)
        {
          WorkItemRequest<TWorkItem> req = WorkMgr.GetNextWorkItem();
          if (CancelSource.IsCancellationRequested)
          {
            Console.WriteLine("Work was cancelled!");
            return;
          }
          else if (!req.IsWorkAvailable)
          {
            if (!WorkMgr.HasActiveItems)
            {
              Console.WriteLine($"No work for worker: {workerId}!  Quitting!");
              return;
            }

            // No work now, but other items are still in flight.  Let's wait a bit to see what happens....
            // DEBUG LOG:  NOTE: This sleep time should be the average of work items, which I'm sure workmgr can log / tell us.....
            // It can at least predict some kind of reasonable wait time I think as not all jobs may be uniform....
            Thread.Sleep(250);
          }
          else
          {
            // Execute the work as normal...
            try
            {
              workAction.Invoke(req.WorkItem, WorkMgr);

              WorkMgr.SetComplete(req.WorkItem);
            }
            catch (Exception ex)
            {
              exHandler(req.WorkItem, ex);
              WorkMgr.SetComplete(req.WorkItem, ex);

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
    Console.WriteLine($"{tasks.Count} workers are now active!");

    Task.WaitAll(tasks.ToArray());

    EndWork(false);
  }



}
