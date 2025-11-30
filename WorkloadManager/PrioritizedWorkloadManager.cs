namespace drewCo.Work;

// ============================================================================================================================
public class PrioritizedWorkloadManager<TWorkItem> : WorkloadManager<TWorkItem>
  where TWorkItem : IHasPriority
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
  public PrioritizedWorkloadManager(IList<TWorkItem> workItems, int maxItems = -1)
  : base(workItems, maxItems)
  { }


  // --------------------------------------------------------------------------------------------------------------------------
  public override WorkItemRequest<TWorkItem> GetNextWorkItem()
  {
    lock (GroupLock)
    {


        // Figure this out.....  
        // basically we can delay for a certain amount of time, but in that time it could also be possible
        // for new work items to be added.  We don't want to be in a situation where we are using GroupLock
        // for this whole work selection process as we may have to wait around for a while....
        

        // Also, in a multi-threaded scenario, how do we handle situations where many threads could come
        // in at the same time, see the same set of delays, and wait what are essentially wrong times...
        // what we want is thread one to wait a little, then the next thread to wait for the next amount
        // of time, etc.

        // Maybe 'get next work item' needs to have some kind of timestamp attached to it?
        // --> Think about it, it is a cool problem....
        // Maybe some kind of eventing..?
        // Probably just set a flag on the group to indicate that there is already a thread waiting for it
        // so we can then just skip it!
        // --> This is probably the simplest way to do it....

      int groupLen = PriorityNumers.Count;

      // This is a very simple way to do it.  It does not take any kind of possible delay into account.
      var byDelay = new List<GetItemResult<TWorkItem>>();
      GetItemResult<TWorkItem>? selectedItem = default;

      for (int i = 0; i < groupLen; i++)
      {
        var pGroup = PriorityGroups[PriorityNumers[i]];
        var getItemRes = pGroup.GetNextItem();
        if (getItemRes != null)
        {
          if (getItemRes.Delay == 0)
          {
            // Use this item immediately!
            selectedItem = getItemRes;
            break;
          }
          else
          {
            byDelay.Add(getItemRes);
          }
        }
      }

      // Every priority group had a delay.  We will use the one with the smallest amount....
      if (selectedItem == null && byDelay.Count > 0)
      {

      }


    }


    return new WorkItemRequest<TWorkItem>(false, default);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected override void InitWorkItems(IList<TWorkItem> workItems)
  {
    foreach (var item in workItems)
    {
      AddWorkItem(item);
    }
  }

  // --------------------------------------------------------------------------------------------------------------------------
  protected virtual PriorityGroup<TWorkItem> CreatePriorityGroup(int priority)
  {
    var res = new PriorityGroup<TWorkItem>(priority);
    return res;
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public override void AddWorkItem(TWorkItem item)
  {
    lock (GroupLock)
    {
      if (!PriorityGroups.TryGetValue(item.Priority, out var pGroup))
      {
        pGroup = CreatePriorityGroup(item.Priority);
        PriorityGroups.Add(item.Priority, pGroup);

        PriorityNumers.Add(item.Priority);
        PriorityNumers.Sort();
      }

      pGroup.AddItem(item);
    }
  }

}




// ============================================================================================================================
public interface IHasPriority
{
  int Priority { get; }
}

// ============================================================================================================================
public class PriorityGroup<TWorkItem>
  where TWorkItem : IHasPriority
{
  // --------------------------------------------------------------------------------------------------------------------------
  public PriorityGroup(int priority_)
  {
    Priority = priority_;
  }

  public int Priority { get; private set; }

  /// <summary>
  /// Delay, in milliseconds that this group is currently experiencing.
  /// This can happen with rate limited groups, for example.
  /// </summary>
  public int Delay { get; protected set; }


  // TODO:  We will hide this at some point....
  private List<TWorkItem> AllItems = new List<TWorkItem>();

  // --------------------------------------------------------------------------------------------------------------------------
  public void AddItem(TWorkItem item)
  {
    AllItems.Add(item);
  }
  // --------------------------------------------------------------------------------------------------------------------------
  public void RemoveItem(TWorkItem item)
  {
    AllItems.Remove(item);
  }

  // --------------------------------------------------------------------------------------------------------------------------
  public GetItemResult<TWorkItem>? GetNextItem()
  {
    if (AllItems.Count == 0) { return null; }

    return new GetItemResult<TWorkItem>()
    {
      WorkItem = AllItems[0],
      Delay = 0
    };
  }

}

// ==============================================================================================================================
public class GetItemResult<TWorkItem>
  where TWorkItem : IHasPriority
{
  public PriorityGroup<TWorkItem> Group { get; set; } = null!;
  public TWorkItem WorkItem { get; set; } = default!;
  public int Delay { get; set; } = 0;
}
// public class GetWOr
