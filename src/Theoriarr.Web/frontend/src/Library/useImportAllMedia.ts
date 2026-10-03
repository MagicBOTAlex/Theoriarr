import { useCallback } from 'react';
import Command, { NewCommandBody } from 'Commands/Command';
import useApiMutation from 'Helpers/Hooks/useApiMutation';

const IMPORT_ALL_COMMAND: NewCommandBody = { name: 'ImportAll' };

// The ImportAll command is server-side and global (the command queue is shared by
// both domains), so a single push covers every root folder for both Series and Movies.
const useImportAllMedia = () => {
  const { mutate, isPending } = useApiMutation<Command, NewCommandBody>({
    path: '/command',
    method: 'POST',
    service: 'series',
  });

  const importAll = useCallback(() => {
    mutate(IMPORT_ALL_COMMAND);
  }, [mutate]);

  return {
    importAll,
    isImporting: isPending,
  };
};

export default useImportAllMedia;
