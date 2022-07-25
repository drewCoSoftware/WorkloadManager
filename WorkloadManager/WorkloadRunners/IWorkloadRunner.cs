using System.Diagnostics;

namespace drewCo.WorkloadManager;

// ============================================================================================================================
public interface IWorkloadRunner<TWorkItem>
{
    void DoWork(Action<TWorkItem, WorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException);
    void Cancel();
}

// ============================================================================================================================
public abstract class WorkloadRunner<TWorkItem> : IWorkloadRunner<TWorkItem>
{
    protected CancellationTokenSource CancelSource = null!;
    protected WorkloadManager<TWorkItem> WorkMgr = null!;

    private Stopwatch RuntimeClock = null!;
    private TimeSpan _RunTime = TimeSpan.Zero;
    public TimeSpan RunTime
    {
        get
        {
            if (!IsRunning || RuntimeClock == null)
            {
                return TimeSpan.Zero;
            }
            _RunTime = RuntimeClock.Elapsed;
            return _RunTime;
        }
    }

    // NOTE: These flags can probaably be changed into some kind of state enumeration instead.
    public bool IsComplete {get; protected set; }= false;
    public bool WasCancelled {get; protected set; }= false;
    public bool IsRunning {get; protected set; } = false;

    // --------------------------------------------------------------------------------------------------------------------------
    public WorkloadRunner(WorkloadManager<TWorkItem> workMgr_)
    {
        WorkMgr = workMgr_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected virtual void BeginWork()
    {
        RuntimeClock = Stopwatch.StartNew();
        IsRunning = true;
        IsComplete = false;
        WasCancelled = false;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected virtual void EndWork(bool cancelled)
    {
        _RunTime = RuntimeClock.Elapsed;
        IsComplete = true;
        WasCancelled = cancelled;
        IsRunning = false;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public abstract void DoWork(Action<TWorkItem, WorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException);


    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Cancel further work operations.
    /// </summary>
    public virtual void Cancel()
    {
        CancelSource.Cancel();
        EndWork(true);
    }
}