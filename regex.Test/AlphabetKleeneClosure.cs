namespace regex.Test;

class AlphabetKleeneClosure
{
    private IEnumerable<byte> alphabet;
    private byte[][] cache1, cache2;
    private int n;

    private static int pow(int a, int b)
    {
        int p = 1;
        for (int i = 0; i < b; ++i)
            p *= a;
        return p;
    }

    public AlphabetKleeneClosure(IEnumerable<byte> alphabet, int n)
    {
        this.alphabet = alphabet;
        int p = pow(alphabet.Count(), n + 1);
        cache1 = new byte[p][];
        cache2 = new byte[p][];
        this.n = n;
    }

    public IEnumerable<byte[]> Words()
    {
        cache1[0] = [];
        yield return [];

        int q = alphabet.Count();
        int p = 1;

        for (int m = 1; m <= n; ++m)
        {
            for (int i = 0; i < p; ++i)
            {
                int j = 0;
                foreach (byte c in alphabet)
                {
                    cache2[i * q + j] = new byte[m];
                    cache1[i].CopyTo(cache2[i * q + j], 0);
                    cache2[i * q + j][m - 1] = c;
                    yield return cache2[i * q + j];
                    ++j;
                }
            }

            (cache1, cache2) = (cache2, cache1);
            p *= q;
        }
    }
}
