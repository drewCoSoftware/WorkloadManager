
namespace drewCo.WorkloadManager;

/// <summary>
/// A workflow is used to model a set of steps that a particular task must take.  For example,
/// download an image from the internet, compute a color histogram, and report the results to the server.
/// The workflow maintains state data so that the user may observe its current progress, failed steps,
/// manually trigger further steps restart and so on.
/// </summary>
public class Workflow
{

}

// public interface IWorkTask
// {
//   DateTimeOffset LastRun { get; set; }
// }

// TODO: Extract an interface later.
// public class WorkTask // : IWorkTask
// {
//   public WorkTask(WorkTask parent_ = null)
//   {
//   }

//   public DateTimeOffset LastRun { get; set; } = DateTimeOffset.MinValue;
//   public  void DoWork();

//   public virtual void GetInputData();
//   public virtual void 
// }