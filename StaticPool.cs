namespace FineGameDesign.Pooling
{
    /// <summary>
    /// Preallocates for fast memory access.
    /// </summary>
    /// <remarks>
    /// Naming convention follows:
    /// <a href="https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1">
    /// ArrayPool
    /// </a>
    /// </remarks>
    public sealed class StaticPool<T> where T: new()
    {
        /// <summary>
        /// Manually assign this for fast, global access.
        /// Nullify when memory needs to be freed.
        /// Not a property for faster read access.
        /// </summary>
        public static StaticPool<T> Shared;

        /// <returns>
        /// If Shared was created during this call.
        /// </returns>
        public static bool TryInit(int length)
        {
            if (Shared != null)
            {
                return false;
            }

            Shared = new StaticPool<T>(length);
            return true;
        }

        /// <remarks>
        /// Array access might be faster than a list.
        /// <a href="https://jacksondunstan.com/articles/3058">
        /// Array vs. List Performance Jun 1, 2015
        /// </a>
        /// </remarks>
        private T[] m_Elements;

        /// <remarks>
        /// Direct access to reset in bulk by setting RentIndex to 0.
        /// </remarks>
        public int RentIndex;

        public StaticPool(int length)
        {
            m_Elements = new T[length];

            for (int index = 0; index < length; ++index)
            {
                m_Elements[index] = new T();
            }
        }

        /// <summary>
        /// For speed, depends on caller to reset state.
        /// </summary>
        public T Rent()
        {
            return m_Elements[RentIndex++];
        }

        /// <summary>
        /// Only valid if returned element is the last one rented.
        /// </summary>
        public void Return()
        {
            RentIndex--;
        }
    }
}
