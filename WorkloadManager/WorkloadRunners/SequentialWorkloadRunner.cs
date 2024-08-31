namespace drewCo.WorkloadManager;

// ============================================================================================================================
/// <summary>
/// Run all of the workload items sequentially (synchronously).
/// </summary>
public class SequentialWorkloadRunner<TWorkItem> : IWorkloadRunner<TWorkItem>
{
  private WorkloadManager<TWorkItem> WorkMgr = null!;
  private bool CancelWork = false;

  // --------------------------------------------------------------------------------------------------------------------------
  public SequentialWorkloadRunner(WorkloadManager<TWorkItem> workMgr_)
  {
    WorkMgr = workMgr_;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void DoWork(Action<TWorkItem, WorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException)
  {
    while (!CancelWork)
    {
      var workRequest = WorkMgr.GetNextWorkItem();

      if (CancelWork || !workRequest.IsWorkAvailable)
      {
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
    }
  }
}
