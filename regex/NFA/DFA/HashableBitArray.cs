namespace Regex;

internal class HashableBitArray
{
    private uint[] words;
    private int width;

    public HashableBitArray(int width)
    {
        words = new uint[(width + 32 - 1) / 32];
        this.width = width;
    }

    public HashableBitArray(int width, IEnumerable<int> onesIndices) : this(width)
    {
        foreach (var index in onesIndices)
            Set(index);
    }

    public HashableBitArray(int width, params int[] indices) : this(width, indices.AsEnumerable()) { }

    public bool Get(int bit)
    {
        return (words[bit / 32] & (uint)1 << (bit % 32)) != 0;
    }

    public void Set(int bit)
    {
        if (bit < 0 || bit >= width)
            throw new IndexOutOfRangeException();

        words[bit / 32] |= (uint)1 << (bit % 32);
    }

    public void Clear(int bit)
    {
        if (bit < 0 || bit >= width)
            throw new IndexOutOfRangeException();

        words[bit / 32] &= ~((uint)1 << (bit % 32));
    }

    public IEnumerable<int> GetOnesIndices()
    {
        for (int i = 0; i < width; ++i)
            if (Get(i))
                yield return i;
    }

    public override int GetHashCode()
    {
        uint hash = 0;
        foreach (var word in words)
        {
            hash ^= word;
        }
        return (int)hash;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || !(obj is HashableBitArray))
            return false;

        var other = (HashableBitArray)obj;
        if (other.width != this.width)
            return false;

        for (int i = 0; i < words.Length; ++i)
        {
            if (other.words[i] != this.words[i])
                return false;
        }
        return true;
    }
}
