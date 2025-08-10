using drewCo.Tools.Logging;
using drewCo.Work;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkTesters
{

  class MutliTypeWorkItem
  {
    public object WorkItem { get; set; } = default!;
  }

  public class WorkloadTesters
  {
    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    { }


    // --------------------------------------------------------------------------------------------------------------------
    [Test]
    public void CanRerunSkippedSteps()
    {

      var step1 = new JobStepEx<object, IEnumerable<int>>("make numbers", "generates some numbers that should be summed up!", (input) =>
      {
        return new[] { 1, 2, 3 };
      }, null);

      var step2 = new JobStepEx<IEnumerable<int>, int>("sum numbers", "takes a set of numbers and computes the sum", (input) =>
      {
        return input.Sum();
      }, step1);

      var step3 = new JobStepEx<int, int>("make new number", "multiplies an number", (input) =>
      {
        return input * 3;
      }, step2);

      const int START_STEP = 3;
      var runner = new JobRunnerEx("test", "test job", step3);
      var result = runner.Execute(new StepOptions() { StartStep = START_STEP }, DateTimeOffset.Now);

      int final = (int)runner.GetData();

      Assert.That(final, Is.EqualTo(18), "Invalid result!");
      Assert.That(step1.State, Is.EqualTo(EJobState.Rerun));
      Assert.That(step2.State, Is.EqualTo(EJobState.Rerun));
      Assert.That(step3.State, Is.EqualTo(EJobState.Success));
    }

    // --------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This test case was provided to prototype some scenarios for non-linear jobs, in particular an AWR
    /// processor that I was working on.
    /// </summary>
    [Test]
    public void CanPrioritizeWorkItems()
    {
      // var workItems = new List<ThreadInterruptedException>
      var pWorkloadManager = new PrioritizedWorkloadManager<int>();

      const int STEP1_COUNT = 5;
      const int LOW_PRIOIRTY = 2;
      const int HIGH_PRIORITY = 1;

      const int NEW_ITEMS_PER_STEP = 2;

      int workItemCount = 0;
      int lowPCount = 0;
      int highPCount = 0;

      // Here we just add some low priority work items.
      // Our example is setup so that the low priority items generate higher priority
      // items.  We can then show that those priorities are respected as the code executes.
      for (int i = 0; i < STEP1_COUNT; i++)
      {
        pWorkloadManager.AddWorkItem(LOW_PRIOIRTY, i);
      }


      var runner = new SequentialWorkloadRunner<PriorityWorkItem<int>>(pWorkloadManager);
      runner.DoWork((wi, mgr) =>
      {
        // NOTE: This if/then block shows how we can process work items based on their priority group.
        // In reality, any old bit of data could be used when you need to handle different parts/steps
        // with different methods.
        if (wi.Priority == LOW_PRIOIRTY)
        {
          Assert.That(lowPCount * NEW_ITEMS_PER_STEP, Is.EqualTo(highPCount), "It appears that the priority is not being respected! [1]");

          ++lowPCount;

          for (int i = 0; i < NEW_ITEMS_PER_STEP; i++)
          {
            mgr.AddWorkItem(new PriorityWorkItem<int>(HIGH_PRIORITY, 10 + i));
            Interlocked.Increment(ref workItemCount);
          }
        }
        else
        {
          // Generate some higher priority work items!
          ++highPCount;
          Interlocked.Increment(ref workItemCount);

          if (highPCount % NEW_ITEMS_PER_STEP == 0)
          {
            Assert.That(lowPCount, Is.EqualTo(highPCount / NEW_ITEMS_PER_STEP), "It appears that the priority is not being respected! [2]");
          }
        }
      }, (wi, ex) =>
      {
        throw ex;
      }, true);

      // Show that everything ran correctly, and that the counts are all correct.
      Assert.That(lowPCount, Is.EqualTo(STEP1_COUNT));
      Assert.That(highPCount, Is.EqualTo(STEP1_COUNT * NEW_ITEMS_PER_STEP));

      Assert.IsTrue(runner.IsComplete);
      Assert.IsFalse(runner.WasCancelled);

    }

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
