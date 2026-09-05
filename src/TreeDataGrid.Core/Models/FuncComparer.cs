using System;
using System.Collections.Generic;
using System.Text;

namespace TreeDataGridCore.Models
{
    internal class FuncComparer<T> : IComparer<T>
    {
        private readonly Comparison<T?> _func;
        public FuncComparer(Comparison<T?> func) => _func = func;
        public int Compare(T? x, T? y) => _func(x, y);
    }
}
