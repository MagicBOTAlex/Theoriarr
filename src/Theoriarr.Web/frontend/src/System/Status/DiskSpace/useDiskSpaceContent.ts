import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { DiskSpaceContent } from 'typings/DiskSpace';

const useDiskSpaceContent = (path?: string) => {
  const result = useApiQuery<DiskSpaceContent[]>({
    path: '/diskspace/content',
    queryParams: { path },
    queryOptions: {
      enabled: !!path,
    },
  });

  return {
    ...result,
    data: result.data ?? [],
  };
};

export default useDiskSpaceContent;
