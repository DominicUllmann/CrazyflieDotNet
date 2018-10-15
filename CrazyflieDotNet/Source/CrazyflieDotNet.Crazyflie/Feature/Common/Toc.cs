using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CrazyflieDotNet.Crazyflie.Feature.Common
{
    /// <summary>
    /// Container for TocElements.
    /// </summary>
    public class Toc<T> : IEnumerable<T> where T : ITocElement
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(Toc<T>));

        private IDictionary<string, IList<T>> _tocContent =
            new Dictionary<string, IList<T>>();

        /// <summary>
        /// Clear the TOC
        /// </summary>
        public void Clear()
        {
            _tocContent.Clear();
        }

        /// <summary>
        /// Add a new TocElement to the TOC container.
        /// </summary>
        public void AddElement(T element)
        {
            if (!_tocContent.ContainsKey(element.Group))
            {
                _tocContent[element.Group] = new List<T>();
            }
            var forGroup = _tocContent[element.Group];

            var existing = forGroup.FirstOrDefault(x => x.Name == element.Name);
            if (existing != null)
            {
                forGroup.Remove(existing);
            }
            forGroup.Add(element);
        }

        /// <summary>
        /// Get a TocElement element identified by complete name from the container.
        /// </summary>        
        public T GetElementByCompleteName(string completeName)
        {
            var elementId = GetElementId(completeName);
            if (elementId.HasValue)
            {
                return GetElementById(elementId.Value);
            }
            return default(T);
        }

        /// <summary>
        /// Get the TocElement element id-number of the element with the
        /// supplied name.
        /// </summary>
        public ushort? GetElementId(string completeName)
        {
            var parts = completeName.Split('.');
            if (parts.Length != 2)
            {
                throw new ArgumentException("invalid name" + completeName, nameof(completeName));
            }
            var element = GetElement(parts[0], parts[1]);
            if (element != null)
            {
                return element.Identifier;
            }
            _log.Warn($"Unable to find variable {completeName}");
            return null;
        }

        /// <summary>
        /// Get a TocElement element identified by name and group from the container
        /// </summary>
        public T GetElement(string group, string name)
        {
            if (!_tocContent.ContainsKey(group))
            {
                return default(T);
            }
            return _tocContent[group].FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Get a TocElement element identified by index number from the
        ///container.
        /// </summary>
        public T GetElementById(ushort identifier)
        {
            foreach (var groupElement in _tocContent.Values)
            {
                foreach (var nameElement in groupElement)
                {
                    if (nameElement.Identifier == identifier)
                    {
                        return nameElement;
                    }
                }
            }
            return default(T);
        }

        /// <summary>
        /// take elements from a cached LogToc.
        /// </summary>
        internal void AddFromCache(Toc<T> cached)
        {
            _tocContent = cached._tocContent;
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var groupElement in _tocContent.Values)
            {
                foreach (var nameElement in groupElement)
                {
                    yield return nameElement;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
