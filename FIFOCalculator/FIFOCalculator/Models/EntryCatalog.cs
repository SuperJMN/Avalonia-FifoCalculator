using System.Collections.Generic;

namespace FIFOCalculator.Models;

public class EntryCatalog
{
    public List<Entry> Inputs { get; set; }
    public List<Entry> Outputs { get; }

    public EntryCatalog(List<Entry> inputs, List<Entry> outputs)
    {
        Inputs = inputs;
        Outputs = outputs;
    }
}