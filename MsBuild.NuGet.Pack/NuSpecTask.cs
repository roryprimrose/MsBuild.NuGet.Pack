namespace MsBuild.NuGet.Pack
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.Build.Framework;

    /// <summary>
    ///     The <see cref="NuSpecTask" />
    ///     class is used to provide common functionality around handling NuSpec files.
    /// </summary>
    public abstract class NuSpecTask : ITask
    {
        /// <inheritdoc />
        public abstract bool Execute();

        /// <summary>
        /// Gets the element.
        /// </summary>
        /// <param name="parent">
        /// The parent.
        /// </param>
        /// <param name="elementName">
        /// Name of the element.
        /// </param>
        /// <returns>
        /// The <see cref="XElement"/>.
        /// </returns>
        protected static XElement GetElement(XElement parent, string elementName)
        {
            var defaultNamespace = parent.Document.Root.GetDefaultNamespace();

            var element = parent.Elements().SingleOrDefault(x => x.Name == defaultNamespace + elementName);

            return element;
        }

        /// <summary>
        /// Gets the element.
        /// </summary>
        /// <param name="document">
        /// The document.
        /// </param>
        /// <param name="elementName">
        /// Name of the element.
        /// </param>
        /// <returns>
        /// The <see cref="XElement"/>.
        /// </returns>
        protected static XElement GetElement(XDocument document, string elementName)
        {
            var defaultNamespace = document.Root.GetDefaultNamespace();

            var element = document.Elements().SingleOrDefault(x => x.Name == defaultNamespace + elementName);

            return element;
        }

        /// <summary>
        /// Gets the or create element.
        /// </summary>
        /// <param name="parent">
        /// The parent.
        /// </param>
        /// <param name="elementName">
        /// Name of the element.
        /// </param>
        /// <returns>
        /// The element.
        /// </returns>
        protected static XElement GetOrCreateElement(XElement parent, string elementName)
        {
            var element = GetElement(parent, elementName);
            var defaultNamespace = parent.Document.Root.GetDefaultNamespace();

            if (element == null)
            {
                element = new XElement(defaultNamespace + elementName);

                parent.Add(element);
            }

            return element;
        }

        /// <summary>
        /// Gets the spec metadata.
        /// </summary>
        /// <param name="nuSpecDocument">
        /// The nu spec document.
        /// </param>
        /// <returns>
        /// The <see cref="XElement"/>.
        /// </returns>
        /// <exception cref="System.Exception">
        /// </exception>
        protected static XElement GetSpecMetadata(XDocument nuSpecDocument)
        {
            var package = GetElement(nuSpecDocument.Document, "package");

            if (package == null)
            {
                throw new Exception(
                    string.Format(
                        "The NuSpec file does not contain a <package> XML element. The NuSpec file appears to be invalid."));
            }

            var metadata = GetElement(package, "metadata");

            if (metadata == null)
            {
                throw new Exception(
                    string.Format(
                        "The NuSpec file does not contain a <metadata> XML element. The NuSpec file appears to be invalid."));
            }

            return metadata;
        }

        /// <summary>
        /// Opens the XML document.
        /// </summary>
        /// <param name="specFilePath">
        /// The spec file path.
        /// </param>
        /// <returns>
        /// The <see cref="XDocument"/>.
        /// </returns>
        protected static XDocument OpenXmlDocument(string specFilePath)
        {
            var xml = File.ReadAllText(specFilePath);

            return XDocument.Parse(xml);
        }

        /// <summary>
        /// Logs the message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="importance">
        /// The importance.
        /// </param>
        protected void LogMessage(string message, MessageImportance importance = MessageImportance.Normal)
        {
            var taskName = GetType().Name;

            BuildEngine.LogMessageEvent(
                new BuildMessageEventArgs(
                    taskName + ": " + message,
                    taskName,
                    taskName, 
                    importance));
        }

        /// <inheritdoc />
        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        /// <inheritdoc />
        public ITaskHost HostObject
        {
            get;
            set;
        }
    }
}