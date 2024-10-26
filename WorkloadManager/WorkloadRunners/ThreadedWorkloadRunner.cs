namespace drewCo.Work;

// ============================================================================================================================
public class ThreadedWorkloadRunner<TWorkItem> : WorkloadRunner<TWorkItem>
{
    public const int UNLIMITED_THREADS = -1;
    public int MaxThreads { get; private set; } = UNLIMITED_THREADS;

    // --------------------------------------------------------------------------------------------------------------------------
    public ThreadedWorkloadRunner(WorkloadManager<TWorkItem> workMgr_, int maxThreads_)
        : base(workMgr_)
    {
        MaxThreads = maxThreads_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override void DoWork(Action<TWorkItem, WorkloadManager<TWorkItem>> workAction, Action<TWorkItem, Exception> exHandler, bool cancelOnException)
    {
        base.BeginWork();

        CancelSource = new CancellationTokenSource();

        List<Task> tasks = new List<Task>();
        for (int i = 0; i < MaxThreads; i++)
        {
            var t = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    WorkloadManager<TWorkItem>.WorkItemRequest req = WorkMgr.GetNextWorkItem();
                    if (!req.IsWorkAvailable || CancelSource.IsCancellationRequested)
                    {
                        // Done working....
                        return;
                    }

                    try
                    {
                        workAction.Invoke(req.WorkItem, WorkMgr);
                    }
                    catch (Exception ex)
                    {
                        exHandler(req.WorkItem, ex);
                        if (cancelOnException)
                        {
                            this.Cancel();
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
