using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;

namespace CSProjBatchEditor
{
    public static class ExtensionsContainer
    {
        public static string GetRelativePath(this DirectoryInfo srcDir, DirectoryInfo destDir)
        {
            Func<DirectoryInfo, List<string>> listAncestors =
                dir =>
                {
                    var dirAncestors = new List<string>();
                    var currentAncestor = dir;
                    while (currentAncestor != null)
                    {
                        dirAncestors.Add(currentAncestor.Name);
                        currentAncestor = currentAncestor.Parent;
                    }
                    dirAncestors.Reverse();
                    return dirAncestors;
                };
            var srcAncestors = listAncestors(srcDir);
            var destAncestors = listAncestors(destDir);

            int lastCommonAncestorIndex;
            for (lastCommonAncestorIndex = 0; lastCommonAncestorIndex < srcAncestors.Count && lastCommonAncestorIndex < destAncestors.Count && srcAncestors[lastCommonAncestorIndex] == destAncestors[lastCommonAncestorIndex]; lastCommonAncestorIndex++)
            { }
            lastCommonAncestorIndex--;

            if (lastCommonAncestorIndex < 0)
            {
                return destDir.FullName;
            }
            else
            {
                var relativePath = "";
                for (int i = lastCommonAncestorIndex + 1; i < destAncestors.Count; i++)
                {
                    relativePath = relativePath + destAncestors[i] + Path.DirectorySeparatorChar.ToString();
                }
                for (int i = lastCommonAncestorIndex + 1; i < srcAncestors.Count; i++)
                {
                    relativePath = ".." + Path.DirectorySeparatorChar.ToString() + relativePath;
                }
                return relativePath;
            }
        }

        public static bool IsSubDirectoryOf(this DirectoryInfo childDir, DirectoryInfo parentDir)
        {
            var tempDir = childDir;
            while (tempDir.Parent != null)
            {
                if (tempDir.Parent.FullName == parentDir.FullName)
                {
                    return true;
                }
                tempDir = tempDir.Parent;
            }
            return false;
        }

        public static bool IsSubDirectoryOfAny(this DirectoryInfo childDir, IEnumerable<DirectoryInfo> parentDirs)
        {
            return (from parentDir in parentDirs
                    where childDir.IsSubDirectoryOf(parentDir)
                    select parentDir).Any();
        }


        public static string GetChildValue(this XElement parent, string childName)
        {
            const string NAMESPACE = "{http://schemas.microsoft.com/developer/msbuild/2003}";
            var childs = parent.Elements(NAMESPACE + "" + childName);
            if (childs.Any())
            {
                return childs.First().Value;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Return the value of an attribute of the element if the attribute exists, string.Empty otherwise
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="name">The name of the attribute</param>
        /// <returns>The value of the attribute if it exists, string.Empty otherwise</returns>
        public static string GetAttributeValue(this XElement element, XName name)
        {
            return element.GetAttributeValue(name, string.Empty);
        }

        /// <summary>
        /// Return the value of an attribute of the element if the attribute exists, defaultValue otherwise
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="name">The name of the attribute</param>
        /// <param name="defaultValue">The value to be returned if the attribute doesn't exists</param>
        /// <returns>The value of the attribute if it exists, defaultValue otherwise</returns>
        public static string GetAttributeValue(this XElement element, XName name, string defaultValue)
        {
            var attribute = element.Attribute(name);
            return attribute == null ? defaultValue : attribute.Value;
        }

        /// <summary>
        /// Return the value of an attribute of the element if the attribute exists, defaultValue otherwise
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="name">The name of the attribute</param>
        /// <param name="defaultValue">The value to be returned if the attribute doesn't exists</param>
        /// <returns>The value of the attribute if it exists, defaultValue otherwise</returns>
        public static bool? GetAttributeValue(this XElement element, XName name, bool? defaultValue)
        {
            try
            {
                return XmlConvert.ToBoolean(element.Attribute(name).Value);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Return the value of an attribute of the element if the attribute exists, defaultValue otherwise
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="name">The name of the attribute</param>
        /// <param name="defaultValue">The value to be returned if the attribute doesn't exists</param>
        /// <returns>The value of the attribute if it exists, defaultValue otherwise</returns>
        public static int? GetAttributeValue(this XElement element, XName name, int? defaultValue)
        {
            try
            {
                return XmlConvert.ToInt32(element.Attribute(name).Value);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Return the value of an attribute of the element if the attribute exists, defaultValue otherwise
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="name">The name of the attribute</param>
        /// <param name="defaultValue">The value to be returned if the attribute doesn't exists</param>
        /// <returns>The value of the attribute if it exists, defaultValue otherwise</returns>
        public static DateTime? GetAttributeValue(this XElement element, XName name, DateTime? defaultValue)
        {
            try
            {
                return XmlConvert.ToDateTime(element.Attribute(name).Value, XmlDateTimeSerializationMode.Unspecified);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
