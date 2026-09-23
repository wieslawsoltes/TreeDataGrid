// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) .NET Foundation and Contributors.
// See build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.
using System;

namespace TreeDataGridCore.Selection
{
    public class SelectionModelIndexesChangedEventArgs : EventArgs
    {
        public SelectionModelIndexesChangedEventArgs(int startIndex, int delta)
        {
            StartIndex = startIndex;
            Delta = delta;
        }

        public int StartIndex { get; }
        public int Delta { get; }
    }
}
