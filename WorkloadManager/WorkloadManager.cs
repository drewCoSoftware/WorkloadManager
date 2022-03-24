using System.Collections.Generic;
using drewCo.Tools;

namespace drewCo.WorkloadManager;

// ============================================================================================================================
/// <summary>
/// Keeps track of the units of work that still need to be done.
/// </summary>
public class WorkloadManager<TWorkItem>
{
  private const double UNLIMITED = -1.0d;

  protected object DataLock = new object();
  protected List<TWorkItem> RemainingWorkItems = null!;

  public int InitialItems { get; private set; }
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
  public double MaxWorkRate { get; set; } = UNLIMITED;
  public double MinWorkRate { get; set; } = UNLIMITED;

  /// <summary>
  /// Randomize the order of the work items that are retrieved.
  /// </summary>
  public bool RandomizeOrder { get; set; } = false;

  private DateTimeOffset LastWorkItemDispatchTime = DateTimeOffset.MinValue;


  // --------------------------------------------------------------------------------------------------------------------------
  public WorkloadManager(IList<TWorkItem> workItems)
  {
    RemainingWorkItems = new List<TWorkItem>(workItems);
    InitialItems = RemainingWorkItems.Count;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public WorkItemRequest GetNextWorkItem()
  {
    lock (DataLock)
    {
      // NOTE: This doesn't really work as any attempts to clear the wait externally will cause all
      // other threads to wait for this to resolve (because we are in a locked section.).
      // There ought to be a way to clear the delay and let other threads proceed.....'
      // --> Maybe workload manager has a concept of using cached data in general?  I kind of don't
      // like that since it piles responsibility on this class....
      // Perhaps some kind of internal loop for finer-grained sleep?
      if (MaxWorkRate != UNLIMITED)
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


      if (RemainingWorkItems.Count > 0)
      {

        int useIndex = 0;
        if (this.RandomizeOrder)
        {
          useIndex = RandomTools.RNG.Next(0, RemainingWorkItems.Count - 1);
        }
        TWorkItem res = RemainingWorkItems[useIndex];
        RemainingWorkItems.RemoveAt(useIndex);

        LastWorkItemDispatchTime = DateTime.Now;

        return new WorkItemRequest(true, res);
      }
      else
      {
        return new WorkItemRequest(false, default(TWorkItem));
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public virtual void AddWorkItem(TWorkItem item)
  {
    lock (DataLock)
    {
      this.RemainingWorkItems.Add(item);
    }
  }

  public record WorkItemRequest(bool IsWorkAvailable, TWorkItem WorkItem);

  // --------------------------------------------------------------------------------------------------------------------------
  public void ClearWorkRateDelay()
  {
    LastWorkItemDispatchTime = DateTime.MinValue;
  }
}


