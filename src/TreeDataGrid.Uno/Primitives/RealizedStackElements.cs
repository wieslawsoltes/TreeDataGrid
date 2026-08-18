using System;
using System.Collections.Generic;
using Uno.Controls.Utils;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives
{
    internal class RealizedStackElements
    {
        private int _firstIndex;
        private List<Control?>? _elements;
        private List<double>? _sizes;
        private double _startU;

        public int Count => _elements?.Count ?? 0;

        public int FirstIndex => _elements?.Count > 0 ? _firstIndex : -1;

        public int LastIndex => _elements?.Count > 0 ? _firstIndex + _elements.Count - 1 : -1;

        public IReadOnlyList<Control?> Elements => _elements ??= new List<Control?>();

        public IReadOnlyList<double> SizeU => _sizes ??= new List<double>();

        public double StartU => _startU;

        public void Add(int index, Control element, double u, double sizeU)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            _elements ??= new List<Control?>();
            _sizes ??= new List<double>();

            if (Count == 0)
            {
                _elements.Add(element);
                _sizes.Add(sizeU);
                _startU = u;
                _firstIndex = index;
            }
            else if (index == LastIndex + 1)
            {
                _elements.Add(element);
                _sizes.Add(sizeU);
            }
            else if (index == FirstIndex - 1)
            {
                --_firstIndex;
                _elements.Insert(0, element);
                _sizes.Insert(0, sizeU);
                _startU = u;
            }
            else
            {
                throw new NotSupportedException("Can only add items to the beginning or end of realized elements.");
            }
        }

        public Control? GetElement(int index)
        {
            var i = index - FirstIndex;
            if (i >= 0 && i < _elements?.Count)
                return _elements[i];
            return null;
        }

        public (int index, double position) GetOrEstimateAnchorElementForViewport(
            double viewportStartU,
            double viewportEndU,
            int itemCount,
            ref double estimatedElementSizeU)
        {
            if (itemCount <= 0)
                return (-1, 0);

            if (DoubleUtils.IsZero(viewportStartU))
                return (0, 0);

            if (_sizes is not null)
            {
                var u = _startU;

                for (var i = 0; i < _sizes.Count; ++i)
                {
                    var size = _sizes[i];

                    if (double.IsNaN(size))
                        break;

                    var endU = u + size;

                    if (endU > viewportStartU && u < viewportEndU)
                        return (FirstIndex + i, u);

                    u = endU;
                }
            }

            var estimatedSize = EstimateElementSizeU() switch
            {
                <= 0 => estimatedElementSizeU,
                double value => value,
            };

            estimatedElementSizeU = estimatedSize;

            var index = Math.Min((int)(viewportStartU / estimatedSize), itemCount - 1);
            return (index, index * estimatedSize);
        }

        public double GetOrEstimateElementU(int index, ref double estimatedElementSizeU)
        {
            var u = GetElementU(index);

            if (!double.IsNaN(u))
                return u;

            var estimatedSize = EstimateElementSizeU() switch
            {
                <= 0 => estimatedElementSizeU,
                double value => value,
            };

            estimatedElementSizeU = estimatedSize;
            return index * estimatedSize;
        }

        public double EstimateElementSizeU()
        {
            if (_sizes is null)
                return -1;

            var total = 0.0;
            var count = 0;

            foreach (var size in _sizes)
            {
                if (double.IsNaN(size) || size <= 0)
                    continue;

                total += size;
                ++count;
            }

            return count > 0 && total > 0 ? total / count : -1;
        }

        public void RecycleElementsBefore(int index, Action<Control, int> recycleElement)
        {
            if (index <= FirstIndex || _elements is null || _elements.Count == 0)
                return;

            if (index > LastIndex)
            {
                RecycleAllElements(recycleElement);
                return;
            }

            var endIndex = index - FirstIndex;

            for (var i = 0; i < endIndex; ++i)
            {
                if (_elements[i] is Control element)
                {
                    _elements[i] = null;
                    recycleElement(element, i + FirstIndex);
                }
            }

            _elements.RemoveRange(0, endIndex);
            _sizes!.RemoveRange(0, endIndex);
            _firstIndex = index;
        }

        public void RecycleElementsAfter(int index, Action<Control, int> recycleElement)
        {
            if (index >= LastIndex || _elements is null || _elements.Count == 0)
                return;

            if (index < FirstIndex)
            {
                RecycleAllElements(recycleElement);
                return;
            }

            var startIndex = (index + 1) - FirstIndex;

            for (var i = startIndex; i < _elements.Count; ++i)
            {
                if (_elements[i] is Control element)
                {
                    _elements[i] = null;
                    recycleElement(element, i + FirstIndex);
                }
            }

            _elements.RemoveRange(startIndex, _elements.Count - startIndex);
            _sizes!.RemoveRange(startIndex, _sizes.Count - startIndex);
        }

        public void RecycleAllElements(Action<Control, int> recycleElement)
        {
            if (_elements is null || _elements.Count == 0)
                return;

            for (var i = 0; i < _elements.Count; ++i)
            {
                if (_elements[i] is Control element)
                {
                    _elements[i] = null;
                    recycleElement(element, i + FirstIndex);
                }
            }

            ResetForReuse();
        }

        public void ResetForReuse()
        {
            _startU = 0;
            _firstIndex = 0;
            _elements?.Clear();
            _sizes?.Clear();
        }

        private double GetElementU(int index)
        {
            if (index < FirstIndex || _sizes is null)
                return double.NaN;

            var endIndex = index - FirstIndex;

            if (endIndex >= _sizes.Count)
                return double.NaN;

            var u = StartU;

            for (var i = 0; i < endIndex; ++i)
                u += _sizes[i];

            return u;
        }
    }
}
