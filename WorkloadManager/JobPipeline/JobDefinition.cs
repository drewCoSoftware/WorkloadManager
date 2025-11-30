using System;
using System.Collections.Generic;

namespace drewCo.Work
{
  // ============================================================================================================================
  [Obsolete("This will be removed!")]
  public class JobDefinition
  {
    public string Name { get; private set; }
    public string Description { get; private set; }

    public bool IsFrozen { get; private set; } = false;

    public List<JobStep> Steps { get; set; } = new List<JobStep>();

    public EJobState State { get; set; }

    // --------------------------------------------------------------------------------------------------------------------------
    public JobDefinition(string name_, string description_)
    {
      Name = name_;
      Description = description_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void AddStep(string name, Func<int> worker)
    {
      AddStep(name, null, worker);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void AddStep(string name, string? description, Func<int> worker)
    {
      var step = new JobStep(name, description, worker);
      AddStep(step);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void AddStep(JobStep step)
    {
      if (IsFrozen) { throw new InvalidOperationException("The job definition is frozen.  New steps cannot be added!"); }

      Steps.Add(step);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public void Freeze()
    {
      IsFrozen = true;
    }
  }
}
