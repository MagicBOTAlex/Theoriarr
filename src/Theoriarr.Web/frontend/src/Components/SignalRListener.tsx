import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';
import { QueryKey, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { setAppValue, setVersion } from 'App/appStore';
import ModelBase from 'App/ModelBase';
import Command from 'Commands/Command';
import { useUpdateCommand } from 'Commands/useCommands';
import Episode from 'Episode/Episode';
import { EpisodeFile } from 'EpisodeFile/EpisodeFile';
import { PagedQueryResponse } from 'Helpers/Hooks/usePagedApiQuery';
import { getReleaseSearchProgressActions } from 'InteractiveSearch/releaseSearchProgressStore';
import Series from 'Series/Series';
import { getService } from 'Services';
import { DownloadClientModel } from 'Settings/DownloadClients/DownloadClients/useDownloadClients';
import { ImportListModel } from 'Settings/ImportLists/ImportLists/useImportLists';
import { IndexerModel } from 'Settings/Indexers/useIndexers';
import { MetadataModel } from 'Settings/Metadata/useMetadata';
import { NotificationModel } from 'Settings/Notifications/useConnections';
import { repopulatePage } from 'Utilities/pagePopulator';
import SignalRLogger from 'Utilities/SignalRLogger';

type SignalRAction = 'sync' | 'created' | 'updated' | 'deleted';

interface ReleaseSearchProgressBody {
  searchId: string;
  status: 'started' | 'indexerCompleted' | 'completed';
  indexers?: { id: number; name: string }[];
  indexerId?: number;
  indexerName?: string;
  releaseCount?: number;
  failed?: boolean;
}

interface SignalRMessage {
  name: string;
  body: {
    action: SignalRAction;
    resource: ModelBase;
    version: string;
  } & Partial<ReleaseSearchProgressBody>;
  version: number | undefined;
}

function handleReleaseSearchProgress(body: Partial<ReleaseSearchProgressBody>) {
  const { searchId, status } = body;

  if (!searchId) {
    return;
  }

  const actions = getReleaseSearchProgressActions();

  if (status === 'started') {
    actions.startSearch(searchId, body.indexers ?? []);

    return;
  }

  if (status === 'indexerCompleted') {
    actions.completeIndexer(
      searchId,
      body.indexerId ?? 0,
      body.indexerName ?? '',
      body.releaseCount ?? 0,
      body.failed ?? false
    );

    return;
  }

  if (status === 'completed') {
    actions.completeSearch(searchId);
  }
}

function SignalRListener() {
  const queryClient = useQueryClient();
  const updateCommand = useUpdateCommand();
  const connection = useRef<HubConnection | null>(null);
  const moviesConnection = useRef<HubConnection | null>(null);

  const handleStartFail = useRef((error: unknown) => {
    console.error('[signalR] failed to connect');
    console.error(error);

    setAppValue({
      isConnected: false,
      isReconnecting: false,
      isDisconnected: false,
      isRestarting: false,
    });
  });

  const handleStart = useRef(() => {
    console.debug('[signalR] connected');

    setAppValue({
      isConnected: true,
      isReconnecting: false,
      isDisconnected: false,
      isRestarting: false,
    });
  });

  const handleReconnecting = useRef(() => {
    setAppValue({ isReconnecting: true });
  });

  const handleReconnected = useRef(() => {
    setAppValue({
      isConnected: true,
      isReconnecting: false,
      isDisconnected: false,
      isRestarting: false,
    });

    // Repopulate the page (if a repopulator is set) to ensure things
    // are in sync after reconnecting.
    queryClient.invalidateQueries({ queryKey: ['/series'] });
    queryClient.invalidateQueries({ queryKey: ['/command'] });

    repopulatePage();
  });

  const handleClose = useRef(() => {
    console.debug('[signalR] connection closed');
  });

  const handleReceiveMessage = useRef((message: SignalRMessage) => {
    console.debug(
      `[signalR] received ${message.name}${
        message.version ? ` v${message.version}` : ''
      }`,
      message.body
    );

    const { name, body, version = 0 } = message;

    if (version < 5) {
      return;
    }

    if (name === 'calendar') {
      if (body.action === 'updated') {
        const updatedItem = body.resource as Episode;
        updateQueryClientItem(queryClient, ['/calendar'], updatedItem, true);

        return;
      }
    }

    if (name === 'command') {
      if (body.action === 'sync') {
        queryClient.invalidateQueries({ queryKey: ['/command'] });
        return;
      }

      const resource = body.resource as Command;

      updateCommand(resource);

      return;
    }

    if (name === 'downloadclient') {
      const updatedItem = body.resource as DownloadClientModel;

      if (body.action === 'created' || body.action === 'updated') {
        updateQueryClientItem(
          queryClient,
          ['/downloadclient'],
          updatedItem,
          true
        );
      } else if (body.action === 'deleted') {
        removeQueryClientItem(
          queryClient,
          ['/downloadclient'],
          body.resource.id
        );
      }

      return;
    }

    if (name === 'episode') {
      if (body.action === 'updated') {
        const updatedItem = body.resource as Episode;

        updateQueryClientItem(queryClient, ['/episode'], updatedItem, false);
      }

      return;
    }

    if (name === 'episodefile') {
      if (body.action === 'updated') {
        const updatedItem = body.resource as EpisodeFile;

        updateQueryClientItem(queryClient, ['/episodeFile'], updatedItem, true);

        // Repopulate the page to handle recently imported file
        repopulatePage('episodeFileUpdated');
      } else if (body.action === 'deleted') {
        const id = body.resource.id;

        removeQueryClientItem(queryClient, ['/episodeFile'], id);
        repopulatePage('episodeFileDeleted');
      }

      return;
    }

    if (name === 'health') {
      queryClient.invalidateQueries({ queryKey: ['/health'] });
      return;
    }

    if (name === 'importlist') {
      const updatedItem = body.resource as ImportListModel;

      if (body.action === 'created' || body.action === 'updated') {
        updateQueryClientItem(queryClient, ['/importlist'], updatedItem, true);
      } else if (body.action === 'deleted') {
        removeQueryClientItem(queryClient, ['/importlist'], body.resource.id);
      }

      return;
    }

    if (name === 'indexer') {
      const updatedItem = body.resource as IndexerModel;

      if (body.action === 'created' || body.action === 'updated') {
        updateQueryClientItem(queryClient, ['/indexer'], updatedItem, true);
      } else if (body.action === 'deleted') {
        removeQueryClientItem(queryClient, ['/indexer'], body.resource.id);
      }

      return;
    }

    if (name === 'metadata') {
      const updatedItem = body.resource as MetadataModel;

      if (body.action === 'updated') {
        updateQueryClientItem(queryClient, ['/metadata'], updatedItem, false);
      }

      return;
    }

    if (name === 'connection') {
      const updatedItem = body.resource as NotificationModel;

      if (body.action === 'created' || body.action === 'updated') {
        updateQueryClientItem(
          queryClient,
          ['/connection'],
          updatedItem,
          body.action === 'created'
        );
      } else if (body.action === 'deleted') {
        removeQueryClientItem(queryClient, ['/connection'], body.resource.id);
      }

      return;
    }

    if (name === 'qualitydefinition') {
      queryClient.invalidateQueries({ queryKey: ['/qualitydefinition'] });
      return;
    }

    if (name === 'queue') {
      queryClient.invalidateQueries({ queryKey: ['/queue'] });
      return;
    }

    if (name === 'queue/details') {
      queryClient.invalidateQueries({ queryKey: ['/queue/details'] });
      return;
    }

    if (name === 'queue/status') {
      const statusDetails = queryClient.getQueriesData({
        queryKey: ['/queue/status'],
      });

      statusDetails.forEach(([queryKey]) => {
        queryClient.setQueryData(queryKey, () => body.resource);
      });

      return;
    }

    if (name === 'rootfolder') {
      queryClient.invalidateQueries({ queryKey: ['/rootFolder'] });

      return;
    }

    if (name === 'series') {
      if (body.action === 'updated') {
        const updatedItem = body.resource as Series;

        updateQueryClientItem(queryClient, ['/series'], updatedItem, true);

        repopulatePage('seriesUpdated');
      } else if (body.action === 'deleted') {
        removeQueryClientItem(queryClient, ['/series'], body.resource.id);
      }

      return;
    }

    if (name === 'system/task') {
      queryClient.invalidateQueries({ queryKey: ['/system/task'] });
      return;
    }

    if (name === 'tag') {
      if (body.action !== 'sync') {
        return;
      }

      queryClient.invalidateQueries({ queryKey: ['/tag'] });
      queryClient.invalidateQueries({ queryKey: ['/tag/detail'] });

      return;
    }

    if (name === 'version') {
      setVersion({ version: body.version });
      return;
    }

    if (name === 'wanted/cutoff') {
      if (body.action !== 'updated') {
        return;
      }

      updatePagedItem<Episode>(
        queryClient,
        ['/wanted/cutoff'],
        body.resource as Episode
      );

      return;
    }

    if (name === 'wanted/missing') {
      if (body.action !== 'updated') {
        return;
      }

      updatePagedItem<Episode>(
        queryClient,
        ['/wanted/missing'],
        body.resource as Episode
      );

      return;
    }

    if (name === 'releaseSearchProgress') {
      handleReleaseSearchProgress(body);

      return;
    }

    if (name === 'transcodeProgress') {
      queryClient.invalidateQueries({ queryKey: ['/media-compression/jobs'] });
      queryClient.invalidateQueries({
        queryKey: ['movies', '/media-compression/jobs'],
      });

      return;
    }

    console.error(`signalR: Unable to find handler for ${name}`);
  });

  const handleReceiveMoviesMessage = useRef((message: SignalRMessage) => {
    console.debug(
      `[signalR:movies] received ${message.name}${
        message.version ? ` v${message.version}` : ''
      }`,
      message.body
    );

    const { name, body } = message;

    if (name === 'movie') {
      if (body.action === 'updated') {
        updateQueryClientItem(
          queryClient,
          ['movies', '/movie'],
          body.resource,
          true
        );

        repopulatePage('movieUpdated');
      } else if (body.action === 'deleted') {
        removeQueryClientItem(
          queryClient,
          ['movies', '/movie'],
          body.resource.id
        );
      }

      return;
    }

    if (name === 'moviefile') {
      if (body.action === 'updated') {
        updateQueryClientItem(
          queryClient,
          ['movies', '/moviefile'],
          body.resource,
          true
        );

        repopulatePage('movieFileUpdated');
      } else if (body.action === 'deleted') {
        removeQueryClientItem(
          queryClient,
          ['movies', '/moviefile'],
          body.resource.id
        );
      }

      return;
    }

    if (name === 'downloadclient') {
      const updatedItem = body.resource as DownloadClientModel;

      if (body.action === 'created' || body.action === 'updated') {
        updateQueryClientItem(
          queryClient,
          ['movies', '/downloadclient'],
          updatedItem,
          true
        );
      } else if (body.action === 'deleted') {
        removeQueryClientItem(
          queryClient,
          ['movies', '/downloadclient'],
          body.resource.id
        );
      }

      return;
    }

    if (name === 'collection') {
      queryClient.invalidateQueries({ queryKey: ['movies', '/collection'] });
      return;
    }

    if (name === 'command') {
      if (body.action === 'sync') {
        queryClient.invalidateQueries({ queryKey: ['/command'] });
        return;
      }

      updateCommand(body.resource as Command);
      return;
    }

    if (name === 'health') {
      queryClient.invalidateQueries({ queryKey: ['movies', '/health'] });
      return;
    }

    if (name === 'queue') {
      queryClient.invalidateQueries({ queryKey: ['movies', '/queue'] });
      return;
    }

    if (name === 'rootfolder') {
      queryClient.invalidateQueries({ queryKey: ['movies', '/rootfolder'] });
      return;
    }

    if (name === 'system/task') {
      queryClient.invalidateQueries({ queryKey: ['movies', '/system/task'] });
      return;
    }

    if (name === 'version') {
      setVersion({ version: body.version });
      return;
    }

    if (name === 'releaseSearchProgress') {
      handleReleaseSearchProgress(body);
      return;
    }

    if (name === 'transcodeProgress') {
      queryClient.invalidateQueries({ queryKey: ['/media-compression/jobs'] });
      queryClient.invalidateQueries({
        queryKey: ['movies', '/media-compression/jobs'],
      });

      return;
    }

    console.debug(`[signalR:movies] no handler for ${name}`);
  });

  useEffect(() => {
    console.log('[signalR] starting');

    const seriesService = getService('series');
    const moviesService = getService('movies');

    connection.current = new HubConnectionBuilder()
      .configureLogging(new SignalRLogger(LogLevel.Information))
      .withUrl(
        `${
          seriesService.signalrRoot
        }/messages?access_token=${encodeURIComponent(seriesService.apiKey)}`
      )
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.elapsedMilliseconds > 180000) {
            setAppValue({ isDisconnected: true });
          }

          return Math.min(retryContext.previousRetryCount, 10) * 1000;
        },
      })
      .build();

    connection.current.onreconnecting(handleReconnecting.current);
    connection.current.onreconnected(handleReconnected.current);
    connection.current.onclose(handleClose.current);

    connection.current.on('receiveMessage', handleReceiveMessage.current);

    connection.current
      .start()
      .then(handleStart.current, handleStartFail.current);

    moviesConnection.current = new HubConnectionBuilder()
      .configureLogging(new SignalRLogger(LogLevel.Information))
      .withUrl(
        `${
          moviesService.signalrRoot
        }/messages?access_token=${encodeURIComponent(moviesService.apiKey)}`
      )
      .withAutomaticReconnect()
      .build();

    moviesConnection.current.on(
      'receiveMessage',
      handleReceiveMoviesMessage.current
    );

    moviesConnection.current.start().catch((error: unknown) => {
      console.error('[signalR:movies] failed to connect');
      console.error(error);
    });

    return () => {
      connection.current?.stop();
      connection.current = null;

      moviesConnection.current?.stop();
      moviesConnection.current = null;
    };
  }, []);

  return null;
}

