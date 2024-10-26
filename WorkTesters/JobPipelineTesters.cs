using drewCo.Tools.Logging;
using drewCo.Work;

namespace WorkTesters
{
  // ==============================================================================================================================
  public class Tests
  {
    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    {
    }

    // --------------------------------------------------------------------------------------------------------------------------
    [Test]
    public void CanRunSimpleJobPipeline()
    {
      var def = new JobDefinition($"{nameof(CanRunSimpleJobPipeline)}_Test", "A test pipeline.");

      int x = 0;
      int y = 0;
      int z = 0;

      def.AddStep("Step 1", () =>
      {
        x = 1;
        Console.WriteLine("Step 1 Complete!");
        return 0;
      });

      def.AddStep("Step 2", () =>
      {
        y = 2;
        Console.WriteLine("Step 2 Complete!");
        return 0;
      });

      def.AddStep("Step 3", () =>
      {
        z = 3;
        Console.WriteLine("Step 3 Complete!");
        return 0;
      });
      def.Freeze();

      var runner = new JobRunner(def, new NullLogger());
      runner.Execute();

      Assert.That(x, Is.EqualTo(1));
      Assert.That(y, Is.EqualTo(2));
      Assert.That(z, Is.EqualTo(3));


    }
  }
}