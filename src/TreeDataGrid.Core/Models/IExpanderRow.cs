using System.ComponentModel;

namespace TreeDataGridCore.Models
{
    /// <summary>
    /// Represents a row which can be expanded to reveal nested data.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public interface IExpanderRow<TModel> : IRow<TModel>, IExpander, INotifyPropertyChanged
    {
        /// <summary>
        /// Updates the
        /// row's <see cref="IExpander.ShowExpander"/> state.
        /// </summary>
        /// <param name="value">The new value.</param>
        void UpdateShowExpander(bool value);
    }
}
