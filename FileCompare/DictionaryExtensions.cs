namespace FileCompare;

public static class DictionaryExtensions {
    public static TValue AddOrUpdate<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        TKey key,
        TValue addValue,
        Func<TKey, TValue, TValue> updateValueFactory)
    {
        if (dict.TryGetValue(key, out TValue? existing))
        {
            addValue = updateValueFactory(key, existing);
            dict[key] = addValue;
        }
        else
        {
            dict.Add(key, addValue);
        }

        return addValue;
    }
    
    public static TValue AddOrUpdate<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        TKey key,
        TValue addValue,
        Action<TValue> updateValueFactory)
    {
        if (dict.TryGetValue(key, out TValue? existing))
        {
            updateValueFactory(existing);
        }
        else
        {
            dict.Add(key, addValue);
        }

        return addValue;
    }


    public static TValue AddOrUpdate<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        TKey key,
        Func<TKey, TValue> addValueFactory,
        Func<TKey, TValue, TValue> updateValueFactory)
    {
        if (dict.TryGetValue(key, out TValue? existing))
        {
            existing = updateValueFactory(key, existing);
            dict[key] = existing;
        }
        else
        {
            existing = addValueFactory(key);
            dict.Add(key, existing);
        }

        return existing;
    }
}