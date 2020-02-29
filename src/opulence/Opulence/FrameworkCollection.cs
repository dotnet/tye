using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Opulence
{
    public sealed class FrameworkCollection : Collection<Framework>
    {
        public void AddRange(IEnumerable<Framework> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var item in items)
            {
                Add(item);
            }
        }
    }
}