using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Net;
using drewCo.Tools;

namespace drewCo.Work;

// ============================================================================================================================
public record WorkItemRequest<TWorkItem>(bool IsWorkAvailable, TWorkItem? WorkItem);

// ============================================================================================================================
public interface IWorkloadData
{
  int TotalWorkItems { get; }
  int RemainingItemCount { get; }
  float PercentComplete { get; }
}

// ============================================================================================================================
public interface IWorkloadManager<TWorkItem> : IWorkloadData
{
  /// <param name="ignoreWorkRateDelay">Set to true to ignore workrate delays.  This is typically used to bypass those delays when the previous work item came from a cache.</param>
  WorkItemRequest<TWorkItem> GetNextWorkItem(bool useWorkRatedDelay);
  void AddWorkItem(TWorkItem item);
  //  void ClearWorkRateDelay();
  void SetComplete(TWorkItem item, bool fromCache, Exception? ex = null);

  void Start();
  void Stop();
  TimeSpan Runtime { get; }
  bool IsRunning { get; }

  bool HasActiveItems { get; }
}

// ============================================================================================================================
/// <summary>
/// Keeps track of the units of work that still need to be done.
/// </summary>
public class WorkloadManager<TWorkItem> : IWorkloadManager<TWorkItem>
{
  private const double UNLIMITED_WORK_RATE = -1.0d;
  private const int UNLIMITED_WORK_ITEMS = -1;

  protected object DataLock = new object();
  protected List<TWorkItem> RemainingWorkItems = new List<TWorkItem>();

  // TODO: A hashset or similar might be the way to go to speed things up!
  protected List<TWorkItem> ActiveWorkItems = new List<TWorkItem>();

  protected record class FailedWorkItem(TWorkItem Item, Exception? Exception);
  protected List<FailedWorkItem> FailedItems = new List<FailedWorkItem>();

  public bool HasActiveItems { get { return ActiveWorkItems.Count > 0; } }

  public int TotalWorkItems { get; private set; }
  public int RemainingItemCount
  {
    get
    {
      lock (DataLock)
      {
        return RemainingWorkItems.Count;
      }

    }
  }

  /// <summary>
  /// Max number of work items that can be dispatched per second.
  /// </summary>
  public double MaxWorkRate { get; set; } = UNLIMITED_WORK_RATE;
  public double MinWorkRate { get; set; } = UNLIMITED_WORK_RATE;
  public int MaxWorkItems { get; private set; } = UNLIMITED_WORK_ITEMS;
  private int WorkItemsDispatched = 0;

  /// <summary>
  /// Randomize the order of the work items that are retrieved.
  /// </summary>
  public bool RandomizeOrder { get; set; } = false;
  public float PercentComplete
  {
    get
    {
      int complete = this.WorkItemsDispatched;
      float res = (float)complete / (float)this.TotalWorkItems;
      return res;
    }
  }

  /// <summary>
  /// The total number of items that have been processed.
  /// </summary>
  private int TotalItemsCompleted = 0;

  /// <summary>
  /// The number of items we have actually done work on.
  /// Cached items don't count towards the work rate limits!
  /// </summary>
  private int NonCachedItemsCompleted = 0;

  private Stopwatch _Clock = null!;

  private DateTimeOffset LastWorkItemDispatchTime = DateTimeOffset.MinValue;

  // --------------------------------------------------------------------------------------------------------------------------
  protected WorkloadManager() { }

  // --------------------------------------------------------------------------------------------------------------------------
  public WorkloadManager(IList<TWorkItem> workItems, int maxItems = UNLIMITED_WORK_ITEMS)
  {
    MaxWorkItems = maxItems;
    RemainingWorkItems.AddRange(workItems);
    TotalWorkItems = (MaxWorkItems != UNLIMITED_WORK_ITEMS) ? MaxWorkItems : RemainingWorkItems.Count;
  }


  public bool IsRunning { get; private set; }

