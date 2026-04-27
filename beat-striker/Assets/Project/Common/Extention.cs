
using System;
using System.Collections.Generic;


public static class Extentions {

    public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TSource : class {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var comparer = Comparer<TKey>.Default;

        using (var iterator = source.GetEnumerator()) {
            if (!iterator.MoveNext()) {
                return null;
            }

            var minElement = iterator.Current;
            var minKey = selector(minElement);

            while (iterator.MoveNext()) {
                var currentElement = iterator.Current;
                var currentKey = selector(currentElement);

                if (comparer.Compare(currentKey, minKey) < 0) {
                    minElement = currentElement;
                    minKey = currentKey;
                }
            }

            return minElement;
        }
    }
}
