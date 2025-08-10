using drewCo.CsvTools;
using drewCo.Tools;
using drewCo.Work;

namespace WorkloadManager.StepSerializers;

  // =========================================================================================================================
  /// <summary>
  /// Saves / Loads IEnumerable instances to/from CSV files.
  /// </summary>
  public class EnumerabelToCSVSerializer<T> : StepSerializer<IEnumerable<T>>
  {
    /// <summary>
    /// The path that the data will be saved / loaded from.
    /// </summary>
    private string SavePath = null!;

    // --------------------------------------------------------------------------------------------------------------------------
    public EnumerabelToCSVSerializer(string savePath_)
    {
      SavePath = savePath_;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override IEnumerable<T>? LoadStepData()
    {
      if (!File.Exists(SavePath))
      {
        return null;
      }

      var res = CsvTools.ReadCSV<T>(SavePath);
      return res;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override bool SaveStepData(IEnumerable<T> data)
    {
      string dir = Path.GetDirectoryName(SavePath);
      FileTools.CreateDirectory(dir);

      CsvTools.WriteCSV(SavePath, data);
      return true;
    }
  }
