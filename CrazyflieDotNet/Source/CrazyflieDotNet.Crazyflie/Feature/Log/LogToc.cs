using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrazyflieDotNet.Crazyflie.Feature.Log
{
    /// <summary>
    /// Container for TocElements.
    /// </summary>
    public class LogToc
    {

        private static readonly ILog _log = LogManager.GetLogger(typeof(LogToc));

        private IDictionary<string, IList<LogTocElement>> _tocContent =
            new Dictionary<string, IList<LogTocElement>>();

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
        public void AddElement(LogTocElement element)
        {
            if (!_tocContent.ContainsKey(element.Group))
            {
                _tocContent[element.Group] = new List<LogTocElement>();
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
        public LogTocElement GetElementByCompleteName(string completeName)
        {
            var elementId = GetElementId(completeName);
            if (elementId.HasValue)
            {
                return GetElementById(elementId.Value);
            }
            return null;
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
        public LogTocElement GetElement(string group, string name)
        {
            if (!_tocContent.ContainsKey(group))
            {
                return null;
            }
            return _tocContent[group].FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Get a TocElement element identified by index number from the
        ///container.
        /// </summary>
        public LogTocElement GetElementById(ushort identifier)
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
            return null;
        }

        /// <summary>
        /// take elements from a cached LogToc.
        /// </summary>
        internal void AddFromCache(LogToc cached)
        {
            _tocContent = cached._tocContent;
        }
    }
}
