using System.Collections.Generic;
using System.ComponentModel.Design;
using drewCo.Tools;

namespace drewCo.Work;

// ============================================================================================================================
public record WorkItemRequest<TWorkItem>(bool IsWorkAvailable, TWorkItem? WorkItem);

// ============================================================================================================================
public class PriorityWorkItem<TWorkItem>
{
  /// <summary>
  /// Items with lower priorioty numbers are executed first.
  /// </summary>
  public int Priority { get; private set; } = 10;
  public TWorkItem WorkItem { get; private set; } = default!;

  // --------------------------------------------------------------------------------------------------------------------------
  public PriorityWorkItem(int priority_, TWorkItem workItem_)
  {
    Priority = priority_;
    WorkItem = workItem_;
  }
}

// ============================================================================================================================
internal class PriorityGroup<TWorkItem>
{
  public int Priority { get; private set; }
  public List<PriorityWorkItem<TWorkItem>> AllItems { get; private set; } = new List<PriorityWorkItem<TWorkItem>>();
}

// ============================================================================================================================
public class PrioritizedWorkloadManager<TWorkItem> : WorkloadManager<PriorityWorkItem<TWorkItem>>
{
  // The key is the priority number.
  private Dictionary<int, PriorityGroup<TWorkItem>> PriorityGroups { get; set; } = new Dictionary<int, PriorityGroup<TWorkItem>>();
  private List<int> PriorityNumers = new List<int>();
  private object GroupLock = new object();

  // --------------------------------------------------------------------------------------------------------------------------
  public PrioritizedWorkloadManager()
    : base()
  { }

  // --------------------------------------------------------------------------------------------------------------------------
  public PrioritizedWorkloadManager(IList<PriorityWorkItem<TWorkItem>> workItems, int maxItems = -1)
  : base(workItems, maxItems)
  { }


  // --------------------------------------------------------------------------------------------------------------------------
  public override WorkItemRequest<PriorityWorkItem<TWorkItem>> GetNextWorkItem()
  {
    int groupLen = PriorityNumers.Count;

    // This is a very simple way to do it.  It does not take any kind of possible delay into account.
    for (int i = 0; i < groupLen; i++)
    {
      var pGroup = PriorityGroups[PriorityNumers[i]];
      if (pGroup.AllItems.Count > 0)
      {
        PriorityWorkItem<TWorkItem> res = pGroup.AllItems[0];
        pGroup.AllItems.Remove(res);

        return new WorkItemRequest<PriorityWorkItem<TWorkItem>>(true, res);
      }
    }

    return new WorkItemRequest<PriorityWorkItem<TWorkItem>>(false, default);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected override void InitWorkItems(IList<PriorityWorkItem<TWorkItem>> workItems)
  {
    foreach (var item in workItems)
    {
      AddWorkItem(item);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public void AddWorkItem(int priority, TWorkItem item)
  {
    var pwi = new PriorityWorkItem<TWorkItem>(priority, item);
    AddWorkItem(pwi);
  }
  // --------------------------------------------------------------------------------------------------------------------------
  public override void AddWorkItem(PriorityWorkItem<TWorkItem> item)
  {
    lock (GroupLock)
    {
      if (!PriorityGroups.TryGetValue(item.Priority, out var pGroup))
      {
        pGroup = new PriorityGroup<TWorkItem>();
        PriorityGroups.Add(item.Priority, pGroup);

        PriorityNumers.Add(item.Priority);
        PriorityNumers.Sort();
      }

      pGroup.AllItems.Add(item);
    }
  }

}


// ============================================================================================================================
public interface IWorkloadManager<TWorkItem>
{
  WorkItemRequest<TWorkItem> GetNextWorkItem();
  void AddWorkItem(TWorkItem item);
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
  protected List<TWorkItem> RemainingWorkItems = null!;

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
  public float PercentCompelte
  {
    get
    {
      int complete = this.WorkItemsDispatched;
      float res = (float)complete / (float)this.TotalWorkItems;
      return res;
    }
  }

  private DateTimeOffset LastWorkItemDispatchTime = DateTimeOffset.MinValue;

  // --------------------------------------------------------------------------------------------------------------------------
  protected WorkloadManager() { }

  // --------------------------------------------------------------------------------------------------------------------------
  public WorkloadManager(IList<TWorkItem> workItems, int maxItems = UNLIMITED_WORK_ITEMS)
  {
    MaxWorkItems = maxItems;
    InitWorkItems(workItems);
    TotalWorkItems = (MaxWorkItems != UNLIMITED_WORK_ITEMS) ? MaxWorkItems : RemainingWorkItems.Count;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected virtual void InitWorkItems(IList<TWorkItem> workItems)
  {
    RemainingWorkItems = new List<TWorkItem>(workItems);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public virtual WorkItemRequest<TWorkItem> GetNextWorkItem()
  {
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
        ExecuteWorkRateDelay();

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
      // NOTE: RandomTools needs an overload for 'double'!!!!!
      double frac = RandomTools.RNG.NextDouble();
      double useWorkRate = (frac * (MaxWorkRate - MinWorkRate)) + MinWorkRate;
      double delay = 1.0d / useWorkRate;

      TimeSpan curWait = DateTime.Now - LastWorkItemDispatchTime;
      if (curWait.TotalSeconds < delay)
      {
        int waitFor = (int)((delay - curWait.TotalSeconds) * 1000);
        if (waitFor > 0)
        {
          Thread.Sleep(waitFor);
        }
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
  public void ClearWorkRateDelay()
  {
    LastWorkItemDispatchTime = DateTime.MinValue;
  }

}


