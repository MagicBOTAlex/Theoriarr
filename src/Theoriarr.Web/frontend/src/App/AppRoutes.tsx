import React from 'react';
import { Navigate, Route } from 'react-router-dom';
import CalendarPage from 'Calendar/CalendarPage';
import CombinedCalendarPage from 'Calendar/Combined/CombinedCalendarPage';
import NotFound from 'Components/NotFound';
import Dashboard from 'Dashboard/Dashboard';
import MediaActivityPage from 'Library/Activity/MediaActivityPage';
import AddNewPage from 'Library/AddNewPage';
import MediaCompressionPage from 'Library/Compression/MediaCompressionPage';
import Library from 'Library/Library';
import LibraryImportPage from 'Library/LibraryImportPage';
import MediaWantedPage from 'Library/Wanted/MediaWantedPage';
import CollectionDetailsPage from 'Movies/Collections/CollectionDetailsPage';
import Collections from 'Movies/Collections/Collections';
import DiscoverMovie from 'Movies/Discover/DiscoverMovie';
import MovieDetailsPage from 'Movies/MovieDetails/MovieDetailsPage';
import MovieImportPage from 'Movies/MovieImport/MovieImportPage';
import SeriesDetailsPage from 'Series/Details/SeriesDetailsPage';
import Compression from 'Settings/Compression/Compression';
import CustomFormatSettingsPage from 'Settings/CustomFormats/CustomFormatSettingsPage';
import DownloadClientSettings from 'Settings/DownloadClients/DownloadClientSettings';
import GeneralSettings from 'Settings/General/GeneralSettings';
import ImportListSettings from 'Settings/ImportLists/ImportListSettings';
import IndexerSettings from 'Settings/Indexers/IndexerSettings';
import MediaManagement from 'Settings/MediaManagement/MediaManagement';
import MetadataSettings from 'Settings/Metadata/MetadataSettings';
import MetadataSourceSettings from 'Settings/MetadataSource/MetadataSourceSettings';
import NotificationSettings from 'Settings/Notifications/NotificationSettings';
import Profiles from 'Settings/Profiles/Profiles';
import Quality from 'Settings/Quality/Quality';
import Settings from 'Settings/Settings';
import { SettingsServiceProvider } from 'Settings/SettingsServiceContext';
import TagSettings from 'Settings/Tags/TagSettings';
import UISettings from 'Settings/UI/UISettings';
import Statistics from 'Statistics/Statistics';
import Backups from 'System/Backup/Backups';
import LogsTable from 'System/Events/LogsTable';
import Logs from 'System/Logs/Logs';
import Status from 'System/Status/Status';
import Tasks from 'System/Tasks/Tasks';
import Updates from 'System/Updates/Updates';

function serviceAware(page: React.ReactNode) {
  return <SettingsServiceProvider>{page}</SettingsServiceProvider>;
}

