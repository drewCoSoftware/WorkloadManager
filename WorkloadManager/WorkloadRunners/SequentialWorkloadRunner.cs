namespace drewCo.Work;

// ============================================================================================================================
/// <summary>
/// Run all of the workload items sequentially (synchronously).
/// </summary>
public class SequentialWorkloadRunner<TWorkItem> : IWorkloadRunner<TWorkItem>
  where TWorkItem: IWorkItem
{
  private IWorkloadManager<TWorkItem> WorkMgr = null!;
  private bool CancelWork = false;


  public bool IsComplete { get; private set; }
  public bool WasCancelled { get; private set; }
  public bool IsRunning { get; private set; }

  // --------------------------------------------------------------------------------------------------------------------------
  public SequentialWorkloadRunner(IWorkloadManager<TWorkItem> workMgr_)
  {
    WorkMgr = workMgr_;
  }


  // --------------------------------------------------------------------------------------------------------------------------
  public void DoWork(Action<TWorkItem, IWorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException)
  {
    IsRunning = true;
    bool skipWait = false;

    while (!CancelWork)
    {
      var req = WorkMgr.GetNextWorkItem(!skipWait);

      if (CancelWork || !req.IsWorkAvailable)
      {
        IsRunning = false;
        WasCancelled = false;
        IsComplete = true;
        return;
      }

      try
      {
        workAction(req.WorkItem, WorkMgr);
        skipWait = req.WorkItem.FromCache;
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

  // --------------------------------------------------------------------------------------------------------------------------
  public void Cancel()
  {
    if (!CancelWork)
    {
      CancelWork = true;
      WasCancelled = true;
      IsRunning = false;
      IsComplete = false;
    }
  }
}
