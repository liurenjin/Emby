﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Xml;

namespace MediaBrowser.LocalMetadata.Savers
{
    public abstract class BaseXmlSaver : IMetadataFileSaver
    {
        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        private static readonly Dictionary<string, string> CommonTags = new[] {

                    "Added",
                    "AspectRatio",
                    "AudioDbAlbumId",
                    "AudioDbArtistId",
                    "AwardSummary",
                    "BirthDate",
                    "Budget",
                    
                    // Deprecated. No longer saving in this field.
                    "certification",

                    "Chapters",
                    "ContentRating",
                    "Countries",
                    "CustomRating",
                    "CriticRating",
                    "CriticRatingSummary",
                    "DeathDate",
                    "DisplayOrder",
                    "EndDate",
                    "Genres",
                    "Genre",
                    "GamesDbId",
                    
                    // Deprecated. No longer saving in this field.
                    "IMDB_ID",

                    "IMDB",
                    
                    // Deprecated. No longer saving in this field.
                    "IMDbId",

                    "Language",
                    "LocalTitle",
                    "OriginalTitle",
                    "LockData",
                    "LockedFields",
                    "Format3D",
                    "Metascore",
                    
                    // Deprecated. No longer saving in this field.
                    "MPAARating",

                    "MPAADescription",

                    "MusicBrainzArtistId",
                    "MusicBrainzAlbumArtistId",
                    "MusicBrainzAlbumId",
                    "MusicBrainzReleaseGroupId",

                    // Deprecated. No longer saving in this field.
                    "MusicbrainzId",

                    "Overview",
                    "ShortOverview",
                    "Persons",
                    "PlotKeywords",
                    "PremiereDate",
                    "ProductionYear",
                    "Rating",
                    "Revenue",
                    "RottenTomatoesId",
                    "RunningTime",
                    
                    // Deprecated. No longer saving in this field.
                    "Runtime",

                    "SortTitle",
                    "Studios",
                    "Tags",
                    
                    // Deprecated. No longer saving in this field.
                    "TagLine",

                    "Taglines",
                    "TMDbCollectionId",
                    "TMDbId",

                    // Deprecated. No longer saving in this field.
                    "Trailer",

                    "Trailers",
                    "TVcomId",
                    "TvDbId",
                    "Type",
                    "TVRageId",
                    "VoteCount",
                    "Website",
                    "Zap2ItId",
                    "CollectionItems",
                    "PlaylistItems",
                    "Shares"

        }.ToDictionary(i => i, StringComparer.OrdinalIgnoreCase);

        public BaseXmlSaver(IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, ILogger logger, IXmlReaderSettingsFactory xmlReaderSettingsFactory)
        {
            FileSystem = fileSystem;
            ConfigurationManager = configurationManager;
            LibraryManager = libraryManager;
            UserManager = userManager;
            UserDataManager = userDataManager;
            Logger = logger;
            XmlReaderSettingsFactory = xmlReaderSettingsFactory;
        }

        protected IFileSystem FileSystem { get; private set; }
        protected IServerConfigurationManager ConfigurationManager { get; private set; }
        protected ILibraryManager LibraryManager { get; private set; }
        protected IUserManager UserManager { get; private set; }
        protected IUserDataManager UserDataManager { get; private set; }
        protected ILogger Logger { get; private set; }
        protected IXmlReaderSettingsFactory XmlReaderSettingsFactory { get; private set; }

        protected ItemUpdateType MinimumUpdateType
        {
            get
            {
                return ItemUpdateType.MetadataDownload;
            }
        }

        public string Name
        {
            get
            {
                return XmlProviderUtils.Name;
            }
        }

        public string GetSavePath(IHasMetadata item)
        {
            return GetLocalSavePath(item);
        }

        /// <summary>
        /// Gets the save path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        protected abstract string GetLocalSavePath(IHasMetadata item);

        /// <summary>
        /// Gets the name of the root element.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        protected abstract string GetRootElementName(IHasMetadata item);