export default SignalRListener;

const updatePagedItem = <T extends ModelBase>(
  queryClient: ReturnType<typeof useQueryClient>,
  queryKey: QueryKey,
  updatedItem: T
) => {
  queryClient.setQueriesData(
    { queryKey },
    (oldData: PagedQueryResponse<T> | undefined) => {
      if (!oldData) {
        return oldData;
      }

      const itemIndex = oldData.records.findIndex(
        (item) => item.id === updatedItem.id
      );

      if (itemIndex === -1) {
        return oldData;
      }

      return {
        ...oldData,
        records: oldData.records.map((item) => {
          if (item.id === updatedItem.id) {
            return updatedItem;
          }

          return item;
        }),
      };
    }
  );
};

const updateQueryClientItem = <T extends ModelBase>(
  queryClient: ReturnType<typeof useQueryClient>,
  queryKey: QueryKey,
  updatedItem: T,
  addMissing: boolean
) => {
  queryClient.setQueriesData({ queryKey }, (oldData: T[] | undefined) => {
    if (!oldData) {
      return oldData;
    }

    const itemIndex = oldData.findIndex((item) => item.id === updatedItem.id);

    if (itemIndex === -1 && addMissing) {
      return [...oldData, updatedItem];
    }

    return oldData.map((item) => {
      if (item.id === updatedItem.id) {
        return updatedItem;
      }

      return item;
    });
  });
};

const removeQueryClientItem = <T extends ModelBase>(
  queryClient: ReturnType<typeof useQueryClient>,
  queryKey: QueryKey,
  id: T['id']
) => {
  queryClient.setQueriesData({ queryKey }, (oldData: T[] | undefined) => {
    if (!oldData) {
      return oldData;
    }

    const itemIndex = oldData.findIndex((item) => item.id === id);

    if (itemIndex === -1) {
      return oldData;
    }

    return oldData.filter((item) => item.id !== id);
  });
};
