namespace drewCo.WorkloadManager;

// ============================================================================================================================
public interface IWorkloadRunner<TWorkItem>
{
  void DoWork(Action<TWorkItem, WorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException);
  void Cancel();
}