        /// <summary>
        /// Determines whether [is enabled for] [the specified item].
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="updateType">Type of the update.</param>
        /// <returns><c>true</c> if [is enabled for] [the specified item]; otherwise, <c>false</c>.</returns>
        public abstract bool IsEnabledFor(IHasMetadata item, ItemUpdateType updateType);

        protected virtual List<string> GetTagsUsed()
        {
            return new List<string>();
        }

        public void Save(IHasMetadata item, CancellationToken cancellationToken)
        {
            var path = GetSavePath(item);

            using (var memoryStream = new MemoryStream())
            {
                Save(item, memoryStream, path);

                memoryStream.Position = 0;

                cancellationToken.ThrowIfCancellationRequested();

                SaveToFile(memoryStream, path);
            }
        }

        private void SaveToFile(Stream stream, string path)
        {
            FileSystem.CreateDirectory(Path.GetDirectoryName(path));

            var file = FileSystem.GetFileInfo(path);

            var wasHidden = false;

            // This will fail if the file is hidden
            if (file.Exists)
            {
                if (file.IsHidden)
                {
                    FileSystem.SetHidden(path, false);

                    wasHidden = true;
                }
            }

            using (var filestream = FileSystem.GetFileStream(path, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.Read))
            {
                stream.CopyTo(filestream);
            }

            if (wasHidden || ConfigurationManager.Configuration.SaveMetadataHidden)
            {
                FileSystem.SetHidden(path, true);
            }
        }

        private void Save(IHasMetadata item, Stream stream, string xmlPath)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                CloseOutput = false
            };

            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                var root = GetRootElementName(item);

                writer.WriteStartDocument(true);

                writer.WriteStartElement(root);

                var baseItem = item as BaseItem;

                if (baseItem != null)
                {
                    AddCommonNodes(baseItem, writer, LibraryManager, UserManager, UserDataManager, FileSystem, ConfigurationManager);
                }

                WriteCustomElements(item, writer);

                var tagsUsed = GetTagsUsed();

                try
                {
                    AddCustomTags(xmlPath, tagsUsed, writer, Logger, FileSystem);
                }
                catch (FileNotFoundException)
                {

                }
                catch (IOException)
                {

                }
                catch (XmlException ex)
                {
                    Logger.ErrorException("Error reading existng xml", ex);
                }

                writer.WriteEndElement();

                writer.WriteEndDocument();
            }
        }

        protected abstract void WriteCustomElements(IHasMetadata item, XmlWriter writer);

        public const string DateAddedFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Adds the common nodes.
        /// </summary>
        /// <returns>Task.</returns>
        public static void AddCommonNodes(BaseItem item, XmlWriter writer, ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataRepo, IFileSystem fileSystem, IServerConfigurationManager config)
        {
            var writtenProviderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        }

        private static bool IsPersonType(PersonInfo person, string type)
        {
            return string.Equals(person.Type, type, StringComparison.OrdinalIgnoreCase) || string.Equals(person.Role, type, StringComparison.OrdinalIgnoreCase);
        }

        private void AddCustomTags(string path, List<string> xmlTagsUsed, XmlWriter writer, ILogger logger, IFileSystem fileSystem)
        {
            var settings = XmlReaderSettingsFactory.Create(false);

            settings.CheckCharacters = false;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreComments = true;

            using (var fileStream = fileSystem.OpenRead(path))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    // Use XmlReader for best performance
                    using (var reader = XmlReader.Create(streamReader, settings))
                    {
                        try
                        {
                            reader.MoveToContent();
                        }
                        catch (Exception ex)
                        {
                            logger.ErrorException("Error reading existing xml tags from {0}.", ex, path);
                            return;
                        }

                        // Loop through each element
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                var name = reader.Name;

                                if (!CommonTags.ContainsKey(name) && !xmlTagsUsed.Contains(name, StringComparer.OrdinalIgnoreCase))
                                {
                                    writer.WriteNode(reader, false);
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}