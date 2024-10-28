using drewCo.Work;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkTesters
{

  public class WorkloadTesters
  {
    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    { }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This test case was provided to show that a work item in a workload manger is capable of generating
    /// additional work items.  This kind of feature is useful in crawlers and other non-linear jobs.
    /// </summary>
    [Test]
    public void WorkItemsCanGenerateMoreWorkItems()
    {

      var firstItem = new WorkItem();

      var workMgr = new WorkloadManager<WorkItem>(new[] { firstItem });
      var runner = new SequentialWorkloadRunner<WorkItem>(workMgr);

      int workItemCount = 0;

      const int MAX_WORK_ITEMS = 3;

      // Fire off the work runner.
      runner.DoWork((wi, mgr) =>
      {
        // NOTE: Even tho we are using a sequential workload runner, it is a good idea
        // to assume that your work items might be used in a multi-threaded context.
        Interlocked.Increment(ref workItemCount);
        if (workItemCount < MAX_WORK_ITEMS)
        {
          // Here are adding new work items, up to a max count.
          mgr.AddWorkItem(new WorkItem());
        }
      }, (wi, ex) =>
      {
        Console.WriteLine("There was an exception!");
        Console.WriteLine(ex.Message);
      }, true);


      Assert.That(workItemCount, Is.EqualTo(MAX_WORK_ITEMS));
    }


  }


  class WorkItem
  {
  }

}