  public TimeSpan Runtime
  {
    get
    {
      Debug.Assert(IsRunning, "Start() not called!");
      return _Clock.Elapsed;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void Start()
  {
    Debug.Assert(!IsRunning);
    TotalItemsCompleted = 0;
    NonCachedItemsCompleted = 0;
    IsRunning = true;
    _Clock = Stopwatch.StartNew();
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void Stop()
  {
    Debug.Assert(IsRunning);
    _Clock.Stop();
  }



  // --------------------------------------------------------------------------------------------------------------------------
  public virtual WorkItemRequest<TWorkItem> GetNextWorkItem(bool useWorkRatedDelay)
  {
    // Debug.Assert(_Clock != null, "Start() not called!");

    // NOTE: RESEARCH:
    // I see a way that we can accidentally report invalid number of work items or accidentally drop
    // workers...  Consider this, we have a work function that can add more work items as they complete,
    // a web crawler is a simple example, as we crawl each url more urls may be added.
    // Scenario:
    // We have 4 threads that are processing a url queue.
    // One work item ( a url ) remains in the queue.
    // Thread #1 removes that item and begins to work on it.
    // Threads 2-4 each request a work item, and there is none so they stop working.
    // #1 completes its work and adds ten work items to the queue.
    // If 2-4 already indicated that there wasn't any work left, does that mean they stop working forever
    // and we have essentially reduced our application to single threaded?
    // --> This could be solved in a number of ways:
    // 1. When requesting work, sleep for an arbitrary amount of time if there are no work item, and there is
    // at least one worker active.  (this could be optimized by having a flag that allows for new work items or not)

    // 2. Track the number of active workers, and when new work items are added, reactivate up to a MAX_THREADS again.
    // --> Personally, I like this option the best.  Probably less logic, and more dynamically balanaced as well.

    lock (DataLock)
    {
      bool maxItemsExceeded = (MaxWorkItems != UNLIMITED_WORK_ITEMS) && (WorkItemsDispatched >= MaxWorkItems);
      if (RemainingWorkItems.Count > 0 && !maxItemsExceeded)
      {
        // NOTE: We might want to have some kind of rate-limiting workload manager, etc.
        // vs. building this directly into the workload manager.
        if (useWorkRatedDelay) { ExecuteWorkRateDelay(); }

        int useIndex = 0;
        if (this.RandomizeOrder)
        {
          useIndex = RandomTools.RNG.Next(0, RemainingWorkItems.Count - 1);
        }
        TWorkItem workItem = RemainingWorkItems[useIndex];
        RemainingWorkItems.RemoveAt(useIndex);

        LastWorkItemDispatchTime = DateTime.Now;

        var res = new WorkItemRequest<TWorkItem>(true, workItem);
        WorkItemsDispatched += 1;
        ActiveWorkItems.Add(workItem);

        return res;
      }
      else
      {
        // No more work items!
        return new WorkItemRequest<TWorkItem>(false, default(TWorkItem));
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public virtual void AddWorkItem(TWorkItem item)
  {
    lock (DataLock)
    {
      this.RemainingWorkItems.Add(item);
      this.TotalWorkItems += 1;
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void SetComplete(TWorkItem item, bool fromCache, Exception? ex = null)
  {
    lock (DataLock)
    {
      Debug.Assert(ActiveWorkItems.Contains(item), "This work item does not belong to this manager, or has already been removed!");
      ActiveWorkItems.Remove(item);

      ++TotalItemsCompleted;
      if (!fromCache)
      {
        ++NonCachedItemsCompleted;
      }

      if (ex != null)
      {
        FailedItems.Add(new FailedWorkItem(item, ex));
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  private object WaitLock = new object();
  private void ExecuteWorkRateDelay()
  {
    // NOTE: This doesn't really work as any attempts to clear the wait externally will cause all
    // other threads to wait for this to resolve (because we are in a locked section.).
    // There ought to be a way to clear the delay and let other threads proceed.....'
    // --> Maybe workload manager has a concept of using cached data in general?  I kind of don't
    // like that since it piles responsibility on this class....
    // Perhaps some kind of internal loop for finer-grained sleep?
    if (MaxWorkRate != UNLIMITED_WORK_RATE)
    {
      var workTime = _Clock.Elapsed.TotalSeconds;
      var curRate = NonCachedItemsCompleted / workTime;

      double frac = RandomTools.RNG.NextDouble();
      double useWorkRate = (frac * (MaxWorkRate - MinWorkRate)) + MinWorkRate;

      if (curRate > useWorkRate)
      {
        //lock (WaitLock)
        //{
          var waitTime = (NonCachedItemsCompleted / useWorkRate) - workTime;
          Thread.Sleep((int)(waitTime * 1000));
        //}
      }



      //double frac = RandomTools.RNG.NextDouble();
      //double useWorkRate = (frac * (MaxWorkRate - MinWorkRate)) + MinWorkRate;
      //double delay = 1.0d / useWorkRate;

      //TimeSpan curWait = DateTime.Now - LastWorkItemDispatchTime;
      //if (curWait.TotalSeconds < delay)
      //{
      //  int waitFor = (int)((delay - curWait.TotalSeconds) * 1000);
      //  if (waitFor > 0)
      //  {
      //    Thread.Sleep(waitFor);
      //  }
      //}
    }
  }


  //// --------------------------------------------------------------------------------------------------------------------------
  //public void ClearWorkRateDelay()
  //{
  //  LastWorkItemDispatchTime = DateTime.MinValue;
  //}

}


