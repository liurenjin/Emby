﻿using System;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Users;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Controller.Entities.TV
{
    /// <summary>
    /// Class Season
    /// </summary>
    public class Season : Folder, IHasSeries, IHasLookupInfo<SeasonInfo>
    {
        [IgnoreDataMember]
        public override bool SupportsAddingToPlaylist
        {
            get { return true; }
        }

        [IgnoreDataMember]
        public override bool IsPreSorted
        {
            get
            {
                return true;
            }
        }

        [IgnoreDataMember]
        public override bool SupportsDateLastMediaAdded
        {
            get
            {
                return false;
            }
        }

        [IgnoreDataMember]
        public override bool SupportsPeople
        {
            get { return true; }
        }

        [IgnoreDataMember]
        public override bool SupportsInheritedParentImages
        {
            get { return true; }
        }

        [IgnoreDataMember]
        public override Guid? DisplayParentId
        {
            get { return SeriesId; }
        }

        public override double? GetDefaultPrimaryImageAspectRatio()
        {
            double value = 2;
            value /= 3;

            return value;
        }

        public string FindSeriesSortName()
        {
            var series = Series;
            return series == null ? SeriesName : series.SortName;
        }

        public override List<string> GetUserDataKeys()
        {
            var list = base.GetUserDataKeys();

            var series = Series;
            if (series != null)
            {
                list.InsertRange(0, series.GetUserDataKeys().Select(i => i + (IndexNumber ?? 0).ToString("000")));
            }

            return list;
        }

        public override int GetChildCount(User user)
        {
            var result = GetChildren(user, true).Count;

            return result;
        }

        /// <summary>
        /// This Episode's Series Instance
        /// </summary>
        /// <value>The series.</value>
        [IgnoreDataMember]
        public Series Series
        {
            get
            {
                var seriesId = SeriesId ?? FindSeriesId();
                return seriesId.HasValue ? (LibraryManager.GetItemById(seriesId.Value) as Series) : null;
            }
        }

        [IgnoreDataMember]
        public string SeriesPath
        {
            get
            {
                var series = Series;

                if (series != null)
                {
                    return series.Path;
                }

                return FileSystem.GetDirectoryName(Path);
            }
        }

        public override string CreatePresentationUniqueKey()
        {
            if (IndexNumber.HasValue)
            {
                var series = Series;
                if (series != null)
                {
                    return series.PresentationUniqueKey + "-" + (IndexNumber ?? 0).ToString("000");
                }
            }

            return base.CreatePresentationUniqueKey();
        }

        /// <summary>
        /// Creates the name of the sort.
        /// </summary>
        /// <returns>System.String.</returns>
        protected override string CreateSortName()
        {
            return IndexNumber != null ? IndexNumber.Value.ToString("0000") : Name;
        }

        protected override QueryResult<BaseItem> GetItemsInternal(InternalItemsQuery query)
        {
            if (query.User == null)
            {
                return base.GetItemsInternal(query);
            }

            var user = query.User;

            Func<BaseItem, bool> filter = i => UserViewBuilder.Filter(i, user, query, UserDataManager, LibraryManager);

            var items = GetEpisodes(user, query.DtoOptions).Where(filter);

            var result = PostFilterAndSort(items, query, false, false);

            return result;
        }

        /// <summary>
        /// Gets the episodes.
        /// </summary>
        public List<BaseItem> GetEpisodes(User user, DtoOptions options)
        {
            return GetEpisodes(Series, user, options);
        }

        public List<BaseItem> GetEpisodes(Series series, User user, DtoOptions options)
        {
            return GetEpisodes(series, user, null, options);
        }

        public List<BaseItem> GetEpisodes(Series series, User user, IEnumerable<Episode> allSeriesEpisodes, DtoOptions options)
        {
            return series.GetSeasonEpisodes(this, user, allSeriesEpisodes, options);
        }

        public List<BaseItem> GetEpisodes()
        {
            return Series.GetSeasonEpisodes(this, null, null, new DtoOptions(true));
        }

        public override List<BaseItem> GetChildren(User user, bool includeLinkedChildren)
        {
            return GetEpisodes(user, new DtoOptions(true));
        }

        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            // Don't block. Let either the entire series rating or episode rating determine it
            return false;
        }

        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Series;
        }

        [IgnoreDataMember]
        public string SeriesPresentationUniqueKey { get; set; }

        [IgnoreDataMember]
        public string SeriesName { get; set; }

        [IgnoreDataMember]
        public Guid? SeriesId { get; set; }

        public string FindSeriesPresentationUniqueKey()
        {
            var series = Series;
            return series == null ? null : series.PresentationUniqueKey;
        }

        public string FindSeriesName()
        {
            var series = Series;
            return series == null ? SeriesName : series.Name;
        }

        public Guid? FindSeriesId()
        {
            var series = FindParent<Series>();
            return series == null ? (Guid?)null : series.Id;
        }

        /// <summary>
        /// Gets the lookup information.
        /// </summary>
        /// <returns>SeasonInfo.</returns>
        public SeasonInfo GetLookupInfo()
        {
            var id = GetItemLookupInfo<SeasonInfo>();

            var series = Series;

            if (series != null)
            {
                id.SeriesProviderIds = series.ProviderIds;
            }

            return id;
        }

        /// <summary>
        /// This is called before any metadata refresh and returns true or false indicating if changes were made
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public override bool BeforeMetadataRefresh()
        {
            var hasChanges = base.BeforeMetadataRefresh();

            if (!IndexNumber.HasValue && !string.IsNullOrEmpty(Path))
            {
                IndexNumber = IndexNumber ?? LibraryManager.GetSeasonNumberFromPath(Path);

                // If a change was made record it
                if (IndexNumber.HasValue)
                {
                    hasChanges = true;
                }
            }

            return hasChanges;
        }
    }
}
