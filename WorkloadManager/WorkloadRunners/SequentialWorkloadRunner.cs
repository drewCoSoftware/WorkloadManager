namespace drewCo.Work;

// ============================================================================================================================
/// <summary>
/// Run all of the workload items sequentially (synchronously).
/// </summary>
public class SequentialWorkloadRunner<TWorkItem> : IWorkloadRunner<TWorkItem>
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
    while (!CancelWork)
    {
      var workRequest = WorkMgr.GetNextWorkItem();

      if (CancelWork || !workRequest.IsWorkAvailable)
      {
        IsRunning = false;
        WasCancelled = false;
        IsComplete = true;
        return;
      }

      try
      {
        workAction(workRequest.WorkItem, WorkMgr);
      }
      catch (Exception ex)
      {
        exHandler(workRequest.WorkItem, ex);
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
