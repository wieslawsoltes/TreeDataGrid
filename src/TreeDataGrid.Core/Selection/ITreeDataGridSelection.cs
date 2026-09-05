using System.Collections;

namespace TreeDataGridCore.Selection
{
    public interface ITreeDataGridSelection
    {
        IEnumerable? Source { get; set; }
    }
}