export function appRouteElements() {
  return (
    <>
      {/*
        Library is the landing page; the Dashboard lives at /dashboard.
      */}

      <Route path="/" element={<Navigate to="/library" replace={true} />} />

      {/*
        Library — one tree for shows and movies.
      */}

      <Route path="/library" element={<Library />} />

      <Route path="/dashboard" element={<Dashboard />} />

      {/*
        Legacy per-domain index URLs redirect into the unified Library.
      */}

      <Route
        path="/series"
        element={<Navigate to="/library?type=series" replace={true} />}
      />

      <Route
        path="/movie"
        element={<Navigate to="/library?type=movie" replace={true} />}
      />

      {/*
        Movies
      */}

      <Route
        path="/movie/add/new"
        element={<Navigate to="/add/new?type=movies" replace={true} />}
      />

      <Route path="/movie/add/discover" element={<DiscoverMovie />} />

      <Route
        path="/movie/add/import"
        element={<Navigate to="/add/import?type=movies" replace={true} />}
      />

      <Route path="/movie/collections" element={<Collections />} />

      <Route path="/movie/collection/:id" element={<CollectionDetailsPage />} />

      <Route
        path="/movie/calendar"
        element={<Navigate to="/calendar/all" replace={true} />}
      />

      <Route path="/movie/import" element={<MovieImportPage />} />

      <Route path="/movie/:id" element={<MovieDetailsPage />} />

      {/*
        Series
      */}

      <Route path="/add/new" element={<AddNewPage />} />

      <Route path="/add/import/*" element={<LibraryImportPage />} />

      <Route
        path="/serieseditor"
        element={<Navigate to="/library?type=series" replace={true} />}
      />

      <Route
        path="/seasonpass"
        element={<Navigate to="/library?type=series" replace={true} />}
      />

      <Route path="/series/:titleSlug" element={<SeriesDetailsPage />} />

      <Route path="/transcoding" element={<MediaCompressionPage />} />

      <Route path="/statistics" element={<Statistics />} />

      {/*
        Calendar
      */}

      <Route path="/calendar" element={<CalendarPage />} />

      <Route path="/calendar/all" element={<CombinedCalendarPage />} />

      {/*
        Activity — one unified queue/history/blocklist for shows and movies.
      */}

      <Route
        path="/activity"
        element={<Navigate to="/activity/queue" replace={true} />}
      />

      <Route path="/activity/history" element={<MediaActivityPage />} />

      <Route path="/activity/queue" element={<MediaActivityPage />} />

      <Route path="/activity/blocklist" element={<MediaActivityPage />} />

      <Route path="/activity/compression" element={<MediaCompressionPage />} />

      <Route
        path="/activity/movies"
        element={<Navigate to="/activity/queue" replace={true} />}
      />

      {/*
        Wanted — one unified list of missing/cutoff items for shows and movies.
      */}

      <Route
        path="/wanted"
        element={<Navigate to="/wanted/missing" replace={true} />}
      />

      <Route path="/wanted/missing" element={<MediaWantedPage />} />

      <Route path="/wanted/cutoffunmet" element={<MediaWantedPage />} />

      <Route
        path="/wanted/movies"
        element={<Navigate to="/wanted/missing" replace={true} />}
      />

      {/*
        Settings — one tree for both domains. Pages that can be configured for
        either domain are wrapped in a SettingsServiceProvider, which shows the
        Series/Movies switch in the toolbar. Everything else is shared/series.
      */}

      <Route path="/settings" element={<Settings />} />

      <Route path="/settings/mediamanagement" element={<MediaManagement />} />

      <Route path="/settings/transcode" element={<Compression />} />

      <Route path="/settings/profiles" element={serviceAware(<Profiles />)} />

      <Route path="/settings/quality" element={<Quality />} />

      <Route
        path="/settings/customformats"
        element={serviceAware(<CustomFormatSettingsPage />)}
      />

      <Route
        path="/settings/indexers"
        element={serviceAware(<IndexerSettings />)}
      />

      <Route
        path="/settings/downloadclients"
        element={<DownloadClientSettings />}
      />

      <Route
        path="/settings/importlists"
        element={serviceAware(<ImportListSettings />)}
      />

      <Route
        path="/settings/connect"
        element={serviceAware(<NotificationSettings />)}
      />

      <Route
        path="/settings/metadata"
        element={serviceAware(<MetadataSettings />)}
      />

      <Route
        path="/settings/metadatasource"
        element={<MetadataSourceSettings />}
      />

      <Route path="/settings/tags" element={serviceAware(<TagSettings />)} />

      <Route path="/settings/general" element={<GeneralSettings />} />

      <Route path="/settings/ui" element={<UISettings />} />

      {/*
        Legacy Movies Settings URLs redirect into the single tree.
      */}

      <Route
        path="/settings/movies"
        element={<Navigate to="/settings" replace={true} />}
      />

      <Route
        path="/settings/movies/rootfolders"
        element={<Navigate to="/settings/mediamanagement" replace={true} />}
      />

      <Route
        path="/settings/movies/quality"
        element={<Navigate to="/settings/profiles" replace={true} />}
      />

      <Route
        path="/settings/movies/customformats"
        element={<Navigate to="/settings/customformats" replace={true} />}
      />

      <Route
        path="/settings/movies/indexers"
        element={<Navigate to="/settings/indexers" replace={true} />}
      />

      <Route
        path="/settings/movies/downloadclients"
        element={<Navigate to="/settings/downloadclients" replace={true} />}
      />

      <Route
        path="/settings/movies/importlists"
        element={<Navigate to="/settings/importlists" replace={true} />}
      />

      <Route
        path="/settings/movies/connect"
        element={<Navigate to="/settings/connect" replace={true} />}
      />

      <Route
        path="/settings/movies/metadata"
        element={<Navigate to="/settings/metadata" replace={true} />}
      />

      <Route
        path="/settings/movies/tags"
        element={<Navigate to="/settings/tags" replace={true} />}
      />

      {/*
        System
      */}

      <Route path="/system/status" element={<Status />} />

      <Route path="/system/tasks" element={<Tasks />} />

      <Route path="/system/backup" element={<Backups />} />

      <Route path="/system/updates" element={<Updates />} />

      <Route path="/system/events" element={<LogsTable />} />

      <Route path="/system/logs/files/*" element={<Logs />} />

      {/*
        Not Found
      */}

      <Route path="*" element={<NotFound />} />
    </>
  );
}
