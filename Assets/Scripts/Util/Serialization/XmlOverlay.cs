using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Merges a sparse override document over a complete defaults document.
    /// </summary>
    /// <remarks>
    /// Merge semantics: elements are matched by name; matched leaf values are
    /// replaced, matched branches recurse, and unmatched override elements are
    /// appended. An element with repeated child names on either side is a lookup
    /// table and is replaced wholesale rather than merged entry-by-entry.
    /// </remarks>
    public static class XmlOverlay
    {
        /// <summary>
        /// Applies a sparse override document onto a defaults document in place.
        /// </summary>
        /// <param name="defaults">The complete defaults document to mutate.</param>
        /// <param name="overrides">The sparse override document to apply.</param>
        public static void Apply(XmlDocument defaults, XmlDocument overrides)
        {
            XmlElement defaultsRoot =
                defaults?.DocumentElement
                ?? throw new InvalidDataException("A defaults document root element is required.");
            XmlElement overridesRoot =
                overrides?.DocumentElement
                ?? throw new InvalidDataException("An override document root element is required.");
            if (!string.Equals(defaultsRoot.Name, overridesRoot.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Override root '{overridesRoot.Name}' does not match defaults root '{defaultsRoot.Name}'."
                );
            }

            MergeElement(defaultsRoot, overridesRoot);
        }

        /// <summary>
        /// Merges one override element into its matching defaults element.
        /// </summary>
        /// <param name="target">The defaults element to mutate.</param>
        /// <param name="overlay">The override element to apply.</param>
        private static void MergeElement(XmlElement target, XmlElement overlay)
        {
            if (HasRepeatedChildNames(target) || HasRepeatedChildNames(overlay))
            {
                ReplaceContent(target, overlay);
                return;
            }

            foreach (XmlElement overlayChild in ChildElements(overlay))
            {
                XmlElement targetChild = FindChildElement(target, overlayChild.Name);
                if (targetChild == null)
                {
                    target.AppendChild(target.OwnerDocument.ImportNode(overlayChild, true));
                }
                else if (HasChildElements(overlayChild) || HasChildElements(targetChild))
                {
                    MergeElement(targetChild, overlayChild);
                }
                else
                {
                    ReplaceContent(targetChild, overlayChild);
                }
            }
        }

        /// <summary>
        /// Replaces a defaults element's content with an override element's content.
        /// </summary>
        /// <param name="target">The defaults element to mutate.</param>
        /// <param name="overlay">The override element supplying the content.</param>
        private static void ReplaceContent(XmlElement target, XmlElement overlay)
        {
            target.RemoveAll();
            foreach (XmlNode overlayChild in overlay.ChildNodes)
                target.AppendChild(target.OwnerDocument.ImportNode(overlayChild, true));
        }

        /// <summary>
        /// Determines whether an element repeats any child element name.
        /// </summary>
        /// <param name="element">The element to inspect.</param>
        /// <returns>True when any child element name occurs more than once.</returns>
        private static bool HasRepeatedChildNames(XmlElement element)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlElement child in ChildElements(element))
            {
                if (!names.Add(child.Name))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether an element has any child elements.
        /// </summary>
        /// <param name="element">The element to inspect.</param>
        /// <returns>True when at least one child node is an element.</returns>
        private static bool HasChildElements(XmlElement element)
        {
            foreach (XmlNode child in element.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the first child element with the requested name.
        /// </summary>
        /// <param name="element">The element to search.</param>
        /// <param name="name">The child element name to match.</param>
        /// <returns>The matching child element, or null when absent.</returns>
        private static XmlElement FindChildElement(XmlElement element, string name)
        {
            foreach (XmlElement child in ChildElements(element))
            {
                if (string.Equals(child.Name, name, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Enumerates the child elements of an element.
        /// </summary>
        /// <param name="element">The element to enumerate.</param>
        /// <returns>The element's child elements in document order.</returns>
        private static IEnumerable<XmlElement> ChildElements(XmlElement element)
        {
            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlElement childElement)
                    yield return childElement;
            }
        }
    }
}
