using System.Collections.Generic;

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
    public sealed class ObjectPool<T> where T: new()
    {
        /// <summary>
        /// Manually assign this for fast, global access.
        /// Nullify when memory needs to be freed.
        /// Not a property for faster read access.
        /// </summary>
        public static ObjectPool<T> Shared;

        /// <returns>
        /// If Shared was created during this call.
        /// </returns>
        public static bool TryInit(int length)
        {
            if (Shared != null)
            {
                return false;
            }

            Shared = new ObjectPool<T>(length);
            return true;
        }

        /// <remarks>
        /// Array access might be faster than a list.
        /// <a href="https://jacksondunstan.com/articles/3058">
        /// Array vs. List Performance Jun 1, 2015
        /// </a>
        /// </remarks>
        private List<T> m_Elements;

        /// <remarks>
        /// Direct access to reset in bulk by setting m_RentIndex to 0.
        /// </remarks>
        private int m_RentIndex;

        public ObjectPool(int length)
        {
            m_Elements = new List<T>(length);

            for (int index = 0; index < length; ++index)
            {
                m_Elements.Add(new T());
            }

            m_RentIndex = length - 1;
        }

        /// <summary>
        /// For speed, depends on caller to reset state.
        /// </summary>
        public T Rent()
        {
            T element = m_Elements[m_RentIndex];
            m_Elements.RemoveAt(m_RentIndex);
            m_RentIndex--;
            if (m_RentIndex < 0)
            {
                m_Elements.Add(new T());
                m_RentIndex = 0;
            }
            return element;
        }

        /// <summary>
        /// Only valid if returned element is the last one rented.
        /// </summary>
        public void Return(T element)
        {
            m_RentIndex++;
            m_Elements.Add(element);
        }
    }
}

